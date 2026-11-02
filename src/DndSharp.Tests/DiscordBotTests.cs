using System.Net;
using CardboardBox;
using CardboardBox.Json;
using Discord.Interactions;
using DndSharp.Dice.NumberGenerator;
using DndSharp.Discord;
using DndSharp.PokeDex;
using DndSharp.PokeDex.Models.Pokemon;
using DndSharp.Pokemon;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SkiaSharp;

namespace DndSharp.Tests;

[TestClass]
public sealed class DiscordBotTests
{
    [TestMethod]
    [DataRow("null", null)]
    [DataRow("112", 112)]
    public async Task PokemonWithOptionalBaseExperienceCanBeFetched(string experienceJson, int? expected)
    {
        using var fixture = new PokemonFixture();
        fixture.Write("pokemon/10281/index.json", """
            {
              "id": 10281, "name": "dragonite-mega", "base_experience": EXPERIENCE, "weight": 2900,
              "location_area_encounters": "", "species": { "url": "/api/v2/pokemon-species/149/" },
              "sprites": {}
            }
            """.Replace("EXPERIENCE", experienceJson));
        await using var provider = fixture.CreateServices();
        var pokemon = await provider.GetRequiredService<IPokeApiService>().Fetch<PkPokemon>(10281);
        Assert.IsNotNull(pokemon);
        Assert.AreEqual(expected, pokemon.BaseExperience);
        Assert.AreEqual("dragonite-mega", pokemon.Name);
        Assert.AreEqual(2900, pokemon.Weight);
    }

    [TestMethod]
    public async Task RollsCustomAndPresetDiceWithBreakdown()
    {
        var service = new DiscordRollService();
        var custom = await service.RollAsync("2d6+3 (fire), d8-1 (cold)", null, new FixedGenerator());
        StringAssert.Contains(custom, "→ 5");
        StringAssert.Contains(custom, "[1, 1] + 3 = 5");
        StringAssert.Contains(custom, "[1] - 1 = 0");
        var preset = await service.RollAsync(null, PresetDie.D20, new FixedGenerator());
        StringAssert.Contains(preset, "D20 → 1");
    }

    [TestMethod]
    public async Task RollsRejectConflictingOrExcessiveInputs()
    {
        var service = new DiscordRollService();
        await Assert.ThrowsAsync<ArgumentException>(() => service.RollAsync(null, null));
        await Assert.ThrowsAsync<ArgumentException>(() => service.RollAsync("d6", PresetDie.D20));
        await Assert.ThrowsAsync<ArgumentException>(() => service.RollAsync("nothing", null));
        await Assert.ThrowsAsync<ArgumentException>(() => service.RollAsync("101d6", null));
        await Assert.ThrowsAsync<ArgumentException>(() => service.RollAsync("d1000001", null));
    }

    [TestMethod]
    public async Task SlashCommandsCanBeRegisteredWithTheConfiguredServices()
    {
        using var fixture = new PokemonFixture();
        await using var provider = fixture.CreateServices();
        var interactions = provider.GetRequiredService<InteractionService>();
        var socketConfig = provider.GetRequiredService<global::Discord.WebSocket.DiscordSocketConfig>();
        Assert.IsTrue((socketConfig.GatewayIntents & global::Discord.GatewayIntents.GuildEmojis) != 0);
        await interactions.AddModulesAsync(typeof(BotCommands).Assembly, provider);
        CollectionAssert.AreEquivalent(new[] { "roll", "poke", "poke-dnd", "campaign-watch", "campaign-unwatch", "campaign-watches", "campaign-duality-emoji", "campaign-duality-settings", "campaign-characters", "character-claim", "character-unclaim", "character-sheet" },
            interactions.SlashCommands.Select(command => command.Name).ToArray());
        var roll = interactions.SlashCommands.Single(command => command.Name == "roll");
        CollectionAssert.AreEquivalent(new[] { "dice", "preset" },
            roll.Parameters.Select(parameter => parameter.Name).ToArray());
        var preset = roll.Parameters.Single(parameter => parameter.Name == "preset");
        var presetProperties = new global::Discord.ApplicationCommandOptionProperties();
        preset.TypeConverter.Write(presetProperties, preset);
        Assert.AreEqual(7, presetProperties.Choices.Count);
        var dnd = interactions.SlashCommands.Single(command => command.Name == "poke-dnd");
        CollectionAssert.AreEquivalent(new[] { "pokemon", "level" },
            dnd.Parameters.Select(parameter => parameter.Name).ToArray());
        var level = dnd.Parameters.Single(parameter => parameter.Name == "level");
        Assert.AreEqual(1d, Convert.ToDouble(level.MinValue));
        Assert.AreEqual(100d, Convert.ToDouble(level.MaxValue));
        Assert.AreEqual(0, interactions.SlashCommands.Single(command => command.Name == "character-claim").Parameters.Count);
        Assert.AreEqual(0, interactions.SlashCommands.Single(command => command.Name == "character-unclaim").Parameters.Count);
        Assert.AreEqual(0, interactions.SlashCommands.Single(command => command.Name == "character-sheet").Parameters.Count);
        Assert.AreEqual(7, interactions.ComponentCommands.Count);
        Assert.AreEqual(0, interactions.SlashCommands.Single(command => command.Name == "campaign-unwatch").Parameters.Count);
        Assert.AreEqual(0, interactions.SlashCommands.Single(command => command.Name == "campaign-characters").Parameters.Count);
        Assert.IsFalse(interactions.SlashCommands.Single(command => command.Name == "campaign-duality-emoji").Parameters.Any(parameter => parameter.Name == "campaign"));
        Assert.IsFalse(interactions.SlashCommands.Single(command => command.Name == "campaign-duality-settings").Parameters.Any(parameter => parameter.Name == "campaign"));
        foreach (var name in new[] { "campaign-watch", "campaign-unwatch", "campaign-duality-emoji", "campaign-duality-settings" })
        {
            var command = interactions.SlashCommands.Single(item => item.Name == name);
            Assert.AreEqual(global::Discord.GuildPermission.Administrator, command.DefaultMemberPermissions);
            Assert.IsTrue(command.Preconditions.OfType<RequireUserPermissionAttribute>().Any(item => item.GuildPermission == global::Discord.GuildPermission.Administrator));
        }
        Assert.IsFalse(interactions.SlashCommands.Single(item => item.Name == "character-sheet").Preconditions.OfType<RequireUserPermissionAttribute>().Any());
    }

    [TestMethod]
    public async Task MissingPokemonDataProducesAnActionableError()
    {
        using var fixture = new PokemonFixture();
        await using var provider = fixture.CreateServices();
        provider.GetRequiredService<IConfiguration>()["PokeApi:BasePath"] = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var service = provider.GetRequiredService<PokemonCommandService>();
        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.FindAsync("25"));
        StringAssert.Contains(error.Message, "PokeApi:BasePath");
    }

    [TestMethod]
    public async Task PokemonCommandsFindNamesAndNumbersAndShowBaseStats()
    {
        using var fixture = new PokemonFixture();
        await using var provider = fixture.CreateServices();
        var service = provider.GetRequiredService<PokemonCommandService>();
        var byName = await service.FindAsync("PIKACHU");
        var byNumber = await service.FindAsync("25");
        Assert.IsNotNull(byName);
        Assert.IsNotNull(byNumber);
        Assert.AreEqual(byName.Id, byNumber.Id);
        Assert.IsNull(await service.FindAsync("not-a-pokemon"));
        var embed = PokemonCommandService.RawStats(byName);
        Assert.AreEqual("35", embed.Fields.Single(field => field.Name == "HP").Value);
        Assert.AreEqual("320", embed.Fields.Single(field => field.Name == "Base stat total").Value);
        StringAssert.Contains(embed.Footer!.Value.Text, "base stats");
        await Assert.ThrowsAsync<ArgumentException>(() => service.TranslateAsync(byName, 101));
    }

    [TestMethod]
    public async Task TranslatedPokemonProducesPngWithArtworkAndAbilityScores()
    {
        using var fixture = new PokemonFixture();
        await using var provider = fixture.CreateServices();
        var service = provider.GetRequiredService<PokemonCommandService>();
        var mon = await service.FindAsync("pikachu");
        Assert.IsNotNull(mon);
        var character = await service.TranslateAsync(mon, 50);
        Assert.IsNotNull(character);
        Assert.AreEqual(50, character.Level);

        using var http = new HttpClient(new ImageHandler());
        var renderer = new PokemonCardRenderer(http, provider.GetRequiredService<IConfiguration>(),
            NullLogger<PokemonCardRenderer>.Instance);
        using var output = await renderer.RenderAsync(character);
        Assert.AreEqual(0, output.Position);
        using var png = SKBitmap.Decode(output);
        Assert.IsNotNull(png);
        Assert.AreEqual(PokemonCardRenderer.Width, png.Width);
        Assert.AreEqual(PokemonCardRenderer.Height, png.Height);
        Assert.AreEqual(SKColors.Red, png.GetPixel(372, 320), "Downloaded artwork should be visible in the card.");

        character.Level = 100;
        using var secondOutput = await renderer.RenderAsync(character);
        Assert.AreNotEqual(Convert.ToBase64String(((MemoryStream)output).ToArray()),
            Convert.ToBase64String(((MemoryStream)secondOutput).ToArray()), "The card should reflect the Pokémon's level and stats.");

        character.Pokemon.Sprites.FrontDefault = null;
        using var noArtwork = await renderer.RenderAsync(character);
        using var fallback = SKBitmap.Decode(noArtwork);
        Assert.IsNotNull(fallback, "A card should still be generated when artwork is unavailable.");
    }

    private sealed class FixedGenerator : INumberGenerator
    {
        public Task<int> Generate(int min, int max, CancellationToken token = default) => Task.FromResult(min);
    }

    private sealed class ImageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            using var bitmap = new SKBitmap(20, 20);
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Red);
            using var image = SKImage.FromBitmap(bitmap);
            using var png = image.Encode(SKEncodedImageFormat.Png, 100);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(png.ToArray()) });
        }
    }

    private sealed class PokemonFixture : IDisposable
    {
        private readonly string path = Path.Combine(Path.GetTempPath(), "dnd-sharp-tests", Guid.NewGuid().ToString("N"));

        public PokemonFixture()
        {
            Write("pokemon/index.json", """
                { "count": 1, "results": [{ "name": "pikachu", "url": "/api/v2/pokemon/25/" }] }
                """);
            Write("pokemon/25/index.json", """
                {
                  "id": 25, "name": "pikachu", "is_default": true,
                  "location_area_encounters": "", "species": { "name": "pikachu", "url": "/api/v2/pokemon-species/25/" },
                  "sprites": { "front_default": "https://example.com/sprite.png" },
                  "types": [{ "slot": 1, "type": { "name": "electric", "url": "/api/v2/type/13/" } }],
                  "stats": [
                    { "base_stat": 35, "stat": { "name": "hp", "url": "/api/v2/stat/1/" } },
                    { "base_stat": 55, "stat": { "name": "attack", "url": "/api/v2/stat/2/" } },
                    { "base_stat": 40, "stat": { "name": "defense", "url": "/api/v2/stat/3/" } },
                    { "base_stat": 50, "stat": { "name": "special-attack", "url": "/api/v2/stat/4/" } },
                    { "base_stat": 50, "stat": { "name": "special-defense", "url": "/api/v2/stat/5/" } },
                    { "base_stat": 90, "stat": { "name": "speed", "url": "/api/v2/stat/6/" } }
                  ]
                }
                """);
            Write("pokemon-species/25/index.json", """
                {
                  "id": 25, "name": "pikachu", "color": { "url": "/api/v2/pokemon-color/1/" },
                  "generation": { "url": "/api/v2/generation/1/" },
                  "growth_rate": { "url": "/api/v2/growth-rate/1/" },
                  "habitat": { "url": "/api/v2/pokemon-habitat/1/" },
                  "shape": { "url": "/api/v2/pokemon-shape/1/" }
                }
                """);
            Write("growth-rate/1/index.json", """{ "id": 1, "name": "slow", "formula": "x" }""");
            Write("nature/index.json", """
                { "count": 2, "results": [
                  { "name": "hardy", "url": "/api/v2/nature/1/" },
                  { "name": "docile", "url": "/api/v2/nature/2/" }
                ] }
                """);
            Write("nature/1/index.json", """{ "id": 1, "name": "hardy" }""");
            Write("nature/2/index.json", """{ "id": 2, "name": "docile" }""");
        }

        public ServiceProvider CreateServices()
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PokeApi:BasePath"] = path
            }).Build();
            return new ServiceCollection()
                .AddLogging()
                .AddSingleton<IConfiguration>(configuration)
                .AddJson()
                .AddPokeApi()
                .AddPokemonServices()
                .AddDiscordBot()
                .BuildServiceProvider();
        }

        public void Write(string name, string json)
        {
            var file = Path.Combine(path, "api", "v2", name.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, json);
        }

        public void Dispose() => Directory.Delete(path, recursive: true);
    }
}
