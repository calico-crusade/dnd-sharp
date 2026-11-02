using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace DndSharp.Database;

public sealed record CampaignSubscription(ulong GuildId, ulong ChannelId, long CampaignId, string CampaignSlug, DualityDisplaySettings? Duality = null);
public sealed record DualityDisplaySettings(bool Enabled = true, string SuccessFear = "✅ 💀", string SuccessHope = "✅ ✨",
    string FailureFear = "❌ 💀", string FailureHope = "❌ ✨", string Critical = "🎉🎉🎉", string Fear = "💀", string Hope = "✨", int? Difficulty = 12);
public sealed record CharacterClaim(long CharacterId, ulong UserId);

/// <summary>SQLite storage for subscriptions, campaign settings, and character claims.</summary>
public sealed class CampaignSubscriptionStore(IConfiguration configuration)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private bool initialized;
    private string DatabasePath => configuration["Discord:DatabasePath"] ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DndSharp", "campaigns.sqlite3");
    private string LegacyPath => configuration["Discord:CampaignSubscriptionsPath"] ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DndSharp", "campaign-subscriptions.json");

    private async Task<SqliteConnection> OpenAsync(CancellationToken token)
    {
        var path = Path.GetFullPath(DatabasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, ForeignKeys = true, Pooling = false }.ToString());
        try { await db.OpenAsync(token); return db; }
        catch { await db.DisposeAsync(); throw; }
    }

    private async Task InitializeAsync(CancellationToken token)
    {
        if (initialized) return;
        await using var db = await OpenAsync(token);
        await using var tx = db.BeginTransaction();
        await ExecuteAsync(db, tx, """
            CREATE TABLE IF NOT EXISTS campaigns (guild_id TEXT NOT NULL, campaign_id INTEGER NOT NULL, slug TEXT NOT NULL, duality_json TEXT, PRIMARY KEY (guild_id, campaign_id));
            CREATE TABLE IF NOT EXISTS subscriptions (guild_id TEXT NOT NULL, channel_id TEXT NOT NULL, campaign_id INTEGER NOT NULL, PRIMARY KEY (guild_id, channel_id, campaign_id), FOREIGN KEY (guild_id, campaign_id) REFERENCES campaigns(guild_id, campaign_id) ON DELETE CASCADE);
            CREATE TABLE IF NOT EXISTS character_claims (guild_id TEXT NOT NULL, campaign_id INTEGER NOT NULL, character_id INTEGER NOT NULL, user_id TEXT NOT NULL, PRIMARY KEY (guild_id, campaign_id, character_id), FOREIGN KEY (guild_id, campaign_id) REFERENCES campaigns(guild_id, campaign_id) ON DELETE CASCADE);
            CREATE TABLE IF NOT EXISTS metadata (key TEXT PRIMARY KEY, value TEXT NOT NULL);
            """, token);
        await using var marker = db.CreateCommand(); marker.Transaction = tx;
        marker.CommandText = "SELECT value FROM metadata WHERE key = 'legacy_import'";
        if (await marker.ExecuteScalarAsync(token) is null)
        {
            await using var count = db.CreateCommand(); count.Transaction = tx; count.CommandText = "SELECT count(*) FROM subscriptions";
            if (Convert.ToInt64(await count.ExecuteScalarAsync(token)) == 0 && File.Exists(LegacyPath))
            {
                await using var file = File.OpenRead(LegacyPath);
                var items = await JsonSerializer.DeserializeAsync<CampaignSubscription[]>(file, cancellationToken: token) ?? throw new FormatException("The legacy subscription file is invalid.");
                foreach (var item in items.DistinctBy(item => (item.GuildId, item.ChannelId, item.CampaignId))) { Validate(item); await UpsertAsync(db, tx, item, token); }
            }
            await ExecuteAsync(db, tx, "INSERT INTO metadata (key, value) VALUES ('legacy_import', 'complete')", token);
        }
        await tx.CommitAsync(token);
        initialized = true;
    }

    public async Task<IReadOnlyList<CampaignSubscription>> LoadAsync(CancellationToken token = default)
    {
        await gate.WaitAsync(token);
        try
        {
            await InitializeAsync(token);
            await using var db = await OpenAsync(token);
            await using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT s.guild_id,s.channel_id,s.campaign_id,c.slug,c.duality_json FROM subscriptions s JOIN campaigns c ON c.guild_id=s.guild_id AND c.campaign_id=s.campaign_id";
            var items = new List<CampaignSubscription>();
            await using var reader = await cmd.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                var item = new CampaignSubscription(ulong.Parse(reader.GetString(0)), ulong.Parse(reader.GetString(1)), reader.GetInt64(2), reader.GetString(3), reader.IsDBNull(4) ? null : JsonSerializer.Deserialize<DualityDisplaySettings>(reader.GetString(4)));
                Validate(item); items.Add(item);
            }
            return items;
        }
        finally { gate.Release(); }
    }

    public async Task SaveAsync(IEnumerable<CampaignSubscription> items, CancellationToken token = default)
    {
        var all = items.ToArray(); foreach (var item in all) Validate(item);
        await gate.WaitAsync(token);
        try
        {
            await InitializeAsync(token);
            await using var db = await OpenAsync(token); await using var tx = db.BeginTransaction();
            await ExecuteAsync(db, tx, "DELETE FROM subscriptions", token);
            foreach (var item in all) await UpsertAsync(db, tx, item, token);
            await ExecuteAsync(db, tx, "DELETE FROM campaigns WHERE NOT EXISTS (SELECT 1 FROM subscriptions s WHERE s.guild_id=campaigns.guild_id AND s.campaign_id=campaigns.campaign_id)", token);
            await tx.CommitAsync(token);
        }
        finally { gate.Release(); }
    }

    public async Task<bool> ClaimAsync(ulong guildId, long campaignId, long characterId, ulong userId, CancellationToken token = default)
    {
        if (guildId == 0 || campaignId <= 0 || characterId <= 0 || userId == 0) throw new ArgumentOutOfRangeException();
        await gate.WaitAsync(token);
        try
        {
            await InitializeAsync(token);
            await using var db = await OpenAsync(token); await using var cmd = db.CreateCommand();
            cmd.CommandText = "INSERT INTO character_claims (guild_id,campaign_id,character_id,user_id) VALUES ($guild,$campaign,$character,$user) ON CONFLICT(guild_id,campaign_id,character_id) DO UPDATE SET user_id=excluded.user_id WHERE character_claims.user_id=excluded.user_id";
            AddClaimParameters(cmd, guildId, campaignId, characterId, userId);
            return await cmd.ExecuteNonQueryAsync(token) != 0;
        }
        finally { gate.Release(); }
    }

    public async Task<bool> UnclaimAsync(ulong guildId, long campaignId, long characterId, ulong userId, CancellationToken token = default)
    {
        await gate.WaitAsync(token);
        try
        {
            await InitializeAsync(token);
            await using var db = await OpenAsync(token); await using var cmd = db.CreateCommand();
            cmd.CommandText = "DELETE FROM character_claims WHERE guild_id=$guild AND campaign_id=$campaign AND character_id=$character AND user_id=$user";
            AddClaimParameters(cmd, guildId, campaignId, characterId, userId);
            return await cmd.ExecuteNonQueryAsync(token) != 0;
        }
        finally { gate.Release(); }
    }

    public async Task<IReadOnlyList<CharacterClaim>> ClaimsAsync(ulong guildId, long campaignId, CancellationToken token = default)
    {
        await gate.WaitAsync(token);
        try
        {
            await InitializeAsync(token);
            await using var db = await OpenAsync(token); await using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT character_id,user_id FROM character_claims WHERE guild_id=$guild AND campaign_id=$campaign";
            cmd.Parameters.AddWithValue("$guild", guildId.ToString()); cmd.Parameters.AddWithValue("$campaign", campaignId);
            var items = new List<CharacterClaim>(); await using var reader = await cmd.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) items.Add(new CharacterClaim(reader.GetInt64(0), ulong.Parse(reader.GetString(1))));
            return items;
        }
        finally { gate.Release(); }
    }

    private static void AddClaimParameters(SqliteCommand cmd, ulong guildId, long campaignId, long characterId, ulong userId)
    {
        cmd.Parameters.AddWithValue("$guild", guildId.ToString()); cmd.Parameters.AddWithValue("$campaign", campaignId);
        cmd.Parameters.AddWithValue("$character", characterId); cmd.Parameters.AddWithValue("$user", userId.ToString());
    }

    private static void Validate(CampaignSubscription item)
    {
        if (item.GuildId == 0 || item.ChannelId == 0 || item.CampaignId <= 0 || item.CampaignSlug is null || !Regex.IsMatch(item.CampaignSlug, "^[a-zA-Z0-9_-]{1,80}$", RegexOptions.CultureInvariant))
            throw new FormatException("A saved campaign subscription is invalid.");
    }

    private static async Task UpsertAsync(SqliteConnection db, SqliteTransaction tx, CampaignSubscription item, CancellationToken token)
    {
        await using var cmd = db.CreateCommand(); cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO campaigns (guild_id,campaign_id,slug,duality_json) VALUES ($guild,$campaign,$slug,$duality)
            ON CONFLICT(guild_id,campaign_id) DO UPDATE SET slug=excluded.slug,duality_json=excluded.duality_json;
            INSERT OR IGNORE INTO subscriptions (guild_id,channel_id,campaign_id) VALUES ($guild,$channel,$campaign);
            """;
        cmd.Parameters.AddWithValue("$guild", item.GuildId.ToString()); cmd.Parameters.AddWithValue("$channel", item.ChannelId.ToString());
        cmd.Parameters.AddWithValue("$campaign", item.CampaignId); cmd.Parameters.AddWithValue("$slug", item.CampaignSlug);
        cmd.Parameters.AddWithValue("$duality", item.Duality is null ? DBNull.Value : JsonSerializer.Serialize(item.Duality));
        await cmd.ExecuteNonQueryAsync(token);
    }

    private static async Task ExecuteAsync(SqliteConnection db, SqliteTransaction tx, string sql, CancellationToken token)
    {
        await using var cmd = db.CreateCommand(); cmd.Transaction = tx; cmd.CommandText = sql; await cmd.ExecuteNonQueryAsync(token);
    }
}
