using DndSharp.Cli.Verbs;
using DndSharp.Pokemon;

return await new ServiceCollection()
    .AddCardboardHttp()
    .AddPokeApi()
    .AddPokemonServices()
    .AddCoreServices()
    .Cli(args, c => c
        .Add<PrintPokemonVerb>());