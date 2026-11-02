#if !BOT_ONLY
using DndSharp.Cli.Verbs;
#endif
using DndSharp.Pokemon;
using DndSharp.Discord;
using DndSharp.Discord.Verbs;
using DndSharp.HeartOfDaggers;
using DndSharp.HeartOfDaggers.Verbs;

return await new ServiceCollection()
    .AddCardboardHttp()
    .AddPokeApi()
    .AddPokemonServices()
    .AddCoreServices(c => c.AddUserSecrets<Program>())
    .AddDiscordBot()
    .AddHeartOfDaggers()
    .Cli(args, c => c
#if !BOT_ONLY
        .Add<PrintPokemonVerb>()
        .Add<GenStatsVerb>()
#endif
        .Add<DiscordVerb>()
        .Add<HeartOfDaggersVerb>()
        .Add<HeartMailboxCheckVerb>());
