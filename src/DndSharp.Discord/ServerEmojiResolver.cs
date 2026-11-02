using System.Text.RegularExpressions;
using Discord;
using Discord.WebSocket;

namespace DndSharp.Discord;

internal sealed record ServerEmoji(string Name, ulong Id, bool Animated, bool Available = true, ulong? GuildId = null, bool IsApplication = false)
{
    public string Markup => $"<{(Animated ? "a" : "")}:{Name}:{Id}>";
}

internal sealed class EmojiResolutionException(string message) : ArgumentException(message);

/// <summary>Translates server emoji shortcodes without changing existing Discord markup.</summary>
internal static class ServerEmojiResolver
{
    private static readonly Regex Tokens = new(
        @"<a?:[A-Za-z0-9_]{2,32}:(?<id>[0-9]{1,20})>|(?<![<\w]):(?<name>[A-Za-z0-9_]{2,32}):",
        RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    public static ServerEmoji[] FromGuilds(IEnumerable<SocketGuild> guilds) => guilds.SelectMany(guild =>
        guild.Emotes.Select(emote => new ServerEmoji(emote.Name, emote.Id, emote.Animated,
            emote.IsAvailable != false && (emote.RoleIds.Count == 0 || guild.CurrentUser is not null && guild.CurrentUser.Roles.Any(role => emote.RoleIds.Contains(role.Id))),
            guild.Id))).ToArray();

    public static bool HasTokens(string text) => Tokens.IsMatch(text);

    public static string Resolve(string text, IReadOnlyCollection<ServerEmoji> emotes, bool strict)
    {
        return Tokens.Replace(text, match =>
        {
            var name = match.Groups["name"];
            if (!name.Success)
            {
                if (!strict) return match.Value;
                if (!ulong.TryParse(match.Groups["id"].Value, out var id))
                    throw new EmojiResolutionException("The custom emoji ID is invalid.");
                var emoji = emotes.FirstOrDefault(item => item.Id == id && item.Available);
                if (emoji is null)
                    throw new EmojiResolutionException("The bot cannot access that custom emoji. Invite it to the emoji's server and grant any required role, or use an emoji uploaded to this server or the bot application.");
                return emoji.Markup;
            }
            var matches = emotes.Where(emote => emote.Available && emote.Name.Equals(name.Value, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length == 1) return matches[0].Markup;
            if (!strict) return match.Value;
            if (matches.Length == 0)
                throw new EmojiResolutionException($"No accessible emoji matches :{name.Value}:. Invite the bot to its server, use a Unicode emoji, or upload an application emoji.");
            throw new EmojiResolutionException($"More than one accessible emoji matches :{name.Value}:. Paste the full custom emoji markup to choose one.");
        });
    }

    public static bool UsesExternalGuildEmoji(string text, IReadOnlyCollection<ServerEmoji> emotes, ulong targetGuild)
    {
        foreach (Match match in Tokens.Matches(text))
            if (match.Groups["id"].Success && ulong.TryParse(match.Groups["id"].Value, out var id) &&
                emotes.Any(emoji => emoji.Id == id && !emoji.IsApplication && emoji.GuildId is not null && emoji.GuildId != targetGuild)) return true;
        return false;
    }
}
