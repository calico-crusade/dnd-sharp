using System.Diagnostics;

namespace DndSharp.Cli.Verbs;

using Pokemon;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Spectre.Console;

public enum StatType
{
    Min,
    Max,
    Randomize,
    Mid
}

[Verb("print-pokemon", HelpText = "Prints Pokemon's D&D information.")]
internal class PrintPokemonOptions
{
    private const string DEFAULT_OUTPUT = "pokemon.json";
    private const int DEFAULT_LEVEL = 78;

    [Option('n', "number", HelpText = "The ID of the Pokemon to print.", Default = null)]
    public int? Number { get; set; } = null;

    [Option('s', "search", HelpText = "The search term for the Pokemon to print.", Default = null)]
    public string? Search { get; set; } = null;

    [Option('o', "output", HelpText = "The output file path for the JSON file.", Default = DEFAULT_OUTPUT)]
    public string OutputFile { get; set; } = DEFAULT_OUTPUT;

    [Option('l', "level", HelpText = "The level of the Pokemon to print.", Default = DEFAULT_LEVEL)]
    public int Level { get; set; } = DEFAULT_LEVEL;

    [Option('e', "ev-stat-type", HelpText = "The method to determine EV stats.", Default = StatType.Min)]
    public StatType EvStatType { get; set; } = StatType.Min;

    [Option('i', "iv-stat-type", HelpText = "The method to determine IV stats.", Default = StatType.Min)]
    public StatType IvStatType { get; set; } = StatType.Min;
}

internal class PrintPokemonVerb(
    ILogger<PrintPokemonVerb> logger,
    IPokemonTranslationService _translate,
    IPokeApiService _poke,
    IApiService _api) : BooleanVerb<PrintPokemonOptions>(logger)
{
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true
    };

    public async Task<PkPokemon?> Search(PrintPokemonOptions options, CancellationToken token)
    {
        if (options.Number is not null)
        {
            var pk = await _poke.Fetch<PkPokemon>(options.Number.Value, token: token);
            if (pk is not null) return pk;
        }

        if (string.IsNullOrEmpty(options.Search)) return null;

        var search = await _poke.Indexed<PkPokemon>()
            .SearchResources(options.Search, token: token)
            .OrderByDescending(t => t.Score)
            .FirstOrDefaultAsync(token);
        if (search is null) return null;

        return await _poke.Fetch<PkPokemon>(search.Url, token: token);
    }

    public async Task<PokemonEVs> GetEVs(PrintPokemonOptions options, CancellationToken token)
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

    public async Task<PokemonIVs> GetIVs(PrintPokemonOptions options, CancellationToken token)
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

    public void GeneratePkStats(PokemonCharacter character)
    {
        static int[] Row(IPokemonStats stats)
        {
            int[] data = [stats.Hp, stats.Attack, stats.Defense, stats.SpecialAttack, stats.SpecialDefense, stats.Speed];
            var total = data.Sum();
            return [..data, total];
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
        _logger.GenerateTable(x, y, values);
    }

    public async Task LoadSprite(PokemonCharacter mon, CancellationToken token)
    {
        var sprite = mon.Pokemon.Sprites.FrontDefault;
        if (string.IsNullOrEmpty(sprite)) return;

        try
        {
            var (stream, _, _, _) = await _api.GetData(sprite);
            using var sli = Image<Rgba32>.Load(stream);
            var margin = 20;
            var rect = new Rectangle(margin, margin, sli.Width - (margin * 2), sli.Height - (margin * 2));
            sli.Mutate(t => t.Crop(rect));
            using var str = new MemoryStream();
            await sli.SaveAsPngAsync(str, token);
            str.Position = 0;
            var image = new CanvasImage(str);
            AnsiConsole.Write(image);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading image: {url}", sprite);
        }
    }

    public override async Task<bool> Execute(PrintPokemonOptions options, CancellationToken token)
    {
        var pokemon = await Search(options, token);
        if (pokemon is null)
        {
            _logger.LogError("Could not find Pokemon with ID {Id} or Name {Name}.", 
                options.Number?.ToString() ?? "null", options.Search?.ForceNull() ?? "null");
            return false;
        }

        _logger.LogInformation("Translating Pokemon {Name} (ID: {Id}) to D&D format...", 
            pokemon.Name, pokemon.Id);
        var translation = await _translate.Translate(pokemon.Id, token: token);
        if (translation is null)
        {
            _logger.LogError("Translation failed for Pokemon ID {Id}.", pokemon.Id);
            return false;
        }

        translation.Level = options.Level;
        translation.EVs = await GetEVs(options, token);
        translation.IVs = await GetIVs(options, token);
        using var io = File.Create(options.OutputFile);
        await JsonSerializer.SerializeAsync(io, translation, _options, token);
        _logger.LogInformation("Successfully wrote translated Pokemon to {File}.", options.OutputFile);
        await io.FlushAsync(token);
        await io.DisposeAsync();
#if DEBUG
        var abs = Path.GetFullPath(options.OutputFile);
        Process.Start("cmd.exe", $"/c code \"{abs}\"");
#endif

        await LoadSprite(translation, token);
        _logger.LogInformation("Name: {name} ({species}) - Lvl. {level} - Nature: {nature}",
            translation.Name, translation.SpeciesName, translation.Level, 
            translation.Nature.Localizations.PreferredOrFirst(t => t.Resource.Name == "en")?.Name);
        _logger.LogInformation("STR: {str} | DEX: {dex} | CON: {con} | INT: {int} | WIS: {wis} | CHR: {chr}",
            translation.DndStats.Strength, translation.DndStats.Dexterity,
            translation.DndStats.Constitution, translation.DndStats.Intelligence,
            translation.DndStats.Wisdom, translation.DndStats.Charisma);
        _logger.LogInformation("Generated Stats Table:");
        GeneratePkStats(translation);
        return true;
    }
}
