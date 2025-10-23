using System.Text.Json;
using System.Text.Json.Serialization;

namespace DndSharp.Tests;

using Dice;
using Dice.NumberGenerator;

[TestClass]
public sealed class DiceTests
{
    [TestMethod]
    public void TestFormatting()
    {
        (Die dice, string expected)[] values =
        [
            (Die.D20, "D20"),
            (new Die(3, 5), "5D3"),
            (new Die(6, 2, DamageType.Fire), "2D6 (fire)"),
            (new Die(10, 4, DamageType.Poison, 3), "4D10+3 (poison)"),
            (new Die(8, 1, DamageType.Radiant, -2), "D8-2 (radiant)"),
        ];

        foreach (var (dice, expected) in values)
        {
            string actual = dice.ToString();
            Assert.AreEqual(expected, actual, $"Failed for {dice}");
        }
    }

    [TestMethod]
    public void TestParsing()
    {
        (Die dice, string expected)[] values =
        [
            (Die.D20, "D20"),
            (new Die(3, 5), "5D3"),
            (new Die(6, 2, DamageType.Fire), "2D6 (fire)"),
            (new Die(10, 4, DamageType.Poison, 3), "4d10+3 (poison)"),
            (new Die(8, 1, DamageType.Radiant, -2), "d8-2 (radiant)"),
            (new Die(8, 7, DamageType.Radiant, -100), "7D8-100(r)"),
            (new Die(8, 7, DamageType.Radiant, -100), "7D8-100\t(r)"),
            (new Die(8, 7, DamageType.Radiant, -100), "7D8-100\t\t   (r)"),
        ];

        foreach (var (dice, expected) in values)
        {
            Die parsed = Die.Parse(expected);
            Assert.AreEqual(dice, parsed, $"Failed to parse {expected}");
        }

        Assert.ThrowsException<ArgumentException>(() => Die.Parse(string.Empty));
        Assert.ThrowsException<FormatException>(() => Die.Parse("InvalidDiceFormat"));

        var result = Die.TryParse("2D6+3 (fire)", out Die parsedDice);
        Assert.IsTrue(result, "Failed to parse valid dice format");
        Assert.AreEqual(new Die(6, 2, DamageType.Fire, 3), parsedDice, "Parsed dice does not match expected value");

        result = Die.TryParse("InvalidFormat", out _);
        Assert.IsFalse(result, "Parsed an invalid dice format successfully, which should not happen");
    }

    [TestMethod]
    public void TestParsingMultiple()
    {
        var input = "2D6+3 (fire), 1D8-2 (radiant), 3D4";
        var dice = Die.ParseAll(input).ToArray();

        Assert.AreEqual(3, dice.Length, "Expected 3 dice to be parsed from the input string.");
        Assert.AreEqual(new Die(6, 2, DamageType.Fire, 3), dice[0], "First dice did not match expected value.");
        Assert.AreEqual(new Die(8, 1, DamageType.Radiant, -2), dice[1], "Second dice did not match expected value.");
        Assert.AreEqual(new Die(4, 3), dice[2], "Third dice did not match expected value.");
    }

    [TestMethod]
    public void TestOperators()
    {
        var dice = Die.D20 + 100;
        Assert.AreEqual(new Die(20, 1, DamageType.Unknown, 100), dice, "Dice addition did not produce expected result.");

        var roll = (Die)"3D10+3 (fire)" + (Die)"2D8+2 (radiant)";
        Assert.AreEqual(2, roll.Count, "Expected 2 dice in the roll after addition.");
        Assert.AreEqual(new Die(10, 3, DamageType.Fire, 3), roll[0], "First dice in the roll did not match expected value.");
        Assert.AreEqual(new Die(8, 2, DamageType.Radiant, 2), roll[1], "Second dice in the roll did not match expected value.");
    }

    [TestMethod]
    public async Task TestDiceRolls()
    {
        var generator = new StandardNumberGenerator();

        var dice = Die.D20;
        var advantage = await dice.Roll(RollType.Advantage, generator);
        Assert.IsTrue(advantage.Total >= advantage.AlternateTotal, "Advantage dice not greater than alternative");
        Assert.AreEqual(1, advantage.Rolls.Length, "Advantage roll should have 1 roll");
        Assert.AreEqual(1, advantage.AlternateRolls.Length, "Advantage roll should have 1 alternate roll");
        Assert.AreEqual(RollType.Advantage, advantage.Type, "Roll type should be Advantage");

        var disadvantage = await dice.Roll(RollType.Disadvantage, generator);
        Assert.IsTrue(disadvantage.Total <= disadvantage.AlternateTotal, "Disadvantage dice not greater than alternative");
        Assert.AreEqual(1, disadvantage.Rolls.Length, "Disadvantage roll should have 1 roll");
        Assert.AreEqual(1, disadvantage.AlternateRolls.Length, "Disadvantage roll should have 1 alternate roll");
        Assert.AreEqual(RollType.Disadvantage, disadvantage.Type, "Roll type should be Disadvantage");

        var standard = await dice.Roll(generator: generator);
        Assert.AreEqual(1, standard.Rolls.Length, "Standard roll should have 1 roll");
        Assert.AreEqual(0, standard.AlternateRolls.Length, "Standard roll should have no alternate rolls");
        Assert.AreEqual(RollType.Regular, standard.Type, "Roll type should be Regular");
    }

    [TestMethod]
    public async Task TestRolls()
    {
        var generator = new StandardNumberGenerator();
        var rolls = (Roll)"10D4+5 (radiant) + 3D8-2 (poison) + 4D10 (force)";
        var min = 15 + 1 + 4; // 10D4 minimum is 10 (+5), 3D8 minimum is 3 (-2), 4D10 minimum is 4
        var max = ((10 * 4) + 5) +
                  ((3 * 8) - 2) +
                  (4 * 10);

        Assert.AreEqual(3, rolls.Count, "Expected 3 dice in the roll.");
        Assert.AreEqual(new Die(4, 10, DamageType.Radiant, 5), rolls[0], "First dice did not match expected value.");
        Assert.AreEqual(new Die(8, 3, DamageType.Poison, -2), rolls[1], "Second dice did not match expected value.");
        Assert.AreEqual(new Die(10, 4, DamageType.Force), rolls[2], "Third dice did not match expected value.");

        Assert.AreEqual(min, rolls.Min, "Minimum value of the roll did not match expected value.");
        Assert.AreEqual(max, rolls.Max, "Maximum value of the roll did not match expected value.");

        var results = await rolls.Results(generator: generator);
        Assert.AreEqual(3, results.Count, "Expected 3 results from the roll.");
        Assert.AreEqual(3, results.TotalsByDamage.Count, "Roll type should be Regular");
        Assert.IsTrue(results.TotalsByDamage.ContainsKey(DamageType.Radiant), "Radiant damage type should be present in totals.");
        Assert.IsTrue(results.TotalsByDamage.ContainsKey(DamageType.Poison), "Poison damage type should be present in totals.");
        Assert.IsTrue(results.TotalsByDamage.ContainsKey(DamageType.Force), "Force damage type should be present in totals.");
        Assert.IsTrue(results.Total >= min, results.Total + " is not greater than or equal to " + min);
        Assert.IsTrue(results.Total <= max, results.Total + " is not less than or equal to " + max);
        Assert.AreEqual(17, results.Sum(t => t.Rolls.Length), "Total number of rolls should be 17 (10 + 3 + 4).");
    }

    [TestMethod]
    public async Task TestCryptographyRolls()
    {
        var generator = new CryptoNumberGenerator();

        var dice = Die.D20;
        var advantage = await dice.Roll(RollType.Advantage, generator);
        Assert.IsTrue(advantage.Total >= advantage.AlternateTotal, "Advantage dice not greater than alternative");
        Assert.AreEqual(1, advantage.Rolls.Length, "Advantage roll should have 1 roll");
        Assert.AreEqual(1, advantage.AlternateRolls.Length, "Advantage roll should have 1 alternate roll");
        Assert.AreEqual(RollType.Advantage, advantage.Type, "Roll type should be Advantage");

        var disadvantage = await dice.Roll(RollType.Disadvantage, generator);
        Assert.IsTrue(disadvantage.Total <= disadvantage.AlternateTotal, "Disadvantage dice not greater than alternative");
        Assert.AreEqual(1, disadvantage.Rolls.Length, "Disadvantage roll should have 1 roll");
        Assert.AreEqual(1, disadvantage.AlternateRolls.Length, "Disadvantage roll should have 1 alternate roll");
        Assert.AreEqual(RollType.Disadvantage, disadvantage.Type, "Roll type should be Disadvantage");

        var standard = await dice.Roll(generator: generator);
        Assert.AreEqual(1, standard.Rolls.Length, "Standard roll should have 1 roll");
        Assert.AreEqual(0, standard.AlternateRolls.Length, "Standard roll should have no alternate rolls");
        Assert.AreEqual(RollType.Regular, standard.Type, "Roll type should be Regular");
    }

    [TestMethod]
    public void TestJsonSerialization()
    {
        const string JSON = "{\"dice\":\"2D20\",\"roll\":\"2D6\\u002B3 (fire) \\u002B D8-2 (radiant) \\u002B 3D4 \\u002B 10D6\"}";

        var parse = JsonSerializer.Deserialize<DiceJsonTest>(JSON);
        Assert.IsNotNull(parse, "Deserialization failed");
        Assert.AreEqual(new Die(20, 2), parse.Dice, "Parsed dice did not match expected value.");
        Assert.AreEqual(4, parse.Roll.Count, "Expected 4 dice in the roll after deserialization.");
        Assert.AreEqual(new Die(6, 2, DamageType.Fire, 3), parse.Roll[0], "First dice in the roll did not match expected value.");
        Assert.AreEqual(new Die(8, 1, DamageType.Radiant, -2), parse.Roll[1], "Second dice in the roll did not match expected value.");
        Assert.AreEqual(new Die(4, 3), parse.Roll[2], "Third dice in the roll did not match expected value.");
        Assert.AreEqual(new Die(6, 10), parse.Roll[3], "Fourth dice in the roll did not match expected value.");

        var json = JsonSerializer.Serialize(parse);
        Assert.IsNotNull(json, "Serialization failed");
        Assert.AreEqual(JSON, json, "Serialized JSON did not match expected value.");
    }

    internal class DiceJsonTest
    {
        [JsonPropertyName("dice")]
        public required Die Dice { get; set; }

        [JsonPropertyName("roll")]
        public required Roll Roll { get; set; }
    }
}
