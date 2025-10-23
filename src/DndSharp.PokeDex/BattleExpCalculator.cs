namespace DndSharp.PokeDex;

public enum Generation
{
    /// <summary>
    /// red, blue, yellow, green (jp), blue (jp)
    /// </summary>
    GenerationI = 1,
    /// <summary>
    /// gold, silver, crystal
    /// </summary>
    GenerationII = 2,
    /// <summary>
    /// ruby, sapphire, emerald, fire red, leaf green
    /// </summary>
    GenerationIII = 3,
    /// <summary>
    /// diamond, pearl, platinum, heart gold, soul silver
    /// </summary>
    GenerationIV = 4,
    /// <summary>
    /// black, white
    /// </summary>
    GenerationV = 5,
    /// <summary>
    /// black 2, white 2
    /// </summary>
    /// <remarks>This has to be separate to handle the 100k exp cap</remarks>
    GenerationV_BW2 = 6,
    /// <summary>
    /// x, y, omega ruby, alpha sapphire
    /// </summary>
    GenerationVI = 7,
    /// <summary>
    /// sun, moon, ultra sun, ultra moon
    /// </summary>
    GenerationVII = 8,
    /// <summary>
    /// let's go pikachu, let's go eevee
    /// </summary>
    /// <remarks>This has to be separate to handle friendhsip level modifier</remarks>
    GenerationVII_LetsGo = 9,
    /// <summary>
    /// sword, shield, brilliant diamond, shining pearl, legends: arceus
    /// </summary>
    GenerationVIII = 10,
    /// <summary>
    /// scarlet, violet
    /// </summary>
    GenerationIX = 11
}

public enum OPower
{
    Level1 = 1,
    Level2 = 2,
    Level3 = 3,
    S = 4,
    Max = 5
}

public enum PassPower
{
    PositiveLevel1 = 1,
    PositiveLevel2 = 2,
    PositiveLevel3 = 3,
    NegativeLevel1 = 4,
    NegativeLevel2 = 5,
    NegativeLevel3 = 6
}

[Flags]
public enum BonusMultiplier
{
    /// <summary>
    /// Ignored
    /// </summary>
    None = 0,
    /// <summary>
    /// Catching a Pokémon not registered in the Pokédex
    /// </summary>
    NewPokemon = 1,
    /// <summary>
    /// Hitting within the target ring at a certain size
    /// </summary>
    ExcellentThrow = NewPokemon * 2,
    /// <summary>
    /// Hitting within the target ring at a certain size
    /// </summary>
    GreatThrow = ExcellentThrow * 2,
    /// <summary>
    /// Hitting within the target ring at a certain size
    /// </summary>
    NiceThrow = GreatThrow * 2,
    /// <summary>
    /// Catching a Pokémon on the first throw
    /// </summary>
    FirstThrow = NiceThrow * 2,
    /// <summary>
    /// Throwing a Poké Ball at the same time as the Support Trainer
    /// </summary>
    SynchronizedThrow = FirstThrow * 2,
    /// <summary>
    /// Catch Combo 1 to 10
    /// </summary>
    Combo1To10 = SynchronizedThrow * 2,
    /// <summary>
    /// Catch Combo 11 to 20
    /// </summary>
    Combo11To20 = Combo1To10 * 2,
    /// <summary>
    /// Catch Combo 21 to 30
    /// </summary>
    Combo21To30 = Combo11To20 * 2,
    /// <summary>
    /// Catch Combo 31 to 40
    /// </summary>
    Combo31To40 = Combo21To30 * 2,
    /// <summary>
    /// Catch Combo 41+
    /// </summary>
    Combo41Plus = Combo31To40 * 2,
    /// <summary>
    /// S- or L-sized Pokémon
    /// </summary>
    SizeBonusSOrL = Combo41Plus * 2,
    /// <summary>
    /// XS- or XL-sized Pokémon
    /// </summary>
    SizeBonusXSOrXL = SizeBonusSOrL * 2,
    /// <summary>
    /// Using a Joy-Con or Poké Ball Plus (if Synchronized Bonus is not applied)
    /// </summary>
    TechniqueBonus = SizeBonusXSOrXL * 2,
}

public class BattleExpCalculator
{
    /// <summary>
    /// If the defeated Pokémon is owned by a trainer (true) or wild (false)
    /// </summary>
    public bool DefeatedOwnedByTrainer { get; set; }

    /// <summary>
    /// The effort value yield of the defeated Pokémon
    /// </summary>
    /// <remarks>this is the base_experience or the EXP column from: https://bulbapedia.bulbagarden.net/wiki/List_of_Pokémon_by_effort_value_yield_in_Generations_V_and_VI</remarks>
    public int DefeatedEffort { get; set; }

    /// <summary>
    /// The level of the defeated Pokémon
    /// </summary>
    public int DefeatedLevel { get; set; }

    /// <summary>
    /// The level of the winning Pokémon
    /// </summary>
    public int WinnerLevel { get; set; }

    /// <summary>
    /// Whether or not the Pokémon is holding a Lucky Egg
    /// </summary>
    public bool HasLuckyEgg { get; set; } = false;

    /// <summary>
    /// The number of affection hearts the winning Pokémon has for the current trainer
    /// </summary>
    /// <remarks>
    /// <list type="table">
    /// <listheader>
    /// <term>Hearts</term><description>Points</description>
    /// </listheader>
    /// <item><term>0</term><description>0</description></item>
    /// <item><term>1</term><description>1</description></item>
    /// <item><term>2</term><description>50</description></item>
    /// <item><term>3</term><description>100</description></item>
    /// <item><term>4</term><description>150</description></item>
    /// <item><term>5</term><description>255</description></item>
    /// </list>
    /// <para>Taken from: <see href="https://bulbapedia.bulbagarden.net/wiki/Affection"/></para>
    /// </remarks>
    public int AffectionHearts
    {
        get
        {
            if (AffectionLevel <= 0) return 0;
            if (AffectionLevel < 50) return 1;
            if (AffectionLevel < 100) return 2;
            if (AffectionLevel < 150) return 3;
            if (AffectionLevel < 255) return 4;
            return 5;
        }
        set
        {
            AffectionLevel = value switch
            {
                <= 0 => 0,
                1 => 1,
                2 => 50,
                3 => 100,
                4 => 150,
                _ => 255
            };
        }
    }

    /// <summary>
    /// The Pokémon's affection / friendship level
    /// </summary>
    public int AffectionLevel { get; set; } = 0;

    /// <summary>
    /// The generation of the game being played
    /// </summary>
    public Generation Generation { get; set; } = Generation.GenerationIX;

    /// <summary>
    /// The Pass Power from Generation V
    /// </summary>
    public PassPower? PassPower { get; set; }

    /// <summary>
    /// The O-Power level from Generation VI
    /// </summary>
    public OPower? OPower { get; set; } = null;

    /// <summary>
    /// Whether or not Roto-Exp Points is active
    /// </summary>
    public bool RotoExpPoints { get; set; } = false;

    /// <summary>
    /// Whether or not the winning trainer has the Exp. Charm
    /// </summary>
    public bool ExpCharm { get; set; } = false;

    /// <summary>
    /// Whether or not the winning trainer has an Exp All, Exp Share (only VI+)
    /// </summary>
    public bool ExpItem { get; set; } = false;

    /// <summary>
    /// The number of Pokémon holding an EXP Share (only V-)
    /// </summary>
    public int ExpShareCount { get; set; } = 0;

    /// <summary>
    /// The number of participanting Pokémon sharing the experience
    /// </summary>
    /// <remarks>This should only be not-fainted Pokémon</remarks>
    public int Participants { get; set; } = 1;

    /// <summary>
    /// Whether or not the current Pokémon was a participant in the battle
    /// </summary>
    public bool IsParticipant { get; set; } = true;

    /// <summary>
    /// The number of Pokémon in the winning trainer's party
    /// </summary>
    public int PartyMembers { get; set; } = 6;

    /// <summary>
    /// Whether or not the winning Pokémon's current owner is its original trainer
    /// </summary>
    public bool IsOriginalTrainer { get; set; } = true;

    /// <summary>
    /// Whether or not the winning Pokémon has a different language than its original trainer
    /// </summary>
    public bool HasDifferentLanguage { get; set; } = false;

    /// <summary>
    /// Whether or not the winning Pokémon can evolve but hasn't yet
    /// </summary>
    public bool CanEvolveButHasnt { get; set; } = false;

    /// <summary>
    /// The bonus multiplier for Let's Go Pikachu & Eevee
    /// </summary>
    public BonusMultiplier BonusMultiplier { get; set; } = BonusMultiplier.None;

    /// <summary>
    /// Calculate the effect of EXP Share/All by generation
    /// </summary>
    internal double ExpShare()
    {
        if (Generation == Generation.GenerationI)
        {
            if (!ExpItem) return Participants;

            if (IsParticipant) return Participants * 2;

            return Participants * 2 * PartyMembers;
        }

        //Exp Share for II to V
        if (Generation <= Generation.GenerationV_BW2)
        {
            if (ExpShareCount == 0) 
                return Participants;

            if (IsParticipant) 
                return Participants * 2;

            return ExpShareCount * 2;
        }

        //Exp Share for VI and later
        if (IsParticipant || !ExpItem) return 1.0;

        return 2.0;
    }

    /// <summary>
    /// Calculate any EXP bonuses from other items / statuses
    /// </summary>
    internal double ExpBonuses()
    {
        //Roto-Exp Points
        if (RotoExpPoints && Generation == Generation.GenerationVII) 
            return 2.0;
        //O-Power
        if (OPower is not null && Generation == Generation.GenerationVI)
            return OPower switch
            {
                PokeDex.OPower.Level1 => 1.2,
                PokeDex.OPower.Level2 => 1.5,
                PokeDex.OPower.Level3 or 
                PokeDex.OPower.S or 
                PokeDex.OPower.Max => 2.0,
                _ => 1
            };
        //Pass Power
        if (PassPower is not null && Generation == Generation.GenerationV_BW2)
            return PassPower switch
            {
                PokeDex.PassPower.PositiveLevel1 => 1.2,
                PokeDex.PassPower.PositiveLevel2 => 1.5,
                PokeDex.PassPower.PositiveLevel3 => 2,
                PokeDex.PassPower.NegativeLevel1 => 0.8,
                PokeDex.PassPower.NegativeLevel2 => 0.66,
                PokeDex.PassPower.NegativeLevel3 => 0.5,
                _ => 1
            };
        //The Exp. Charm
        if (ExpCharm && Generation >= Generation.GenerationVIII)
            return 1.5;

        //Pokemon Let's Go bonus multipliers
        double total = 1.0;
        if (BonusMultiplier.HasFlag(BonusMultiplier.NewPokemon))
            total *= 1.5;

        if (BonusMultiplier.HasFlag(BonusMultiplier.ExcellentThrow))
            total *= 2.0;
        else if (BonusMultiplier.HasFlag(BonusMultiplier.GreatThrow))
            total *= 1.5;
        else if (BonusMultiplier.HasFlag(BonusMultiplier.NiceThrow))
            total *= 1.1;

        if (BonusMultiplier.HasFlag(BonusMultiplier.FirstThrow))
            total *= 1.5;

        if (BonusMultiplier.HasFlag(BonusMultiplier.SynchronizedThrow))
            total *= 2.0;
        else if (BonusMultiplier.HasFlag(BonusMultiplier.TechniqueBonus))
            total *= 1.1;

        if (BonusMultiplier.HasFlag(BonusMultiplier.Combo1To10))
            total *= 1.1;
        else if (BonusMultiplier.HasFlag(BonusMultiplier.Combo11To20))
            total *= 1.5;
        else if (BonusMultiplier.HasFlag(BonusMultiplier.Combo21To30))
            total *= 2.0;
        else if (BonusMultiplier.HasFlag(BonusMultiplier.Combo31To40))
            total *= 2.5;
        else if (BonusMultiplier.HasFlag(BonusMultiplier.Combo41Plus))
            total *= 3.0;

        if (BonusMultiplier.HasFlag(BonusMultiplier.SizeBonusSOrL))
            total *= 1.5;
        else if (BonusMultiplier.HasFlag(BonusMultiplier.SizeBonusXSOrXL))
            total *= 4.0;

        return total > 1.0 ? total : 1.0;
    }

    /// <summary>
    /// Calculate outsider Pokémon multipliers
    /// </summary>
    internal double OutsiderMultiplier()
    {
        if (IsOriginalTrainer) return 1.0;

        if (!HasDifferentLanguage) return 1.5;

        if (Generation == Generation.GenerationIV)
            return 1.7;

        if (Generation >= Generation.GenerationV)
            return 6963.0 / 4096.0;

        return 1.5;
    }

    /// <summary>
    /// Calculate the Pokémon's friendship multiplier
    /// </summary>
    internal double FriendshipModifer()
    {
        const double afl = 4915.0 / 4096.0;
        const double def = 1.0;

        //US/UM - Hearts >= 2 - afl
        if (Generation == Generation.GenerationVII)
            return AffectionHearts >= 2 ? afl : def;
        //Lets Go - Friendship >= 100 - afl
        if (Generation == Generation.GenerationVII_LetsGo)
            return AffectionLevel >= 100 ? afl : def;
        //Gen VI only - Friendship > 220 - 1.2
        if (Generation == Generation.GenerationVI)
            return AffectionLevel >= 220 ? 1.2 : def;
        //SW/SH and on - Friendship > 220 - afl
        if (Generation >= Generation.GenerationVIII)
            return AffectionLevel >= 220 ? afl : def;
        //Otherwise no modifier
        return def;
    }

    /// <summary>
    /// Calculate the EXP Gain
    /// </summary>
    /// <remarks>
    /// The majority of this is taken from:
    /// <see href="https://bulbapedia.bulbagarden.net/wiki/Experience#Experience_gain_in_battle" /> 
    /// </remarks>
    public int Calculate()
    {
        static double RoundDown(double value) => Math.Round(value, MidpointRounding.ToZero);
        static double Floor(double value) => Math.Floor(value);
        var a = DefeatedOwnedByTrainer ? 1.5 : 1;
        var b = (double)DefeatedEffort;
        var e = HasLuckyEgg ? 1.5 : 1;
        var f = FriendshipModifer();
        var l = (double)DefeatedLevel;
        var lp = (double)WinnerLevel;
        var p = ExpBonuses();
        var t = OutsiderMultiplier();
        var s = ExpShare();
        //  d = Suffering();
        var v = CanEvolveButHasnt && Generation >= Generation.GenerationVI ? 4915.0 / 4096.0 : 1.0;
        var expShare = 1.0 / s;

        if (Generation <= Generation.GenerationIV)
        {
            var g1Base = ((b * l) / 7.0);
            var g1 = Floor(Floor(Floor(Floor(g1Base * expShare) * e) * a) * t);
            return (int)g1;
        }

        if (Generation == Generation.GenerationVI)
        {
            var g6Base = ((b * l) / 7.0);
            var g6Std = RoundDown(RoundDown(g6Base * a) * expShare);
            var g6 = Math.Floor(g6Std * t * e * v * f * p);
            return (int)g6;
        }

        if (Generation < Generation.GenerationVII)
        {
            var g5Base = ((b * l) / 5.0);
            var n = Floor(Math.Sqrt((2.0 * l) + 10) * Math.Pow((2.0 * l) + 10, 2.0));
            var d = Floor(Math.Sqrt(l + lp + 10.0) * Math.Pow(l + lp + 10, 2.0));
            var g5Level = n / d;
            var g5 = (g5Base * a * expShare * g5Level + 1) * t * e * p;
            var g5Output = (int)Floor(g5);
            //B/W2 maxes at 100,000
            if (Generation == Generation.GenerationV_BW2 && g5Output > 100_000)
                return 100_000;
            return g5Output;
        }

        var g7Base = Floor((b * l) / 5.0);
        var g7Level = Math.Pow(((2.0 * l) + 10.0) / (l + lp + 10.0), 2.5);
        var g7Std = RoundDown(RoundDown(RoundDown(g7Base * expShare) * g7Level) + 1);
        var g7 = Floor(g7Std * t * e * v * f * p);
        return (int)g7;
    }
}