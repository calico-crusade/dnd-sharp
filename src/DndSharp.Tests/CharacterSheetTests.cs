using System.Net;
using System.Text.Json;
using DndSharp.Discord;
using DndSharp.HeartOfDaggers;
using SkiaSharp;

namespace DndSharp.Tests;

[TestClass]
public sealed class CharacterSheetTests
{
    [TestMethod]
    public void CardLayoutFindsTheShortestArrangementEvenWhenGreedyDoesNot()
    {
        int[] heights = [5, 3, 3, 2, 2, 2];
        var columns = SheetColumnLayout.Arrange(heights, 0);
        var totals = new int[3];
        for (var i = 0; i < heights.Length; i++) totals[columns[i]] += heights[i];
        Assert.AreEqual(6, totals.Max());
    }

    [TestMethod]
    public void CardLayoutMatchesExhaustiveSearchWithAFixedStatsPanel()
    {
        var random = new Random(42);
        for (var sample = 0; sample < 50; sample++)
        {
            var heights = Enumerable.Range(0, 7).Select(_ => random.Next(1, 30)).ToArray();
            var statsHeight = random.Next(0, 40);
            var assignment = SheetColumnLayout.Arrange(heights, statsHeight);
            long[] actual = [statsHeight, 0, 0];
            for (var i = 0; i < heights.Length; i++) actual[assignment[i]] += heights[i];

            long minimum = long.MaxValue;
            void Search(int index, long[] totals)
            {
                if (index == heights.Length) { minimum = Math.Min(minimum, totals.Max()); return; }
                for (var column = 0; column < 3; column++)
                {
                    totals[column] += heights[index];
                    Search(index + 1, totals);
                    totals[column] -= heights[index];
                }
            }
            Search(0, [statsHeight, 0, 0]);
            Assert.AreEqual(minimum, actual.Max());
        }
    }

    [TestMethod]
    public async Task GeneratesReadablePngAndRejectsUntrustedPortraitHosts()
    {
        using var json = JsonDocument.Parse("""
            {"name":"Hero &amp; Friend","level":3,"portraitUrl":"https://example.test/private",
             "traits":[{"key":"agility","value":1},{"key":"agility","value":2}],
             "trackers":{"hp":{"value":1,"max":5}},
             "equipment":{"weapons":[{"name":"Sword","damage":"d8+2"}]},
             "domainCards":{"loadout":[{"name":"A card","description":"Some rules text."}]}}
            """);
        var handler = new RejectingHandler();
        using var http = new HttpClient(handler);
        var snapshot = new CharacterSnapshot(json.RootElement.Clone(), null, [new CharacterFeature("Class", "A feature", "A description")]);
        await using var stream = await new CharacterSheetRenderer(http).RenderAsync(snapshot);
        using var image = SKBitmap.Decode(stream);
        Assert.IsNotNull(image);
        Assert.AreEqual(1600, image.Width);
        Assert.AreEqual(790, image.Height, "Short cards should fit alongside the fixed stats panel.");
        Assert.AreNotEqual(image.GetPixel(0, 0), image.GetPixel(0, 200));
        Assert.AreEqual(0, handler.Calls);
    }

    [TestMethod]
    public async Task SheetThemesRenderDifferentPalettesAndRejectUnknownThemes()
    {
        using var json = JsonDocument.Parse("""{"name":"Hero","trackers":{},"traits":[]}""");
        using var http = new HttpClient(new RejectingHandler());
        var renderer = new CharacterSheetRenderer(http);
        var snapshot = new CharacterSnapshot(json.RootElement.Clone(), null);
        await using var lightStream = await renderer.RenderAsync(snapshot, "light");
        await using var darkStream = await renderer.RenderAsync(snapshot, "dark");
        await using var princessStream = await renderer.RenderAsync(snapshot, "princess");
        using var light = SKBitmap.Decode(lightStream);
        using var dark = SKBitmap.Decode(darkStream);
        using var princess = SKBitmap.Decode(princessStream);
        Assert.AreEqual(CharacterSheetThemes.Find("light")!.Paper, light.GetPixel(55, 270));
        Assert.AreEqual(CharacterSheetThemes.Find("dark")!.Paper, dark.GetPixel(55, 270));
        Assert.AreNotEqual(light.GetPixel(55, 270), dark.GetPixel(55, 270));
        Assert.AreEqual(CharacterSheetThemes.Find("princess")!.Paper, princess.GetPixel(55, 270));
        Assert.AreNotEqual(dark.GetPixel(55, 270), princess.GetPixel(55, 270));
        await Assert.ThrowsAsync<ArgumentException>(() => renderer.RenderAsync(snapshot, "unknown"));
    }

    private sealed class RejectingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
        }
    }
}
