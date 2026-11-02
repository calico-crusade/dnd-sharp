namespace DndSharp.Cli.Verbs;

using Pokemon;
using System.Runtime.CompilerServices;
using System.Transactions;

[Verb("gen-stats", HelpText = "Generate character stats for specified levels.")]
internal class GenStatsOptions
{
    [Option('l', "levels", HelpText = "The levels to generate stats for.", Separator = ',', Default = new int[] { 10, 50, 100 })]
    public IEnumerable<int> Levels { get; set; } = [10, 50, 100];

    [Option('p', "pokemon", HelpText = "The Pokémon to generate stats for.", Separator = ',', Default = new string[] { "blissey", "garchomp", "bulbasaur" })]
    public IEnumerable<string> Pokemon { get; set; } = ["blissey", "garchomp", "bulbasaur"];

    [Option('e', "ev-stat-type", HelpText = "The method to determine EV stats.", Default = StatType.Min)]
    public StatType EvStatType { get; set; } = StatType.Min;

    [Option('i', "iv-stat-type", HelpText = "The method to determine IV stats.", Default = StatType.Min)]
    public StatType IvStatType { get; set; } = StatType.Min;
}

internal class GenStatsVerb(
    ILogger<GenStatsVerb> logger,
    IPokemonTranslationService _translate,
    IPokeApiService _poke) : BooleanVerb<GenStatsOptions>(logger)
{
    public async Task<PkPokemon?> Search(string name, CancellationToken token)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        var search = await _poke.Indexed<PkPokemon>()
            .SearchResources(name, token: token)
            .OrderByDescending(t => t.Score)
            .FirstOrDefaultAsync(token);
        if (search is null)
            return null;

        return await _poke.Fetch<PkPokemon>(search.Url, token: token);
    }

    public async IAsyncEnumerable<PkPokemon> GetPokemon(GenStatsOptions options, [EnumeratorCancellation] CancellationToken token)
    {
        foreach (var name in options.Pokemon)
        {
            var pk = await Search(name, token);
            if (pk is null)
            {
                _logger.LogWarning("Could not find Pokémon: {Name}", name);
                continue;
            }

            yield return pk;
        }
    }

    public async IAsyncEnumerable<PokemonCharacter> Convert(GenStatsOptions options, IAsyncEnumerable<PkPokemon> mon, [EnumeratorCancellation] CancellationToken token)
    {
        await foreach (var pk in mon.WithCancellation(token))
        {
            var character = await _translate.Translate(pk.Id, token: token);
            if (character is null)
            {
                _logger.LogWarning("Could not translate Pokémon: {Name} ({Id})", pk.Name, pk.Id);
                continue;
            }

            character.EVs = await GetEVs(options, token);
            character.IVs = await GetIVs(options, token);
            yield return character;
        }
    }

    public async Task<PokemonEVs> GetEVs(GenStatsOptions options, CancellationToken token)
    {
        var mid = PokemonEVs.MAX_TOTAL / 12;
        var max = PokemonEVs.MAX_TOTAL / 6;
        return options.EvStatType switch
        {
            StatType.Randomize => await _translate.RandomizeEVs(token: token),
            StatType.Mid => new PokemonEVs(mid, mid, mid, mid, mid, mid),
            StatType.Max => new PokemonEVs(max, max, max, max, max, max),
            _ => PokemonEVs.Empty,
        };
    }

    public async Task<PokemonIVs> GetIVs(GenStatsOptions options, CancellationToken token)
    {
        var mid = PokemonIVs.MAX_STAT / 6;
        var max = PokemonIVs.MAX_STAT;
        return options.EvStatType switch
        {
            StatType.Randomize => await _translate.RandomizeIVs(token: token),
            StatType.Mid => new PokemonIVs(mid, mid, mid, mid, mid, mid),
            StatType.Max => new PokemonIVs(max, max, max, max, max, max),
            _ => PokemonIVs.Empty,
        };
    }


    public static string GeneratePkStats(PokemonCharacter character)
    {
        static int[] Row(IPokemonStats stats)
        {
            int[] data = [stats.Hp, stats.Attack, stats.Defense, stats.SpecialAttack, stats.SpecialDefense, stats.Speed];
            var total = data.Sum();
            return [.. data, total];
        }

        var stats = character.Stats;
        var x = new[] { "HP", "Attack", "Defense", "Sp. Atk", "Sp. Def", "Speed", "Total" };
        var y = new[] { "Base", "IVs", "EVs", "Nature", "Stats", };

        var @base = character.BaseStats;

        var nature = StatCalculator.Nature(character.Nature);
        int[][] values =
        [
            Row(character.BaseStats),
            Row(character.IVs),
            Row(character.EVs),
            Row(nature),
            Row(character.Stats)
        ];
        return TableGenerator.GenerateTable(x, y, values);
    }

    public static void PrintMon(StringBuilder bob, PokemonCharacter character, GenStatsOptions options)
    {
        bob.AppendLine($"# {character.Name} - {character.Nature.Name}");
        foreach (var level in options.Levels)
        {
            character.Level = level;
            var s = character.Stats;
            var d = character.DndStats;
            var line = $"HP:  {s.Hp:000} | ATK: {s.Attack:000} | DEF: {s.Defense:000} | SPA: {s.SpecialAttack:000} | SPD: {s.SpecialDefense:000} | SPE: {s.Speed:000}";
            var title = $" LEVEL {level:000} STATS ";
            var item = (line.Length - title.Length) / 2;

            bob.AppendLine($"{"".PadLeft(item, '-')}{title}{"".PadLeft(item, '-')}");
            bob.AppendLine(line);
            bob.AppendLine($"STR: {d.Strength:000} | DEX: {d.Dexterity:000} | CON: {d.Constitution:000} | INT: {d.Intelligence:000} | WIS: {d.Wisdom:000} | CHR: {d.Charisma:000}");
            bob.AppendLine();
        }

        bob.AppendLine();
    }

    public override async Task<bool> Execute(GenStatsOptions options, CancellationToken token)
    {
        var pokemon = GetPokemon(options, token);
        var characters = Convert(options, pokemon, token);
        var bob = new StringBuilder();

        var written = false;
        await foreach (var character in characters.WithCancellation(token))
        {
            if (!written)
            {
                written = true;
                var ts = character.Settings;
                bob.AppendLine($"Generation Settings: Pk Stat Range: {ts.LowStat} - {ts.HighStat}. " +
                    $"D&D Score Range: {ts.LowScore} - {ts.HighScore}. " +
                    $"D&D Score Clamp: {ts.MinScore} - {ts.MaxScore}.");
            }

            PrintMon(bob, character, options);
        }
        _logger.LogInformation("GENERATED STATS:\n{Stats}", bob.ToString());
        return true;
    }
}
