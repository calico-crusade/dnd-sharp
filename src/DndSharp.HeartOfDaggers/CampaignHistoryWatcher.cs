using System.Net.Http;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using SocketIOClient;
using SocketIOClient.Common;

namespace DndSharp.HeartOfDaggers;

/// <summary>Watches new campaign history and reconciles missed entries after reconnecting.</summary>
public sealed class CampaignHistoryWatcher(HeartOfDaggersClient api, ILogger<CampaignHistoryWatcher> logger)
{
    public async Task WatchAsync(string campaign, Func<CampaignHistoryItem, CancellationToken, Task> onItem,
        CancellationToken token = default, CampaignHistoryTracker? existingTracker = null)
    {
        ArgumentNullException.ThrowIfNull(onItem);
        var session = await api.GetCampaignAsync(campaign, token);
        var sessionId = session.Id;
        var tracker = existingTracker ?? new CampaignHistoryTracker(session.Id);
        if (tracker.CampaignId != session.Id)
            throw new ArgumentException("History tracker belongs to a different campaign.", nameof(existingTracker));
        if (!tracker.HasBaseline)
        {
            var baseline = await CampaignHistoryRecovery.ReadAsync(api, session, false, logger, token);
            if (baseline is not null)
                tracker.Seed(baseline.Entries);
            else
                logger.LogWarning("Campaign history is unavailable; starting the live watcher without a history baseline.");
        }

        while (!token.IsCancellationRequested)
        {
            // Obtain a fresh socket credential and membership before each connection.
            try
            {
                session = await api.GetCampaignAsync(campaign, token);
            }
            catch (HttpRequestException)
            {
                logger.LogWarning("Could not refresh campaign information; retrying.");
                await Task.Delay(TimeSpan.FromSeconds(3), token);
                continue;
            }
            if (session.Id != sessionId)
                throw new InvalidOperationException("The campaign ID changed while watching history.");
            var incoming = Channel.CreateUnbounded<HistoryMessage>(new UnboundedChannelOptions { SingleReader = true });
            var joined = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var reconcileCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            Task? reconciliationTask = null;
            using var socket = new SocketIO(session.SocketUri, new SocketIOOptions
            {
                EIO = EngineIO.V4,
                Transport = TransportProtocol.WebSocket,
                Reconnection = false,
                ConnectionTimeout = TimeSpan.FromSeconds(20)
            });
            socket.OnDisconnected += (_, _) => incoming.Writer.TryComplete();
            socket.On("campaign:joined_campaign_only", context =>
            {
                var value = context.GetValue<JsonElement>(0);
                if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("campaignId", out var id) &&
                    id.TryGetInt64(out var joinedId) && joinedId == session.Id)
                    joined.TrySetResult();
                return Task.CompletedTask;
            });
            socket.On("campaign:error", _ =>
            {
                joined.TrySetException(new UnauthorizedAccessException("Heart of Daggers rejected the campaign socket subscription."));
                incoming.Writer.TryComplete();
                return Task.CompletedTask;
            });
            foreach (var eventName in new[] { "campaign:dice:rolled", "campaign:spotlight:requested" })
                socket.On(eventName, context =>
                {
                    var payload = context.GetValue<JsonElement>(0);
                    if (payload.ValueKind == JsonValueKind.Object &&
                        payload.TryGetProperty("campaignId", out var eventCampaign) && eventCampaign.TryGetInt64(out var id) && id == session.Id &&
                        payload.TryGetProperty("entry", out var entry) && entry.ValueKind == JsonValueKind.Object)
                        incoming.Writer.TryWrite(new HistoryMessage(entry.Clone(), null));
                    return Task.CompletedTask;
                });

            try
            {
                await socket.ConnectAsync(token);
                await socket.EmitAsync("campaign:join_campaign_only", new object[]
                {
                    new
                    {
                        campaignId = session.Id,
                        characterIds = session.CharacterIds,
                        ownedCharacterIds = session.OwnedCharacterIds,
                        accessMode = session.AccessMode,
                        userId = session.UserId,
                        authToken = session.SocketAuth
                    }
                }, token);
                await joined.Task.WaitAsync(TimeSpan.FromSeconds(15), token);
                logger.LogInformation("Listening to Heart of Daggers campaign {CampaignId}.", session.Id);

                // Subscribe first, then reconcile. Events arriving during the read are buffered and deduplicated.
                reconciliationTask = ReconcileInBackground(api, session, incoming.Writer, logger, reconcileCancellation.Token);
                await foreach (var message in incoming.Reader.ReadAllAsync(token))
                {
                    if (message.Entry is { } entry)
                        await Deliver(entry);
                    else if (message.Snapshot is { } snapshot)
                    {
                        if (!tracker.HasBaseline)
                            tracker.Seed(snapshot.Entries);
                        else
                            foreach (var historyEntry in snapshot.Entries)
                                await Deliver(historyEntry);
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (HistoryConsumerException ex)
            {
                throw ex.InnerException!;
            }
            catch (Exception ex)
            {
                // Avoid logging the socket URL, token or server payload.
                logger.LogWarning("Campaign connection failed ({ErrorType}); retrying.", ex.GetType().Name);
            }
            finally
            {
                reconcileCancellation.Cancel();
                if (reconciliationTask is not null)
                {
                    try { await reconciliationTask; }
                    catch (OperationCanceledException) when (reconcileCancellation.IsCancellationRequested) { }
                }
                if (socket.Connected)
                    await socket.DisconnectAsync();
            }

            if (!token.IsCancellationRequested)
                await Task.Delay(TimeSpan.FromSeconds(3), token);
        }

        async Task Deliver(JsonElement entry)
        {
            if (!tracker.TryObserve(entry, out var item))
                return;
            try
            {
                await onItem(item!, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new HistoryConsumerException(ex);
            }
        }
    }

    internal static async Task ReconcileInBackground(HeartOfDaggersClient api, CampaignSession session,
        ChannelWriter<HistoryMessage> writer, ILogger logger, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var snapshot = await CampaignHistoryRecovery.ReadAsync(api, session, true, logger, token);
                if (snapshot is not null && !writer.TryWrite(new HistoryMessage(null, snapshot)))
                    return;
                if (snapshot is not null && !snapshot.Retry)
                    return;
                logger.LogWarning("Campaign history is unavailable; live notifications remain active and history will be retried.");
                await Task.Delay(TimeSpan.FromSeconds(30), token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            writer.TryComplete(ex);
        }
    }

    internal sealed record HistoryMessage(JsonElement? Entry, CampaignHistorySnapshot? Snapshot);
    private sealed class HistoryConsumerException(Exception inner) : Exception("History notification handler failed.", inner);
}
