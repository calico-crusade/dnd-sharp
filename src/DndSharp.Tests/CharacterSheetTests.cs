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
        Assert.IsTrue(image.Height >= 900);
        Assert.AreNotEqual(image.GetPixel(0, 0), image.GetPixel(0, 200));
        Assert.AreEqual(0, handler.Calls);
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
