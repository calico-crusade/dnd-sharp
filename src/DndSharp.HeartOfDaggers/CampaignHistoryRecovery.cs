using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DndSharp.HeartOfDaggers;

internal sealed record CampaignHistorySnapshot(IReadOnlyList<JsonElement> Entries, bool Retry);

/// <summary>Reads history from the AJAX endpoint or the authorized campaign page.</summary>
internal static class CampaignHistoryRecovery
{
    public static async Task<CampaignHistorySnapshot?> ReadAsync(HeartOfDaggersClient api, CampaignSession session,
        bool refreshPage, ILogger logger, CancellationToken token)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            return new CampaignHistorySnapshot(await api.GetCampaignHistoryAsync(session, timeout.Token), false);
        }
        catch (Exception ex) when (IsTransient(ex, token))
        {
            logger.LogWarning("Campaign history endpoint failed ({ErrorType}); checking the campaign page for history.", ex.GetType().Name);
        }

        if (refreshPage)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                var refreshed = await api.GetCampaignAsync(session.Slug, timeout.Token);
                if (refreshed.Id != session.Id)
                    throw new InvalidOperationException("Campaign identity changed while reading history.");
                if (refreshed.EmbeddedHistory is not null)
                {
                    logger.LogInformation("Using campaign history from the refreshed campaign page.");
                    return new CampaignHistorySnapshot(refreshed.EmbeddedHistory, false);
                }
            }
            catch (Exception ex) when (IsTransient(ex, token))
            {
                logger.LogWarning("Could not refresh campaign history from the page ({ErrorType}).", ex.GetType().Name);
            }
        }
        if (session.EmbeddedHistory is null)
            return null;
        logger.LogInformation("Using campaign history from the previously loaded campaign page.");
        return new CampaignHistorySnapshot(session.EmbeddedHistory, refreshPage);
    }

    private static bool IsTransient(Exception ex, CancellationToken token) =>
        ex is HttpRequestException or IOException or InvalidDataException or TimeoutException or JsonException ||
        (ex is OperationCanceledException && !token.IsCancellationRequested);
}
