using DndSharp.Pokemon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace DndSharp.Discord;

public interface IPokemonCardRenderer
{
    // The caller owns the returned PNG stream, positioned at the start.
    Task<Stream> RenderAsync(PokemonCharacter character, CancellationToken token = default);
}

public sealed class PokemonCardRenderer(
    HttpClient http,
    IConfiguration configuration,
    ILogger<PokemonCardRenderer> logger) : IPokemonCardRenderer
{
    public const int Width = 744;
    public const int Height = 1040;

    private SKTypeface GetFont()
    {
        var fontPath = configuration["Discord:CardFontPath"];
        if (!string.IsNullOrWhiteSpace(fontPath))
            return SKTypeface.FromFile(fontPath)
                ?? throw new InvalidOperationException("Discord:CardFontPath must point to a readable font file.");
        using var stream = typeof(PokemonCardRenderer).Assembly.GetManifestResourceStream(
            "DndSharp.Discord.Assets.Lato-Regular.ttf")
            ?? throw new InvalidOperationException("The bundled card font is missing.");
        using var data = SKData.Create(stream);
        return SKTypeface.FromData(data)
            ?? throw new InvalidOperationException("The bundled card font could not be loaded.");
    }

    public async Task<Stream> RenderAsync(PokemonCharacter character, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        using var typeface = GetFont();
        var ink = SKColor.Parse("273447");
        var gold = SKColor.Parse("D7B85B");
        var paper = SKColor.Parse("FBF5E4");
        var accent = TypeColor(character.Pokemon.Types.OrderBy(type => type.Slot).FirstOrDefault()?.Resource.Name);
        var stats = character.DndStats;
        using var bitmap = new SKBitmap(Width, Height);
        using var canvas = new SKCanvas(bitmap);
        using var artwork = await LoadArtwork(character, token);
        using var paint = new SKPaint { IsAntialias = true };
        canvas.Clear(gold);

        void Panel(SKColor color, float x, float y, float width, float height)
        {
            paint.Color = color;
            canvas.DrawRect(x, y, width, height, paint);
        }

        void Text(string value, float size, float x, float y, float width,
            bool bold = false, SKColor? color = null)
        {
            using var font = new SKFont(typeface, size) { Embolden = bold };
            // Shrink long names to fit their allocated space without clipping or wrapping.
            var measured = font.MeasureText(value);
            if (measured > width)
                font.Size *= width / measured;
            paint.Color = color ?? ink;
            canvas.DrawText(value, x, y - font.Metrics.Ascent, SKTextAlign.Left, font, paint);
        }

        Panel(ink, 14, 14, 716, 1012);
        Panel(paper, 23, 23, 698, 994);
        Panel(accent, 23, 23, 698, 10);
        Text("DND SHARP  /  POKÉMON COMPANION", 15, 46, 48, 640, true);
        Text(character.Name, 36, 46, 79, 490, true);
        Text($"LV. {character.Level:00}", 29, 575, 85, 125, true);

        Panel(gold, 44, 137, 656, 368);
        Panel(SKColor.Parse("E8EBE4"), 50, 143, 644, 356);
        for (var x = 55; x < 690; x += 40)
            Panel(SKColor.Parse("DCE1DA"), x, 144, 1, 354);
        if (artwork is not null)
        {
            var scale = Math.Min(580f / artwork.Width, 336f / artwork.Height);
            var width = artwork.Width * scale;
            var height = artwork.Height * scale;
            using var image = SKImage.FromBitmap(artwork);
            canvas.DrawImage(image, SKRect.Create((Width - width) / 2, 153 + (336 - height) / 2, width, height),
                new SKSamplingOptions(SKFilterMode.Linear), paint);
        }
        else
        {
            Text($"#{character.Pokemon.Id:0000}", 70, 235, 252, 300, true, accent);
            Text("Artwork unavailable", 19, 270, 346, 300);
        }

        var types = string.Join(" / ", character.Pokemon.Types.OrderBy(type => type.Slot)
            .Select(type => type.Resource.Name?.ToUpperInvariant()));
        Text($"#{character.Pokemon.Id:0000}   {types}", 18, 50, 520, 640, true);
        Panel(accent, 44, 558, 656, 3);
        Text("D&D ABILITY SCORES", 20, 50, 578, 640, true);

        (string Label, int Score)[] abilities =
        [
            ("STR", stats.Strength), ("DEX", stats.Dexterity), ("CON", stats.Constitution),
            ("INT", stats.Intelligence), ("WIS", stats.Wisdom), ("CHA", stats.Charisma)
        ];
        for (var i = 0; i < abilities.Length; i++)
        {
            var x = 46 + i % 3 * 220;
            var y = 616 + i / 3 * 112;
            var (label, score) = abilities[i];
            var modifier = (int)Math.Floor((score - 10) / 2.0);
            Panel(SKColor.Parse("EAE4D2"), x, y, 212, 102);
            Panel(accent, x, y, 5, 102);
            Text(label, 16, x + 15, y + 10, 80, true);
            Text(score.ToString(), 36, x + 14, y + 40, 95, true);
            Text(modifier.ToString("+0;-0;0"), 24, x + 142, y + 46, 65, true, accent);
        }

        Text($"Pokémon HP: {character.Stats.Hp}   ·   Nature: {character.Nature.Name}", 18, 50, 854, 640, true);
        Text("Randomized IVs and EVs · Ability modifiers shown beside scores", 15, 50, 886, 640);
        Panel(accent, 44, 925, 656, 2);
        Text("DND SHARP", 17, 50, 949, 180, true);
        Text("Fan-made companion card · Data & artwork: PokéAPI", 13, 224, 953, 475);

        token.ThrowIfCancellationRequested();
        using var outputImage = SKImage.FromBitmap(bitmap);
        using var png = outputImage.Encode(SKEncodedImageFormat.Png, 100);
        return new MemoryStream(png.ToArray(), writable: false);
    }

    private async Task<SKBitmap?> LoadArtwork(PokemonCharacter character, CancellationToken token)
    {
        var url = PokemonCommandService.ArtworkUrl(character.Pokemon);
        if (string.IsNullOrWhiteSpace(url))
            return null;
        try
        {
            var bytes = await http.GetByteArrayAsync(url, token);
            return SKBitmap.Decode(bytes);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load Pokémon artwork for {Id}; rendering a card without artwork.", character.Pokemon.Id);
            return null;
        }
    }

    private static SKColor TypeColor(string? type) => SKColor.Parse(type switch
    {
        "fire" => "AD4B26",
        "water" => "2E668C",
        "grass" => "3C7250",
        "electric" => "9B7515",
        "psychic" => "9F466B",
        "ice" => "387E86",
        "dragon" => "66519B",
        "dark" => "55505E",
        "ghost" => "69547F",
        "fairy" => "A75F87",
        "poison" => "7D5189",
        "fighting" => "945247",
        "ground" => "8A6A3B",
        "rock" => "7A704C",
        "steel" => "536B78",
        "bug" => "667D35",
        "flying" => "5D72A0",
        _ => "626E68"
    });
}
