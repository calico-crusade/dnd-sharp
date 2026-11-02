using CardboardBox;
using CommandLine;
using Microsoft.Extensions.Logging;

namespace DndSharp.Discord.Verbs;

[Verb("discord", HelpText = "Run the Discord slash-command bot until interrupted.")]
public sealed class DiscordOptions
{
    [Option("guild-id", HelpText = "Register commands in this server; otherwise use Discord:GuildId or register globally.")]
    public ulong? GuildId { get; set; }
}

public sealed class DiscordVerb(ILogger<DiscordVerb> logger, DiscordBot bot)
    : BooleanVerb<DiscordOptions>(logger)
{
    public override Task<bool> Execute(DiscordOptions options, CancellationToken token) =>
        bot.RunAsync(options.GuildId, token);
}
