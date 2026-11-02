using System.Text.Json;
using DndSharp.PokeDex.Models.Game;
using DndSharp.PokeDex.Models.Item;
using DndSharp.PokeDex.Models.Move;
using DndSharp.PokeDex.Models.Pokemon;

namespace DndSharp.Tests;

[TestClass]
public sealed class PokeApiModelTests
{
    [TestMethod]
    public void ItemPreservesPricesFlavorTextHeldPokemonAndMachines()
    {
        const string json = """
            {
              "id": 1, "name": "example", "sprites": { "default": "sprite.png" },
              "prices": [{ "purchase_price": null, "sell_price": 100,
                "currency": { "url": "/api/v2/currency/1/" },
                "version_group": { "url": "/api/v2/version-group/1/" } }],
              "flavor_text_entries": [{ "text": "A useful item.",
                "language": { "url": "/api/v2/language/9/" },
                "version_group": { "url": "/api/v2/version-group/1/" } }],
              "held_by_pokemon": [{ "pokemon": { "url": "/api/v2/pokemon/1/" },
                "version_details": [{ "rarity": 5, "version": { "url": "/api/v2/version/1/" } }] }],
              "machines": [{ "machine": { "url": "/api/v2/machine/1/" },
                "version_group": { "url": "/api/v2/version-group/1/" } }]
            }
            """;
        var item = JsonSerializer.Deserialize<PkItem>(json)!;
        item = JsonSerializer.Deserialize<PkItem>(JsonSerializer.Serialize(item))!;
        Assert.IsNull(item.Prices[0].PurchasePrice);
        Assert.AreEqual(100, item.Prices[0].SellPrice);
        Assert.AreEqual("A useful item.", item.FlavorTexts[0].Value);
        Assert.AreEqual(1, item.HeldByPokemon[0].Resource.Id);
        Assert.AreEqual(5, item.HeldByPokemon[0].Versions[0].Rarity);
        Assert.AreEqual(1, item.Machines[0].Resource.Id);
    }

    [TestMethod]
    public void MoveContestCombosUseSnakeCaseFields()
    {
        const string json = """
            { "normal": { "use_before": [{ "url": "/api/v2/move/3/" }], "use_after": null },
              "super": { "use_before": null, "use_after": [{ "url": "/api/v2/move/4/" }] } }
            """;
        var combos = JsonSerializer.Deserialize<PkMove.Combos>(json)!;
        Assert.AreEqual(3, combos.Normal!.UseBefore![0].Id);
        Assert.AreEqual(4, combos.Super!.UseAfter![0].Id);
    }

    [TestMethod]
    public void ResourcesAcceptNullOptionalReferences()
    {
        var location = JsonSerializer.Deserialize<PkLocation>("""{ "name": "x", "region": null }""");
        var region = JsonSerializer.Deserialize<PkRegion>("""{ "name": "x", "main_generation": null }""");
        var pokedex = JsonSerializer.Deserialize<PkPokedex>("""{ "name": "x", "region": null }""");
        var species = JsonSerializer.Deserialize<PkPokemonSpecies>("""
            { "name": "x", "habitat": null, "color": { "url": "/1/" },
              "generation": { "url": "/1/" }, "growth_rate": { "url": "/1/" },
              "shape": { "url": "/1/" } }
            """);
        Assert.IsNull(location!.Region);
        Assert.IsNull(region!.MainGeneration);
        Assert.IsNull(pokedex!.Region);
        Assert.IsNull(species!.Habitat);
    }
}
