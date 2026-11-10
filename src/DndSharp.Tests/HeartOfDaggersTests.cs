using System.Net;
using System.Text;
using System.Text.Json;
using DndSharp.HeartOfDaggers;

namespace DndSharp.Tests;

[TestClass]
public sealed class HeartOfDaggersTests
{
    [TestMethod]
    public void InvitationParsingRejectsOtherSitesAndAmbiguousTokens()
    {
        var invite = CampaignInvitationService.ParseInvite("https://heartofdaggers.com/campaign/test123/?invite=abc123&other=ignored");
        Assert.AreEqual("test123", invite.Slug);
        Assert.AreEqual("https://heartofdaggers.com/campaign/test123/?invite=abc123", invite.Uri.AbsoluteUri);
        Assert.Throws<ArgumentException>(() => CampaignInvitationService.ParseInvite("https://example.test/campaign/test123/?invite=abc"));
        Assert.Throws<ArgumentException>(() => CampaignInvitationService.ParseInvite("https://user@heartofdaggers.com/campaign/test123/?invite=abc"));
        Assert.Throws<ArgumentException>(() => CampaignInvitationService.ParseInvite("https://heartofdaggers.com/character/test123/?invite=abc"));
        Assert.Throws<ArgumentException>(() => CampaignInvitationService.ParseInvite("https://heartofdaggers.com/campaign/test123/?invite=abc&invite=def"));
    }

    [TestMethod]
    public async Task CampaignAndCharacterReadsPreserveUnrecognizedData()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, """
            { "characters": [{ "id": 12, "name": "Test", "details": { "custom": 42 } }] }
            """);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var client = new HeartOfDaggersClient(http);

        var result = await client.GetCampaignCharactersAsync(42);
        Assert.AreEqual(HttpMethod.Get, handler.Method);
        Assert.AreEqual("/wp-json/hod-owlbear/v1/campaigns/42/characters", handler.Path);
        Assert.AreEqual(42, result.GetProperty("characters")[0].GetProperty("details").GetProperty("custom").GetInt32());

        await client.GetCharacterAsync(12);
        Assert.AreEqual("/wp-json/hod-owlbear/v1/characters/12", handler.Path);
        await client.GetCampaignsAsync();
        Assert.AreEqual("/wp-json/hod-owlbear/v1/campaigns", handler.Path);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.GetCharacterAsync(0));
    }

    [TestMethod]
    public async Task AuthenticationErrorsDoNotExposeServerResponse()
    {
        var handler = new FakeHandler(HttpStatusCode.Unauthorized, "secret response");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new HeartOfDaggersClient(http).GetCampaignsAsync());
        Assert.IsFalse(error.Message.Contains("secret", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ParsesCampaignStateWithoutExposingCredentials()
    {
        var session = CampaignSession.Parse("https://heartofdaggers.com/campaign/test123/#players", CampaignHtml);
        Assert.AreEqual("test123", session.Slug);
        Assert.AreEqual(42L, session.Id);
        CollectionAssert.AreEquivalent(new long[] { 12, 13 }, session.CharacterIds.ToArray());
        CollectionAssert.AreEquivalent(new long[] { 12 }, session.OwnedCharacterIds.ToArray());
        Assert.IsFalse(JsonSerializer.Serialize(session).Contains("test-secret", StringComparison.Ordinal));
        Assert.Throws<ArgumentException>(() => CampaignSession.ParseCampaignSlug("https://example.test/campaign/test123"));
        Assert.Throws<ArgumentException>(() => CampaignSession.ParseCampaignSlug("../test123"));
        Assert.Throws<UnauthorizedAccessException>(() => CampaignSession.Parse("test123", "<html>Sign in</html>"));
        Assert.Throws<FormatException>(() => CampaignSession.Parse("test123", CampaignHtml.Replace("realtime.heartofdaggers.com", "example.test")));
    }

    [TestMethod]
    public void CampaignNameComesFromThePageHeading()
    {
        var session = CampaignSession.Parse("test123", "<h1>Daggers &amp; Dragons</h1>" + CampaignHtml);
        Assert.AreEqual("Daggers & Dragons", session.Name);
        Assert.AreEqual("test123", CampaignSession.Parse("test123", CampaignHtml).Name);
    }

    [TestMethod]
    public async Task ReadsCampaignCharacterFromItsCurrentSheet()
    {
        const string profile = """
            <div class="dh-campaign-character-shell">
              <a class="dh-campaign-character-profile__name" href="https://heartofdaggers.com/character/hero/">Hero</a>
              <button data-dh-character-details-source="dh-campaign-character-details-12-42" data-character-name="Hero &amp; Friend"></button>
            </div>
            """;
        var campaign = CampaignSession.Parse("test123", CampaignHtml + profile);
        Assert.AreEqual("Hero & Friend", campaign.CharacterName(12));
        using var http = new HttpClient(new SheetHandler()) { BaseAddress = new Uri("https://heartofdaggers.com/") };
        var character = await new HeartOfDaggersClient(http).GetCharacterDetailsAsync(campaign, 12);
        Assert.AreEqual("Hero & Friend", character.Character.GetProperty("name").GetString());
        Assert.AreEqual(2, character.Character.GetProperty("traits")[0].GetProperty("value").GetInt32());
        Assert.AreEqual("3", character.Character.GetProperty("trackers").GetProperty("hp").GetProperty("value").GetString());
        Assert.AreEqual("Sword", character.Character.GetProperty("equipment").GetProperty("weapons")[0].GetProperty("name").GetString());
        Assert.AreEqual("Torch", character.Inventory!.Items[0].Name);
        Assert.AreEqual("Vampire", character.SheetState!.Value.GetProperty("transformationName").GetString());
        Assert.IsFalse(JsonSerializer.Serialize(character).Contains("testsecret", StringComparison.Ordinal));

        var malicious = CampaignSession.Parse("test123", CampaignHtml + profile.Replace("https://heartofdaggers.com/character/hero/", "https://attacker.example/character/hero/"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new HeartOfDaggersClient(http).GetCharacterDetailsAsync(malicious, 12));
        await Assert.ThrowsAsync<ArgumentException>(() => new HeartOfDaggersClient(http).GetCharacterDetailsAsync(campaign, 999));
    }

    [TestMethod]
    public void ExtractsOnlyPublicSheetFeatureText()
    {
        const string html = """
            <div class="panel dhc-feature-panel" data-acc-title="Class">
              <button data-dhc-json="{&quot;hope_feat_name&quot;:&quot;Hope&quot;,&quot;hope_feat_text&quot;:&quot;Hope detail&quot;,&quot;class_feats&quot;:[{&quot;name&quot;:&quot;Class feature&quot;,&quot;text&quot;:&quot;Class detail&quot;}],&quot;secret&quot;:&quot;secret&quot;}"></button>
              <li data-dhc-feature-actions data-dhc-json="{&quot;name&quot;:&quot;Feature&quot;,&quot;text&quot;:&quot;Details&quot;,&quot;nonce&quot;:&quot;secret&quot;}"></li>
              <li data-dhc-feature-actions data-dhc-json="bad"></li>
            </div>
            <div class="dhc-feature-panel" data-acc-title="Private Notes">
              <li data-dhc-feature-actions data-dhc-json="{&quot;name&quot;:&quot;Private&quot;,&quot;text&quot;:&quot;Secret&quot;}"></li>
            </div>
            """;
        var features = HeartOfDaggersClient.ParseFeatures(html);
        Assert.AreEqual(3, features.Count);
        CollectionAssert.Contains(features.ToArray(), new CharacterFeature("Class", "Feature", "Details"));
        CollectionAssert.Contains(features.ToArray(), new CharacterFeature("Hope Feature", "Hope", "Hope detail"));
        Assert.IsFalse(JsonSerializer.Serialize(features).Contains("secret", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void ParsesInventoryItemsQuantitiesDescriptionsAndGold()
    {
        using var json = JsonDocument.Parse("""
            { "success": true, "data": {
              "fixed2": [{"t":"custom","text":"Torch","q":2},
                         {"t":"core","key":"consumable:potion","q":3}],
              "core_items": {"consumable:potion":{"name":"Potion","data":{"description":"Clear stress."}}},
              "gold_coins":4,"gold_handfuls":1,"gold_bags":0,"gold_chests":0
            }}
            """);
        var inventory = HeartOfDaggersClient.ParseInventory(json.RootElement)!;
        Assert.AreEqual(2, inventory.Items.Count);
        Assert.AreEqual(new CharacterInventoryItem("Torch", "", 2), inventory.Items[0]);
        Assert.AreEqual(new CharacterInventoryItem("Potion", "Clear stress.", 3), inventory.Items[1]);
        Assert.AreEqual(4, inventory.Coins);
        Assert.AreEqual(1, inventory.Handfuls);
    }

    [TestMethod]
    public void ExtractsTransformationDescriptionAndAbilities()
    {
        const string html = """
            <div class="panel-card" data-acc-title="Transformation">
              <button data-dhc-json="{&quot;name&quot;:&quot;Vampire&quot;,&quot;description&quot;:&quot;Undead&quot;,&quot;feats&quot;:[{&quot;name&quot;:&quot;Fangs&quot;,&quot;text&quot;:&quot;Bite&quot;}],&quot;nonce&quot;:&quot;secret&quot;}"></button>
            </div>
            """;
        var features = HeartOfDaggersClient.ParseFeatures(html);
        CollectionAssert.Contains(features.ToArray(), new CharacterFeature("Transformation", "Vampire", "Undead"));
        CollectionAssert.Contains(features.ToArray(), new CharacterFeature("Transformation", "Fangs", "Bite"));
        Assert.IsFalse(JsonSerializer.Serialize(features).Contains("secret", StringComparison.Ordinal));
    }

    [TestMethod]
    public void HistoryTrackerSkipsBaselineDuplicatesAndNonHistoryEvents()
    {
        var tracker = new CampaignHistoryTracker(42);
        using var document = JsonDocument.Parse("""
          [
            { "id":"old", "notation":"d6=4" },
            { "id":"new", "notation":"d6=6" },
            { "id":"inventory-1", "type":"inventory_sync" },
            { "id":"clear-1", "type":"spotlight", "action":"cleared" },
            { "id":"spotlight-1", "type":"spotlight" },
            { "notation":"legacy roll" }
          ]
          """);
        var entries = document.RootElement;
        tracker.Seed(new[] { entries[0] });
        Assert.IsTrue(tracker.HasBaseline);
        Assert.IsFalse(tracker.TryObserve(entries[0], out _));
        Assert.IsTrue(tracker.TryObserve(entries[1], out var item));
        Assert.AreEqual("new", item!.Id);
        Assert.AreEqual(42L, item.CampaignId);
        Assert.IsFalse(tracker.TryObserve(entries[1], out _));
        Assert.IsFalse(tracker.TryObserve(entries[2], out _));
        Assert.IsFalse(tracker.TryObserve(entries[3], out _));
        Assert.IsTrue(tracker.TryObserve(entries[4], out _));
        Assert.IsTrue(tracker.TryObserve(entries[5], out _));
        Assert.IsFalse(tracker.TryObserve(entries[5], out _));
    }

    [TestMethod]
    public async Task ReadsCampaignHistoryWithTheSiteReadOnlyAction()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, """{"success":true,"data":{"history":[{"id":"one"}]}}""");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var client = new HeartOfDaggersClient(http);
        var session = CampaignSession.Parse("test123", CampaignHtml);
        var history = await client.GetCampaignHistoryAsync(session);
        Assert.AreEqual(HttpMethod.Post, handler.Method);
        Assert.AreEqual("/wp-admin/admin-ajax.php", handler.Path);
        StringAssert.Contains(handler.Body!, "action=dh_campaign_dice_history_get");
        StringAssert.Contains(handler.Body!, "campaign_id=42");
        Assert.AreEqual("one", history[0].GetProperty("id").GetString());
    }

    [TestMethod]
    public async Task CharacterSnapshotIncludesSheetStateWithoutCredentials()
    {
        var handler = new CharacterHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://heartofdaggers.com/") };
        var characters = await new HeartOfDaggersClient(http).GetCampaignCharacterDetailsAsync(42);
        Assert.AreEqual(1, characters.Count);
        var result = characters[0];
        Assert.AreEqual("Test", result.Character.GetProperty("name").GetString());
        Assert.AreEqual(2, result.SheetState!.Value.GetProperty("strengthMod").GetInt32());
        Assert.IsFalse(result.SheetState.Value.TryGetProperty("nonce", out _));
        Assert.IsFalse(result.SheetState.Value.GetProperty("nested").TryGetProperty("authToken", out _));
        Assert.AreEqual("Torch", result.Inventory!.Items[0].Name);
        StringAssert.Contains(handler.InventoryRequest!, "action=dhc_inventory_get");
        StringAssert.Contains(handler.InventoryRequest!, "post_id=12");
        Assert.IsFalse(JsonSerializer.Serialize(result).Contains("secret", StringComparison.Ordinal));
    }

    private const string CampaignHtml = """
        <script>let tmp = window.DH_CAMPAIGN_STATE || (window.DH_CAMPAIGN_STATE = {});</script>
        <script>window.DH_CAMPAIGN_STATE = {
          "campaignId":42, "currentUserId":99, "campaignAccessMode":"gm",
          "campaignSocketAuth":"test-secret", "nonce":"test-nonce",
          "socketUrl":"https://realtime.heartofdaggers.com",
          "campaignCharacterIds":{"a":[12,"13",12]}, "campaignOwnedCharacterIds":[12]
        };</script>
        """;

    private sealed class FakeHandler(HttpStatusCode status, string content) : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }
        public string? Path { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Method = request.Method;
            Path = request.RequestUri!.AbsolutePath;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(token);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class CharacterHandler : HttpMessageHandler
    {
        public string? InventoryRequest { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (request.RequestUri!.AbsolutePath == "/wp-admin/admin-ajax.php")
            {
                InventoryRequest = await request.Content!.ReadAsStringAsync(token);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("""{"success":true,"data":{"fixed2":[{"t":"custom","text":"Torch","q":1}]}}""") };
            }
            var body = request.RequestUri!.AbsolutePath switch
            {
                "/wp-json/hod-owlbear/v1/campaigns/42/characters" => """{"characters":[{"id":12}]}""",
                "/wp-json/hod-owlbear/v1/characters/12" => """{"character":{"id":12,"name":"Test","fullSheetUrl":"https://heartofdaggers.com/character/test-1/"}}""",
                "/character/test-1/" => """<script>window.DH_CHARACTER_STATE = {};</script><script>window.DH_CHARACTER_STATE = {"strengthMod":2,"nonce":"secret","nested":{"authToken":"secret"}}; window.DHC_INV = {nonce:'secret',postId:12};</script>""",
                _ => throw new InvalidOperationException("Unexpected request.")
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8)
            };
        }
    }

    private sealed class SheetHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var body = request.RequestUri!.AbsolutePath switch
            {
                "/character/hero/" => """
                    <script>window.DH_CHARACTER_STATE = {"postId":12,"postTitle":"Hero","level":1,"agilityMod":2,"transformationName":"Vampire","nonce":"testsecret"};
                    window.DHC_INV = {nonce:'testsecret',postId:12};</script>
                    <div data-dhc-label="Evasion" data-dhc-base="12"></div>
                    <div data-field="hp" data-value="3" data-max="6"></div>
                    <div data-dhc-json="{&quot;kind&quot;:&quot;weapon&quot;,&quot;name&quot;:&quot;Sword&quot;}"></div>
                    """,
                "/wp-admin/admin-ajax.php" => """{"success":true,"data":{"fixed2":[{"t":"custom","text":"Torch","q":1}]}}""",
                _ => throw new InvalidOperationException("Unexpected request.")
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8) });
        }
    }
}
