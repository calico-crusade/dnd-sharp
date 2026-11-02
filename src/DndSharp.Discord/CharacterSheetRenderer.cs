using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using DndSharp.HeartOfDaggers;
using SkiaSharp;

namespace DndSharp.Discord;

public interface ICharacterSheetRenderer
{
    Task<Stream> RenderAsync(CharacterSnapshot character, CancellationToken token = default);
}

/// <summary>Renders the available character data in a layout inspired by the Heart of Daggers sheet.</summary>
public sealed class CharacterSheetRenderer(HttpClient http) : ICharacterSheetRenderer
{
    private const int Width = 1600;
    private const int MaxHeight = 9000;
    private static readonly SemaphoreSlim RenderGate = new(2, 2);
    private static readonly SKColor Background = SKColor.Parse("11151D");
    private static readonly SKColor Paper = SKColor.Parse("F7F7F8");
    private static readonly SKColor Header = SKColor.Parse("D5D8DC");
    private static readonly SKColor Ink = SKColor.Parse("20232C");
    private static readonly SKColor Muted = SKColor.Parse("636A78");
    private static readonly SKColor Purple = SKColor.Parse("5B2990");
    private static readonly SKColor Gold = SKColor.Parse("B69445");

    public async Task<Stream> RenderAsync(CharacterSnapshot snapshot, CancellationToken token = default)
    {
        await RenderGate.WaitAsync(token);
        try { return await RenderCoreAsync(snapshot, token); }
        finally { RenderGate.Release(); }
    }

    private async Task<Stream> RenderCoreAsync(CharacterSnapshot snapshot, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var root = snapshot.Character;
        if (root.ValueKind != JsonValueKind.Object) throw new ArgumentException("Character data is unavailable.");
        using var typeface = LoadTypeface();
        using var bitmap = new SKBitmap(Width, MaxHeight);
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint { IsAntialias = true };
        using var portrait = await LoadPortraitAsync(Text(root, "portraitUrl"), token);
        canvas.Clear(Background);
        var render = new Painter(canvas, paint, typeface);

        render.Rect(Header, 0, 0, Width, 180);
        render.Rect(SKColor.Parse("BC9A4A"), 0, 180, Width, 4);
        render.Rect(SKColor.Parse("742839"), 42, 0, 82, 180);
        render.Rect(Gold, 48, 0, 3, 180);
        render.Rect(Gold, 116, 0, 3, 180);
        if (portrait is not null)
        {
            using var image = SKImage.FromBitmap(portrait);
            using var border = new SKPaint { IsAntialias = true, Color = Gold, Style = SKPaintStyle.Stroke, StrokeWidth = 5 };
            canvas.DrawCircle(160, 88, 67, border);
            canvas.Save();
            using var clipBuilder = new SKPathBuilder(); clipBuilder.AddCircle(160, 88, 62); using var clip = clipBuilder.Detach(); canvas.ClipPath(clip, antialias: true);
            var side = Math.Min(image.Width, image.Height);
            var source = SKRect.Create((image.Width - side) / 2f, (image.Height - side) / 2f, side, side);
            canvas.DrawImage(image, source, SKRect.Create(98, 26, 124, 124), new SKSamplingOptions(SKFilterMode.Linear), paint);
            canvas.Restore();
        }
        else
        {
            render.Circle(SKColor.Parse("A9AEB7"), 160, 88, 62);
            render.Text("◆", 54, 138, 51, 50, true, Paper);
        }
        render.Text(Text(root, "name") ?? "Unnamed character", 35, 246, 34, 610, true, Ink);
        var subtitle = string.Join("  •  ", new[] { Prefix("Level ", Text(root, "level")), Text(root, "ancestry"), Text(root, "community"),
            JoinNonEmpty(" / ", Text(root, "className"), Text(root, "subclassName")) }.Where(value => !string.IsNullOrWhiteSpace(value)));
        render.Text(subtitle, 18, 248, 87, 600, false, Muted);
        render.Text("DAGGERHEART CHARACTER SHEET", 14, 248, 131, 600, true, Muted);

        var traits = Array(root, "traits").Where(item => !string.IsNullOrWhiteSpace(Text(item, "key")))
            .GroupBy(item => Text(item, "key")!.ToLowerInvariant()).ToDictionary(group => group.Key, group => Text(group.First(), "value"));
        string[] traitNames = ["agility", "strength", "finesse", "instinct", "presence", "knowledge"];
        for (var i = 0; i < traitNames.Length; i++)
        {
            var x = 880 + i * 113;
            render.Rect(Ink, x, 38, 104, 27);
            render.Text(traitNames[i].ToUpperInvariant(), 13, x + 4, 43, 96, true, Paper, centered: true);
            render.Shield(x + 22, 66, 60, 67);
            var score = traits.GetValueOrDefault(traitNames[i]);
            render.Text(Signed(score), 25, x + 25, 79, 54, true, Ink, centered: true);
        }

        var stats = Object(root, "stats");
        var trackers = Object(root, "trackers");
        var thresholds = Object(root, "thresholds");
        const int top = 215;
        const int left = 50;
        const int middle = 565;
        const int right = 1080;
        const int panelWidth = 470;
        render.Panel(left, top, panelWidth, 510, "ARMOR & STATS");
        render.Text("EVASION", 15, left + 25, top + 60, 115, true, Muted, true);
        render.Text("ARMOR", 15, left + 175, top + 60, 115, true, Muted, true);
        render.Text("PROFICIENCY", 15, left + 315, top + 60, 130, true, Muted, true);
        render.Text(Text(root, "evasion") ?? Text(stats, "evasion") ?? "—", 42, left + 25, top + 87, 115, true, Ink, true);
        render.Text(Text(stats, "armorScore") ?? "—", 42, left + 175, top + 87, 115, true, Ink, true);
        render.Text(Text(stats, "proficiency") ?? "—", 42, left + 315, top + 87, 130, true, Ink, true);
        render.Rect(Ink, left + 22, top + 160, panelWidth - 44, 47);
        var major = Text(thresholds, "major") ?? Text(stats, "thresholdMajor") ?? "—";
        var severe = Text(thresholds, "severe") ?? Text(stats, "thresholdSevere") ?? "—";
        render.Text($"MINOR     {major}     MAJOR     {severe}     SEVERE", 16, left + 32, top + 173, panelWidth - 64, true, Paper, true);
        DrawTracker(render, trackers, "hp", "HIT POINTS MARKED", left + 25, top + 225);
        DrawTracker(render, trackers, "stress", "STRESS MARKED", left + 25, top + 290);
        DrawTracker(render, trackers, "hope", "HOPE", left + 25, top + 355);
        DrawTracker(render, trackers, "armor_score", "ARMOR SLOTS MARKED", left + 25, top + 420);

        var columns = CreateColumns(root, snapshot.Features);
        var sheet = snapshot.SheetState ?? default;
        var details = new List<Entry>();
        var spellcast = Text(sheet, "spellcastTraitName");
        if (!string.IsNullOrWhiteSpace(spellcast))
        {
            var traitKey = (Text(sheet, "spellcastTraitKey") ?? "").ToLowerInvariant();
            var modifier = traits.TryGetValue(traitKey, out var value) ? $" ({Signed(value)})" : "";
            details.Add(new Entry("Spellcast trait", spellcast + modifier));
        }
        if (int.TryParse(Text(sheet, "scarCount"), out var scars) && scars > 0) details.Add(new Entry("Scars", scars.ToString()));
        AddCard(columns.Middle, "ADDITIONAL DETAILS", details);
        var cards = columns.Left.Concat(columns.Middle).Concat(columns.Right).Select(card => render.Prepare(card, panelWidth)).ToArray();
        var assignments = SheetColumnLayout.Arrange(cards.Select(card => card.Height + 20).ToArray(), statsHeight: 530);
        int[] positions = [top + 530, top, top];
        int[] xPositions = [left, middle, right];
        for (var i = 0; i < cards.Length; i++)
        {
            var column = assignments[i];
            positions[column] = render.Card(cards[i], xPositions[column], positions[column], panelWidth, MaxHeight - 55);
        }
        var bottom = Math.Min(MaxHeight, positions.Max() + 45);
        var footer = render.Truncated ? "HEART OF DAGGERS  •  CHARACTER SNAPSHOT  •  SOME CONTENT OMITTED" : "HEART OF DAGGERS  •  CHARACTER SNAPSHOT";
        render.Text(footer, 14, 52, bottom - 28, 1500, true, SKColor.Parse("A5ABB8"));
        token.ThrowIfCancellationRequested();
        using var subset = new SKBitmap();
        if (!bitmap.ExtractSubset(subset, new SKRectI(0, 0, Width, bottom))) throw new InvalidOperationException("Could not crop the character sheet.");
        using var cropped = SKImage.FromBitmap(subset);
        using var png = cropped.Encode(SKEncodedImageFormat.Png, 100);
        return new MemoryStream(png.ToArray(), writable: false);
    }

    private static void DrawTracker(Painter render, JsonElement trackers, string key, string label, int x, int y)
    {
        var tracker = Object(trackers, key);
        var value = Text(tracker, "value") ?? "—";
        var max = Text(tracker, "max") ?? "—";
        render.Text(label, 15, x, y, 200, true, Muted);
        render.Text($"{value} / {max}", 21, x + 318, y - 3, 95, true, Ink, true);
        if (int.TryParse(max, out var slots) && int.TryParse(value, out var filled))
        {
            for (var i = 0; i < Math.Min(slots, 12); i++)
                render.Diamond(x + 13 + i * 24, y + 40, 9, i < filled ? (key == "hope" ? Gold : Purple) : SKColor.Parse("CDD1D8"));
        }
    }

    private static (List<Card> Left, List<Card> Middle, List<Card> Right) CreateColumns(JsonElement root, IReadOnlyList<CharacterFeature>? features)
    {
        var left = new List<Card>(); var middle = new List<Card>(); var right = new List<Card>();
        AddCard(left, "CONDITIONS", ActiveConditions(root));
        AddCard(left, "EXPERIENCES", Items(root, "experiences"));
        if (features is not null)
        {
            AddCard(left, "HOPE FEATURE", features.Where(item => item.Category == "Hope Feature").Select(item => new Entry(Plain(item.Name), Plain(item.Text))));
            AddCard(left, "CLASS", features.Where(item => item.Category == "Class").Select(item => new Entry(Plain(item.Name), Plain(item.Text))));
            AddCard(left, "SUBCLASS", features.Where(item => item.Category == "Subclass").Select(item => new Entry(Plain(item.Name), Plain(item.Text))));
            AddCard(middle, "ANCESTRY & COMMUNITY", features.Where(item => item.Category == "Ancestry & Community").Select(item => new Entry(Plain(item.Name), Plain(item.Text))));
        }
        var equipment = Object(root, "equipment");
        AddCard(middle, "WEAPONS & ARMOR", Items(equipment, "weapons").Concat(Items(equipment, "armor")));
        AddCard(middle, "FEATURES", Items(root, "features"));
        AddCard(middle, "INVENTORY", Items(root, "inventory"));
        AddCard(right, "DOMAIN CARDS • LOADOUT", Items(Object(root, "domainCards"), "loadout"), accent: true);
        AddCard(right, "DOMAIN CARDS • VAULT", Items(Object(root, "domainCards"), "vault"));
        AddCard(right, "BACKGROUND", Items(root, "background"));
        AddCard(right, "CONNECTIONS", Items(root, "connections"));
        var beast = Object(root, "beastform");
        if (Text(beast, "active") == "true") AddCard(right, "BEASTFORM", [new Entry(Text(beast, "name") ?? "Active", "")]);
        return (left, middle, right);
    }

    private static IEnumerable<Entry> ActiveConditions(JsonElement root)
    {
        var conditions = Object(root, "conditions");
        foreach (var item in Array(conditions, "core").Concat(Array(conditions, "custom")))
            if (Text(item, "active") == "true") yield return new Entry(Text(item, "label") ?? Text(item, "name") ?? "Condition", "");
    }

    private static IEnumerable<Entry> Items(JsonElement root, string key)
    {
        var all = Array(root, key).ToArray();
        foreach (var item in all.Take(50))
        {
            if (item.ValueKind == JsonValueKind.String) { yield return new Entry(item.GetString() ?? "", ""); continue; }
            if (item.ValueKind != JsonValueKind.Object) continue;
            var name = Text(item, "name") ?? Text(item, "label") ?? Text(item, "title") ?? "";
            var info = string.Join("  •  ", new[] { Text(item, "modifier"), Text(item, "trait"), Text(item, "damage"), Text(item, "range"),
                Text(item, "domain"), Prefix("Level ", Text(item, "level")), Prefix("Recall ", Text(item, "recall")) }.Where(value => !string.IsNullOrWhiteSpace(value)));
            var quantity = Text(item, "quantity");
            if (int.TryParse(quantity, out var count) && count > 1) info = JoinNonEmpty("  •  ", info, $"×{count}");
            var description = Text(item, "description") ?? Text(item, "text") ?? "";
            if (description.Length > 6000) description = description[..6000] + " …";
            if (!string.IsNullOrWhiteSpace(name) || !string.IsNullOrWhiteSpace(description))
                yield return new Entry(name, JoinNonEmpty("\n", info, description));
        }
        if (all.Length > 50) yield return new Entry("Additional entries omitted", "");
    }

    private static void AddCard(List<Card> column, string title, IEnumerable<Entry> items, bool accent = false)
    {
        var entries = items.ToArray();
        if (entries.Length > 0) column.Add(new Card(title, entries, accent));
    }
    private static string? Prefix(string prefix, string? value) => string.IsNullOrWhiteSpace(value) ? null : prefix + value;
    private static string JoinNonEmpty(string separator, params string?[] values) => string.Join(separator, values.Where(value => !string.IsNullOrWhiteSpace(value)));
    private static string Signed(string? text) => int.TryParse(text, out var score) ? score.ToString("+0;-0;+0") : text ?? "—";
    private static JsonElement Object(JsonElement root, string key) => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Object ? value : default;
    private static IEnumerable<JsonElement> Array(JsonElement root, string key) => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().ToArray() : [];
    private static string? Text(JsonElement root, string key)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(key, out var value)) return null;
        if (value.ValueKind is not (JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)) return null;
        var raw = value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
        if (raw is null) return null;
        return Plain(raw);
    }

    private static string Plain(string raw) => WebUtility.HtmlDecode(Regex.Replace(raw, "<[^>]*>", " ", RegexOptions.None, TimeSpan.FromSeconds(1))).Trim();

    private async Task<SKBitmap?> LoadPortraitAsync(string? address, CancellationToken token)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.Port != 443 || uri.UserInfo.Length != 0 ||
            !(uri.Host.Equals("heartofdaggers.com", StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".cloudfront.net", StringComparison.OrdinalIgnoreCase))) return null;
        try
        {
            using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > 4_000_000) return null;
            await using var input = await response.Content.ReadAsStreamAsync(token);
            using var buffer = new MemoryStream();
            var chunk = new byte[16384]; int read;
            while ((read = await input.ReadAsync(chunk, token)) > 0)
            {
                if (buffer.Length + read > 4_000_000) return null;
                buffer.Write(chunk, 0, read);
            }
            using var data = SKData.CreateCopy(buffer.ToArray());
            using var codec = SKCodec.Create(data);
            if (codec is null || codec.Info.Width > 4096 || codec.Info.Height > 4096 || codec.Info.Width <= 0 || codec.Info.Height <= 0) return null;
            return SKBitmap.Decode(codec);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return null; }
    }

    private static SKTypeface LoadTypeface()
    {
        using var stream = typeof(CharacterSheetRenderer).Assembly.GetManifestResourceStream("DndSharp.Discord.Assets.Lato-Regular.ttf")
            ?? throw new InvalidOperationException("The bundled font is missing.");
        using var data = SKData.Create(stream);
        return SKTypeface.FromData(data) ?? throw new InvalidOperationException("The bundled font could not be loaded.");
    }

    private sealed record Entry(string Name, string Details);
    private sealed record Card(string Title, Entry[] Entries, bool Accent);
    private sealed record PreparedCard(Card Card, IReadOnlyList<(string Text, bool Bold)> Lines)
    {
        public int Height => 60 + Lines.Count * 23;
    }

    private sealed class Painter(SKCanvas canvas, SKPaint paint, SKTypeface typeface)
    {
        public bool Truncated { get; private set; }
        public void Rect(SKColor color, float x, float y, float w, float h) { paint.Color = color; paint.Style = SKPaintStyle.Fill; canvas.DrawRect(x, y, w, h, paint); }
        public void Circle(SKColor color, float x, float y, float radius) { paint.Color = color; paint.Style = SKPaintStyle.Fill; canvas.DrawCircle(x, y, radius, paint); }
        public void Text(string text, float size, float x, float y, float width, bool bold, SKColor color, bool centered = false)
        {
            using var font = new SKFont(typeface, size) { Embolden = bold };
            if (font.MeasureText(text) > width) font.Size *= width / font.MeasureText(text);
            paint.Color = color; paint.Style = SKPaintStyle.Fill;
            canvas.DrawText(text, centered ? x + width / 2 : x, y - font.Metrics.Ascent, centered ? SKTextAlign.Center : SKTextAlign.Left, font, paint);
        }
        public void Diamond(float x, float y, float radius, SKColor color)
        {
            using var builder = new SKPathBuilder(); builder.MoveTo(x, y - radius); builder.LineTo(x + radius, y); builder.LineTo(x, y + radius); builder.LineTo(x - radius, y); builder.Close(); using var path = builder.Detach();
            paint.Color = color; paint.Style = SKPaintStyle.Fill; canvas.DrawPath(path, paint);
            paint.Color = SKColor.Parse("8C8F9A"); paint.Style = SKPaintStyle.Stroke; paint.StrokeWidth = 1; canvas.DrawPath(path, paint); paint.Style = SKPaintStyle.Fill;
        }
        public void Shield(float x, float y, float w, float h)
        {
            using var builder = new SKPathBuilder(); builder.MoveTo(x, y); builder.LineTo(x + w, y); builder.LineTo(x + w - 4, y + h * .75f); builder.LineTo(x + w / 2, y + h); builder.LineTo(x + 4, y + h * .75f); builder.Close(); using var path = builder.Detach();
            paint.Color = SKColor.Parse("F8F9FB"); paint.Style = SKPaintStyle.Fill; canvas.DrawPath(path, paint);
            paint.Color = SKColor.Parse("9CA6B5"); paint.Style = SKPaintStyle.Stroke; paint.StrokeWidth = 3; canvas.DrawPath(path, paint); paint.Style = SKPaintStyle.Fill;
        }
        public void Panel(float x, float y, float width, float height, string title)
        {
            Rect(Paper, x, y, width, height); Rect(Header, x, y, width, 40);
            Rect(SKColor.Parse("AAB4C0"), x, y + 39, width, 2);
            Text(title, 16, x + 15, y + 10, width - 30, true, Ink, true);
        }
        public PreparedCard Prepare(Card card, int width)
        {
            var lines = new List<(string Text, bool Bold)>();
            foreach (var entry in card.Entries)
            {
                if (!string.IsNullOrWhiteSpace(entry.Name)) foreach (var line in Wrap(entry.Name, width - 48, true)) lines.Add((line, true));
                if (!string.IsNullOrWhiteSpace(entry.Details)) foreach (var line in Wrap(entry.Details, width - 48, false)) lines.Add((line, false));
                lines.Add(("", false));
            }
            return new PreparedCard(card, lines);
        }
        public int Card(PreparedCard prepared, int x, int y, int width, int limit)
        {
            if (y >= limit - 100) { Truncated = true; return y; }
            var card = prepared.Card;
            var lines = prepared.Lines;
            var available = Math.Min(prepared.Height, limit - y);
            if (available < 80) { Truncated = true; return y; }
            Panel(x, y, width, available, card.Title);
            if (card.Accent) Rect(SKColor.Parse("8B2C37"), x, y + 40, 5, available - 40);
            var visible = Math.Max(0, (available - 60) / 23);
            for (var i = 0; i < Math.Min(visible, lines.Count); i++)
                Text(lines[i].Text, lines[i].Bold ? 18 : 16, x + 22, y + 51 + i * 23, width - 44, lines[i].Bold, lines[i].Bold ? Ink : Muted);
            if (visible < lines.Count) { Truncated = true; Text("Additional content omitted", 14, x + 22, y + available - 23, width - 44, true, Muted); }
            return y + available + 20;
        }
        private IEnumerable<string> Wrap(string text, int width, bool bold)
        {
            using var font = new SKFont(typeface, bold ? 18 : 16) { Embolden = bold };
            foreach (var paragraph in text.Split('\n'))
            {
                var line = "";
                foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    var candidate = line.Length == 0 ? word : line + " " + word;
                    if (line.Length > 0 && font.MeasureText(candidate) > width) { yield return line; line = word; }
                    else line = candidate;
                }
                if (line.Length > 0) yield return line;
            }
        }
    }
}
