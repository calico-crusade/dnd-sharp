using DndSharp.Discord;
using DndSharp.Database;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace DndSharp.Tests;

[TestClass]
public sealed class CampaignSubscriptionTests
{
    [TestMethod]
    public void ResolvesStaticAndAnimatedServerEmojiWithoutChangingUnicodeOrMarkup()
    {
        ServerEmoji[] emotes =
        [
            new("HeadPat", 1535724617514360922, false),
            new("dance", 123456789012345678, true),
            new("external", 987654321098765432, false)
        ];
        var resolved = ServerEmojiResolver.Resolve("✅ :headpat: :DANCE: 🎉 <:external:987654321098765432>", emotes, strict: true);
        Assert.AreEqual("✅ <:HeadPat:1535724617514360922> <a:dance:123456789012345678> 🎉 <:external:987654321098765432>", resolved);
        Assert.AreEqual(resolved, ServerEmojiResolver.Resolve(resolved, emotes, strict: true));
    }

    [TestMethod]
    public void ExplainsUnknownOrAmbiguousEmojiAndPreservesLegacyText()
    {
        ServerEmoji[] emotes = [new("same", 1, false), new("Same", 2, true), new("disabled", 3, false, false)];
        var missing = Assert.Throws<EmojiResolutionException>(() => ServerEmojiResolver.Resolve(":missing:", emotes, strict: true));
        StringAssert.Contains(missing.Message, "No accessible emoji");
        Assert.Throws<EmojiResolutionException>(() => ServerEmojiResolver.Resolve(":disabled:", emotes, strict: true));
        Assert.Throws<EmojiResolutionException>(() => ServerEmojiResolver.Resolve("<:disabled:3>", emotes, strict: true));
        var ambiguous = Assert.Throws<EmojiResolutionException>(() => ServerEmojiResolver.Resolve(":same:", emotes, strict: true));
        StringAssert.Contains(ambiguous.Message, "More than one");
        Assert.AreEqual(":missing:", ServerEmojiResolver.Resolve(":missing:", emotes, strict: false));
    }

    [TestMethod]
    public void RejectsInaccessibleMarkupAndDistinguishesExternalFromApplicationEmoji()
    {
        var inaccessible = Assert.Throws<EmojiResolutionException>(() =>
            ServerEmojiResolver.Resolve("<:HeadPat:1535724617514360922>", [], strict: true));
        StringAssert.Contains(inaccessible.Message, "emoji's server");

        ServerEmoji[] emotes =
        [
            new("HeadPat", 1535724617514360922, false, GuildId: 456),
            new("app_emoji", 876543210987654321, false, IsApplication: true),
            new("local", 111111111111111111, false, GuildId: 123)
        ];
        var external = ServerEmojiResolver.Resolve("<:headpat:1535724617514360922>", emotes, strict: true);
        Assert.AreEqual("<:HeadPat:1535724617514360922>", external);
        Assert.IsTrue(ServerEmojiResolver.UsesExternalGuildEmoji(external, emotes, 123));
        Assert.IsFalse(ServerEmojiResolver.UsesExternalGuildEmoji("<:app_emoji:876543210987654321>", emotes, 123));
        Assert.IsFalse(ServerEmojiResolver.UsesExternalGuildEmoji("<:local:111111111111111111>", emotes, 123));
    }

    [TestMethod]
    public void PreviouslySavedShortcodeResolvesWhenFormattingARoll()
    {
        using var roll = JsonDocument.Parse("""{"actorName":"Test","total":15,"duality":{"fear":3,"hope":9}}""");
        var settings = new DualityDisplaySettings(SuccessHope: ":headpat:", Difficulty: 12);
        var output = CampaignSubscriptions.Format(roll.RootElement, settings, null, [new ServerEmoji("HeadPat", 1535724617514360922, false)]);
        StringAssert.StartsWith(output, "<:HeadPat:1535724617514360922>");
    }

    [TestMethod]
    public void ResolvedMarkupRemainsUsableWhenStoredAndFormatted()
    {
        var text = ServerEmojiResolver.Resolve("✅ :headpat:", [new ServerEmoji("HeadPat", 1535724617514360922, false)], strict: true);
        var settings = DualityDisplay.SetEmoji(new DualityDisplaySettings(Difficulty: 12), DualityOutcome.SuccessHope, text);
        var saved = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<DualityDisplaySettings>(saved);
        using var roll = JsonDocument.Parse("""{"actorName":"Test","total":15,"duality":{"fear":3,"hope":9}}""");
        StringAssert.StartsWith(CampaignSubscriptions.Format(roll.RootElement, restored), "✅ <:HeadPat:1535724617514360922>");
        var multiple = ServerEmojiResolver.Resolve(string.Concat(Enumerable.Repeat(":headpat:", 4)),
            [new ServerEmoji("HeadPat", 1535724617514360922, false)], strict: true);
        Assert.IsTrue(multiple.Length > 100);
        Assert.AreEqual(multiple, DualityDisplay.SetEmoji(settings, DualityOutcome.Critical, multiple).Critical);
    }

    [TestMethod]
    public void CampaignPickerStateExpiresAndCanOnlyBeConsumedOnce()
    {
        var state = new CampaignPickerState();
        var request = new CampaignPickerRequest(123, 456, 789, CampaignPickerAction.Emoji, DateTimeOffset.UtcNow.AddMinutes(1), DualityOutcome.Critical, "🎉");
        var id = state.Add(request);
        Assert.AreEqual(request, state.Get(id));
        Assert.IsTrue(state.Take(id, out var taken));
        Assert.AreEqual(request, taken);
        Assert.IsFalse(state.Take(id, out _));
        var expired = state.Add(request with
        {
            Expires = DateTimeOffset.UtcNow.AddSeconds(-1)
        });
        Assert.IsNull(state.Get(expired));
        Assert.IsFalse(state.Take(expired, out _));
    }

    [TestMethod]
    public void DualityShowsOutcomeOnlyWhenDifficultyIsKnown()
    {
        static string Format(int fear, int hope, int total, DualityDisplaySettings settings)
        {
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                actorName = "Test",
                total,
                duality = new
                {
                    fear,
                    hope
                }
            }));
            return CampaignSubscriptions.Format(json.RootElement, settings);
        }
        var settings = new DualityDisplaySettings(
            SuccessFear: "✅ 💀",
            SuccessHope: "✅ ✨",
            FailureFear: "❌ 💀",
            FailureHope: "❌ ✨",
            Critical: "🎉🎉🎉",
            Fear: "💀",
            Hope: "✨",
            Difficulty: 12);
        StringAssert.StartsWith(Format(9, 3, 12, settings), "✅ 💀");
        StringAssert.StartsWith(Format(3, 9, 12, settings), "✅ ✨");
        StringAssert.StartsWith(Format(8, 2, 10, settings), "❌ 💀");
        StringAssert.StartsWith(Format(2, 8, 10, settings), "❌ ✨");
        StringAssert.StartsWith(Format(2, 2, 4, settings), "🎉🎉🎉");
        StringAssert.StartsWith(Format(8, 2, 10, settings with
        {
            Difficulty = null
        }), "💀");
        StringAssert.StartsWith(Format(2, 8, 10, settings with
        {
            Difficulty = null
        }), "✨");
        StringAssert.StartsWith(Format(2, 8, 10, settings with
        {
            Enabled = false
        }), "🎲");
        StringAssert.StartsWith(Format(2, 2, 4, settings with
        {
            Critical = "🏆"
        }), "🏆");
    }

    [TestMethod]
    public void ConfiguredDualityTextIsValidated()
    {
        var settings = DualityDisplay.SetEmoji(new DualityDisplaySettings(), DualityOutcome.SuccessFear, "✅ 🐉");
        Assert.AreEqual("✅ 🐉", settings.SuccessFear);
        Assert.Throws<ArgumentException>(() => DualityDisplay.SetEmoji(settings, DualityOutcome.Fear, "bad\ntext"));
        using var malformed = JsonDocument.Parse("""{"duality":{"fear":"?","hope":3}}""");
        Assert.IsNull(DualityDisplay.Prefix(malformed.RootElement, settings));
    }

    [TestMethod]
    public void FormatsHistoryWithDecodedHtmlEntities()
    {
        using var history = JsonDocument.Parse("""
            { "actorName": "D&amp;D Sharp Bot", "label": "Duality &amp; Roll", "notation": "2d12 = 7 &#91;Fear 3, Hope 4&#93;" }
            """);
        var message = CampaignSubscriptions.Format(history.RootElement);
        StringAssert.Contains(message, "D&D Sharp Bot");
        StringAssert.Contains(message, "Duality & Roll");
        StringAssert.Contains(message, "2d12 = 7 [Fear 3, Hope 4]");
        Assert.IsFalse(message.Contains("&amp;", StringComparison.Ordinal));
    }

    [TestMethod]
    public void LabelsTraitRollDiceAndPreservesModifiers()
    {
        using var history = JsonDocument.Parse("""
            { "actorName":"Hero", "label":"Knowledge Roll", "notation":"8 with Fear [5, 2, +1 Base]",
              "duality":{"fear":5,"hope":2}, "total":8 }
            """);
        var message = CampaignSubscriptions.Format(history.RootElement, new DualityDisplaySettings(Enabled: false));
        StringAssert.Contains(message, "Knowledge Roll\n2d12+1 = 8 With Fear [Fear 5, Hope 2, +1 Base]");
    }

    [TestMethod]
    public void TraitRollNotationCombinesModifiersAndKeepsTheirBreakdown()
    {
        using var roll = JsonDocument.Parse("""
            { "label":"Knowledge Roll", "notation":"23 with Hope [8, 12, +1 Base, +2 Something]",
              "duality":{"fear":8,"hope":12}, "total":23 }
            """);
        StringAssert.Contains(CampaignSubscriptions.Format(roll.RootElement),
            "2d12+3 = 23 With Hope [Fear 8, Hope 12, +1 Base, +2 Something]");

        using var negative = JsonDocument.Parse("""
            { "notation":"6 with Fear [5, 2, -3 Base, +2 Experience]",
              "duality":{"fear":5,"hope":2}, "total":6 }
            """);
        StringAssert.Contains(CampaignSubscriptions.Format(negative.RootElement),
            "2d12-1 = 6 With Fear [Fear 5, Hope 2, -3 Base, +2 Experience]");

        using var unknownBonus = JsonDocument.Parse("""
            { "notation":"12 with Fear [5, 2, 5 bonus die]",
              "duality":{"fear":5,"hope":2}, "total":12 }
            """);
        StringAssert.Contains(CampaignSubscriptions.Format(unknownBonus.RootElement),
            "12 with Fear [Fear 5, Hope 2, 5 bonus die]");
    }

    [TestMethod]
    public void ManualDualityRollIncludesItsOutcomeWithoutDuplicatingIt()
    {
        using var manual = JsonDocument.Parse("""
            { "actorName":"Hero", "label":"Duality Roll", "notation":"2d12 = 14 [Fear 11, Hope 3]",
              "rollKind":"duality", "duality":{"fear":11,"hope":3}, "total":14 }
            """);
        StringAssert.Contains(CampaignSubscriptions.Format(manual.RootElement),
            "Duality Roll\n2d12 = 14 With Fear [Fear 11, Hope 3]");

        using var existing = JsonDocument.Parse("""
            { "notation":"2d12 = 14 With Fear [Fear 11, Hope 3]", "duality":{"fear":11,"hope":3} }
            """);
        StringAssert.Contains(CampaignSubscriptions.Format(existing.RootElement),
            "2d12 = 14 With Fear [Fear 11, Hope 3]");

        using var critical = JsonDocument.Parse("""
            { "notation":"10 Critical [5, 5]", "duality":{"fear":5,"hope":5}, "total":10 }
            """);
        StringAssert.Contains(CampaignSubscriptions.Format(critical.RootElement),
            "2d12 = 10 Critical [Fear 5, Hope 5]");
    }

    [TestMethod]
    public void ReactionRollIncludesAdvantageDieAndSeparateModifiers()
    {
        using var reaction = JsonDocument.Parse("""
            { "actorName":"Hero", "label":"Duality Reaction Roll", "notation":"24 [2, 7, +1, +5 Experiences, +9 Manual]",
              "baseNotation":"2d12", "total":24, "rollKind":"reaction", "reactionRoll":true,
              "rolls":[2,7,1], "groups":[{"sides":12,"qty":1},{"sides":12,"qty":1},{"sides":6,"qty":1}],
              "advMode":"adv", "advDie":1, "bonusBreakdown":{"base":0,"experiences":5,"manual":9} }
            """);
        var output = CampaignSubscriptions.Format(reaction.RootElement);
        StringAssert.StartsWith(output, "🎲 **Hero** — Duality Reaction Roll");
        StringAssert.Contains(output, "2d12+1d6+14 = 24 [Fear 2, Hope 7, +1 Advantage, +5 Experiences, +9 Manual]");
        Assert.IsFalse(output.Contains("With Hope", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void ReactionRollHandlesDisadvantageAndDoesNotClaimHopeOrFearOutcome()
    {
        using var reaction = JsonDocument.Parse("""
            { "label":"Duality Reaction Roll", "notation":"10 [8, 4, -3, +1 Base]", "total":10,
              "reactionRoll":true, "duality":{"fear":8,"hope":4}, "rolls":[8,4,3],
              "groups":[{"sides":12,"qty":2},{"sides":6,"qty":1}], "advMode":"dis", "advDie":3 }
            """);
        var output = CampaignSubscriptions.Format(reaction.RootElement, new DualityDisplaySettings(Difficulty: 8));
        StringAssert.StartsWith(output, "🎲");
        StringAssert.Contains(output, "2d12-1d6+1 = 10 [Fear 8, Hope 4, -3 Disadvantage, +1 Base]");

        using var normal = JsonDocument.Parse("""
            { "label":"Reaction Roll", "notation":"10 [5, 5]", "total":10, "rollKind":"reaction",
              "duality":{"fear":5,"hope":5}, "rolls":[5,5], "groups":[{"sides":12,"qty":2}], "advMode":"normal" }
            """);
        var critical = CampaignSubscriptions.Format(normal.RootElement, new DualityDisplaySettings(Critical: "🎉"));
        StringAssert.StartsWith(critical, "🎉");
        StringAssert.Contains(critical, "2d12 = 10 [Fear 5, Hope 5]");
    }

    [TestMethod]
    public void ReactionRollKeepsOriginalNotationIfDiceDetailsDoNotMatch()
    {
        using var reaction = JsonDocument.Parse("""
            { "label":"Reaction Roll", "notation":"24 [2, 7, +1, +5 Experiences, +9 Manual]", "total":24,
              "rollKind":"reaction", "rolls":[7,2,1], "groups":[{"sides":12,"qty":2},{"sides":6,"qty":1}],
              "advMode":"adv", "advDie":1 }
            """);
        StringAssert.Contains(CampaignSubscriptions.Format(reaction.RootElement),
            "24 [2, 7, +1, +5 Experiences, +9 Manual]");
    }

    [TestMethod]
    public void CustomRollUsesItsBaseNotationOrDiceGroups()
    {
        const string groups = """
            [{"sides":4,"qty":1},{"sides":6,"qty":1},{"sides":8,"qty":1},
             {"sides":10,"qty":1},{"sides":12,"qty":1},{"sides":20,"qty":2}]
            """;
        using var fromGroups = JsonDocument.Parse($$"""
            { "label":"Custom Roll", "rollKind":"quick", "notation":"46 [4, 1, 8, 4, 12, 8, 9]",
              "groups":{{groups}}, "total":46 }
            """);
        StringAssert.Contains(CampaignSubscriptions.Format(fromGroups.RootElement),
            "D4+D6+D8+D10+D12+2D20 = 46 [4, 1, 8, 4, 12, 8, 9]");

        using var withBase = JsonDocument.Parse("""
            { "label":"Custom Roll", "notation":"10 [4, 3]", "baseNotation":"D4+D6+3", "total":10 }
            """);
        StringAssert.Contains(CampaignSubscriptions.Format(withBase.RootElement), "D4+D6+3 = 10 [4, 3]");

        using var alreadyFormatted = JsonDocument.Parse("""
            { "label":"Custom Roll", "notation":"1d4 = 2 [2]", "baseNotation":"1d4", "total":2 }
            """);
        StringAssert.Contains(CampaignSubscriptions.Format(alreadyFormatted.RootElement), "1d4 = 2 [2]");

        using var missing = JsonDocument.Parse("""{"label":"Custom Roll", "notation":"6 [2, 4]"}""");
        StringAssert.Contains(CampaignSubscriptions.Format(missing.RootElement), "6 [2, 4]");
    }

    [TestMethod]
    public void LabelsOnlyMatchingUnlabeledDualityDice()
    {
        string Format(string notation, string duality)
        {
            using var json = JsonDocument.Parse($$"""{"notation":{{JsonSerializer.Serialize(notation)}},"duality":{{duality}}}""");
            return CampaignSubscriptions.Format(json.RootElement, new DualityDisplaySettings(Enabled: false));
        }
        StringAssert.Contains(Format("8 with Fear [2, 5, +1 Base]", """{"fear":5,"hope":2}"""), "[Hope 2, Fear 5, +1 Base]");
        StringAssert.Contains(Format("10 Critical [5, 5]", """{"fear":5,"hope":5}"""), "[Fear 5, Hope 5]");
        StringAssert.Contains(Format("2d12 = 7 [Fear 5, Hope 2]", """{"fear":5,"hope":2}"""), "[Fear 5, Hope 2]");
        StringAssert.Contains(Format("8 [3, 4, +1 Base]", """{"fear":5,"hope":2}"""), "[3, 4, +1 Base]");
        StringAssert.Contains(Format("8 [5, 2, +1 Base]", """{"fear":"?","hope":2}"""), "[5, 2, +1 Base]");
    }

    [TestMethod]
    public async Task MigratesAndPersistsSubscriptionsSettingsAndClaims()
    {
        var prefix = Path.Combine(Path.GetTempPath(), $"dndsharp-subscriptions-{Guid.NewGuid():N}");
        var path = prefix + ".json";
        var database = prefix + ".sqlite3";
        try
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Discord:CampaignSubscriptionsPath"] = path,
                ["Discord:DatabasePath"] = database
            }).Build();
            var item = new CampaignSubscription(123, 456, 789, "test123", new DualityDisplaySettings(Difficulty: 15, Critical: "🏆"));
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new[] { item, item }));
            var store = new CampaignSubscriptionStore(config);
            var loaded = await store.LoadAsync();
            Assert.AreEqual(1, loaded.Count);
            Assert.AreEqual(item, loaded[0]);
            Assert.IsTrue(File.Exists(path), "Migration retains the original file.");
            Assert.IsTrue(await store.ClaimAsync(123, 789, 42, 100));
            Assert.IsFalse(await store.ClaimAsync(123, 789, 42, 101));
            Assert.IsFalse(await store.UnclaimAsync(123, 789, 42, 101));
            await store.SaveAsync([item with { Duality = item.Duality! with { Difficulty = 16 } }]);
            var restarted = new CampaignSubscriptionStore(config);
            Assert.AreEqual(16, (await restarted.LoadAsync())[0].Duality!.Difficulty);
            Assert.AreEqual(new CharacterClaim(42, 100), (await restarted.ClaimsAsync(123, 789))[0]);
            Assert.IsTrue(await restarted.UnclaimAsync(123, 789, 42, 100));
            await restarted.SaveAsync([]);
            Assert.AreEqual(0, (await new CampaignSubscriptionStore(config).LoadAsync()).Count, "Legacy data must not be imported twice.");
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
            if (File.Exists(database))
                File.Delete(database);
        }
    }

    [TestMethod]
    public void CharacterMentionMatchesOnlyTheStableActorId()
    {
        using var history = JsonDocument.Parse("""{"actorPostId":42,"actorName":"Hero","notation":"1d6"}""");
        var claims = new[] { new CharacterClaim(42, 123456) };
        var owner = CampaignSubscriptions.FindOwner(history.RootElement, claims);
        Assert.AreEqual(123456UL, owner);
        StringAssert.Contains(CampaignSubscriptions.Format(history.RootElement, owner: owner), "<@123456>");
        Assert.IsNull(CampaignSubscriptions.FindOwner(history.RootElement, [new CharacterClaim(43, 123456)]));
        using var noId = JsonDocument.Parse("""{"actorName":"Hero"}""");
        Assert.IsNull(CampaignSubscriptions.FindOwner(noId.RootElement, claims));
    }

    [TestMethod]
    public async Task FailedLegacyImportCanBeRetriedWithoutPartialData()
    {
        var prefix = Path.Combine(Path.GetTempPath(), $"dndsharp-invalid-{Guid.NewGuid():N}");
        var legacy = prefix + ".json";
        var database = prefix + ".sqlite3";
        try
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Discord:CampaignSubscriptionsPath"] = legacy,
                ["Discord:DatabasePath"] = database
            }).Build();
            await File.WriteAllTextAsync(legacy, """[{"GuildId":123,"ChannelId":456,"CampaignId":789,"CampaignSlug":"valid"},{"GuildId":0,"ChannelId":456,"CampaignId":790,"CampaignSlug":"invalid"}]""");
            var store = new CampaignSubscriptionStore(config);
            await Assert.ThrowsAsync<FormatException>(() => store.LoadAsync());
            await File.WriteAllTextAsync(legacy, """[{"GuildId":123,"ChannelId":456,"CampaignId":790,"CampaignSlug":"corrected"}]""");
            var loaded = await store.LoadAsync();
            Assert.AreEqual(1, loaded.Count);
            Assert.AreEqual("corrected", loaded[0].CampaignSlug);
        }
        finally
        {
            if (File.Exists(legacy))
                File.Delete(legacy);
            if (File.Exists(database))
                File.Delete(database);
        }
    }
}
