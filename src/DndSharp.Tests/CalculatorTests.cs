using DndSharp.PokeDex;

namespace DndSharp.Tests;

[TestClass]
public class CalculatorTests
{
    [TestMethod]
    public void Gen2To4()
    {
        var skitty = new BattleExpCalculator
        {
            HasLuckyEgg = true,
            Generation = Generation.GenerationIV,
            Participants = 2,
            ExpShareCount = 1,
            DefeatedLevel = 78,
            DefeatedEffort = 218,
            IsOriginalTrainer = true,
            DefeatedOwnedByTrainer = true,
            IsParticipant = true,
            PartyMembers = 6,
        };
        var skittyExp = skitty.Calculate();
        Assert.AreEqual(1365, skittyExp, "Skitty EXP");

        var salamence = new BattleExpCalculator
        {
            HasLuckyEgg = false,
            Generation = Generation.GenerationIV,
            Participants = 2,
            ExpShareCount = 1,
            DefeatedLevel = 78,
            DefeatedEffort = 218,
            IsOriginalTrainer = true,
            DefeatedOwnedByTrainer = true,
            IsParticipant = false,
            PartyMembers = 6,
        };
        var salamenceExp = salamence.Calculate();
        Assert.AreEqual(1821, salamenceExp, "Salamence EXP");

        var meowth = new BattleExpCalculator
        {
            HasLuckyEgg = false,
            Generation = Generation.GenerationIV,
            Participants = 2,
            ExpShareCount = 1,
            DefeatedLevel = 78,
            DefeatedEffort = 218,
            IsOriginalTrainer = false,
            HasDifferentLanguage = true,
            DefeatedOwnedByTrainer = true,
            IsParticipant = true,
            PartyMembers = 6,
        };
        var meowthExp = meowth.Calculate();
        Assert.AreEqual(1547, meowthExp, "Meowth EXP");
    }

    [TestMethod]
    public void Gen5()
    {
        var venusaur = new BattleExpCalculator
        {
            Generation = Generation.GenerationIX,
            IsOriginalTrainer = false,
            HasDifferentLanguage = true,
            WinnerLevel = 55,
            DefeatedLevel = 62,
            DefeatedOwnedByTrainer = false,
            DefeatedEffort = 306,
        };
        var venusaurExp = venusaur.Calculate();
        Assert.AreEqual(7376, venusaurExp, "Venusaur EXP");
    }

    [TestMethod]
    public void LevelTests()
    {
        (int level, int exp, ExperienceGain function)[] tests =
        [
            (001, 0_000_000, ExperienceGain.Erratic),
            (002, 0_000_015, ExperienceGain.Erratic),
            (050, 0_125_000, ExperienceGain.Erratic),
            (069, 0_267_406, ExperienceGain.Erratic),
            (100, 0_600_000, ExperienceGain.Erratic),

            (001, 0_000_000, ExperienceGain.Fluctuating),
            (002, 0_000_004, ExperienceGain.Fluctuating),
            (050, 0_142_500, ExperienceGain.Fluctuating),
            (069, 0_433_631, ExperienceGain.Fluctuating),
            (100, 1_640_000, ExperienceGain.Fluctuating),

            (001, 0_000_000, ExperienceGain.Fast),
            (002, 0_000_006, ExperienceGain.Fast),
            (050, 0_100_000, ExperienceGain.Fast),
            (069, 0_262_807, ExperienceGain.Fast),
            (100, 0_800_000, ExperienceGain.Fast),

            (001, 0_000_000, ExperienceGain.Slow),
            (002, 0_000_010, ExperienceGain.Slow),
            (050, 0_156_250, ExperienceGain.Slow),
            (069, 0_410_636, ExperienceGain.Slow),
            (100, 1_250_000, ExperienceGain.Slow),

            (001, 0_000_000, ExperienceGain.MediumFast),
            (002, 0_000_008, ExperienceGain.MediumFast),
            (050, 0_125_000, ExperienceGain.MediumFast),
            (069, 0_328_509, ExperienceGain.MediumFast),
            (100, 1_000_000, ExperienceGain.MediumFast),

            (001, 0_000_000, ExperienceGain.MediumSlow),
            (002, 0_000_009, ExperienceGain.MediumSlow),
            (050, 0_117_360, ExperienceGain.MediumSlow),
            (069, 0_329_555, ExperienceGain.MediumSlow),
            (100, 1_059_860, ExperienceGain.MediumSlow),
        ];

        foreach(var (lvl, exp, func) in tests)
        {
            var lvlTest = ExperienceLevelCalculator.LevelFromExp(exp, func);
            var expTest = ExperienceLevelCalculator.ExpFromLevel(lvl, func);

            Assert.AreEqual(exp, expTest, $"EXPERIENCE - {func} - lvl:{lvl} - exp:{exp}");
            Assert.AreEqual(lvl, lvlTest, $"LEVEL - {func} - lvl:{lvl} - exp:{exp}");
        }
    }

    [TestMethod]
    public void StatTests()
    {
        var garchompEv = new PokemonEVs(74, 190, 91, 48, 84, 23);
        var garchompIv = new PokemonIVs(24, 12, 30, 16, 23, 5);
        var garchompStats = new PokemonStats(108, 130, 95, 80, 85, 102);
        var adamant = new PokemonStats(0, 1, 0, -1, 0, 0);
        var calc = new StatCalculator
        {
            Stats = garchompStats,
            EVs = garchompEv,
            IVs = garchompIv,
            NatureModifiers = adamant
        };
        var output = calc.Calculate(78, out var errors);

        Assert.AreEqual(0, errors.Length);
        Assert.AreEqual(289, output.Hp, "Garchomp HP");
        Assert.AreEqual(278, output.Attack, "Garchomp Attack");
        Assert.AreEqual(193, output.Defense, "Garchomp Defense");
        Assert.AreEqual(135, output.SpecialAttack, "Garchomp Special Attack");
        Assert.AreEqual(171, output.SpecialDefense, "Garchomp Special Defense");
        Assert.AreEqual(171, output.Speed, "Garchomp Speed");
    }
}
