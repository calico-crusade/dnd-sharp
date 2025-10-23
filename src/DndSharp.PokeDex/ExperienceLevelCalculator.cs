namespace DndSharp.PokeDex;

/// <summary>
/// The experience gain forumla
/// </summary>
/// <remarks>The numbers correspond to the api-data ID for /api/v1/growth-rate</remarks>
public enum ExperienceGain
{
    Slow = 1,
    MediumFast = 2,
    Fast = 3,
    MediumSlow = 4,
    Erratic = 5,
    Fluctuating = 6,
    //Below are unused but I'm a completionist
    SlightlyFast = 7,
    SlightlySlow = 8,
}

public class ExperienceLevelCalculator
{
    #region Level To EXP
    /// <summary>
    /// <see href="https://bulbapedia.bulbagarden.net/wiki/Experience#Erratic"/>
    /// </summary>
    internal static double ErraticExp(double level)
    {
        if (level < 50)
            return (Math.Pow(level, 3d) * (100d - level)) / 50d;
        if (level < 68)
            return (Math.Pow(level, 3d) * (150d - level)) / 100d;
        if (level < 98)
            return (Math.Pow(level, 3d) * Math.Floor((1911d - (10d * level)) / 3d)) / 500d;
        return (Math.Pow(level, 3d) * (160d - level)) / 100d;
    }

    /// <summary>
    /// <see href="https://bulbapedia.bulbagarden.net/wiki/Experience#Fast"/>
    /// </summary>
    internal static double FastExp(double level)
    {
        return (4d * Math.Pow(level, 3d)) / 5d;
    }

    /// <summary>
    /// <see href="https://bulbapedia.bulbagarden.net/wiki/Experience#Medium_Fast"/>
    /// </summary>
    internal static double MediumFastExp(double level)
    {
        return Math.Pow(level, 3d);
    }

    /// <summary>
    /// <see href="https://bulbapedia.bulbagarden.net/wiki/Experience#Medium_Slow"/>
    /// </summary>
    internal static double MediumSlowExp(double level)
    {
        var a = (6d / 5d) * Math.Pow(level, 3d);
        var b = 15d * Math.Pow(level, 2d);
        var c = 100d * level;
        return a - b + c - 140d;
    }

    /// <summary>
    /// <see href="https://bulbapedia.bulbagarden.net/wiki/Experience#Slow"/>
    /// </summary>
    internal static double SlowExp(double level)
    {
        return (5d * Math.Pow(level, 3d)) / 4d;
    }

    /// <summary>
    /// <see href="https://bulbapedia.bulbagarden.net/wiki/Experience#Fluctuating"/>
    /// </summary>
    internal static double FluctuatingExp(double level)
    {
        if (level < 15)
        {
            var a = Math.Floor((level + 1d) / 3d) + 24d;
            return (Math.Pow(level, 3d) * a) / 50d;
        }

        if (level < 36)
            return (Math.Pow(level, 3d) * (level + 14d)) / 50d;

        return (Math.Pow(level, 3) * (Math.Floor(level / 2d) + 32d)) / 50d;
    }

    /// <summary>
    /// <see href="https://bulbapedia.bulbagarden.net/wiki/Experience#Trivia"/>
    /// </summary>
    internal static double SlightlySlowExp(double level)
    {
        return ((3d * Math.Pow(level, 3d)) / 4d) + (20d * Math.Pow(level, 2d)) - 70d;
    }

    /// <summary>
    /// <see href="https://bulbapedia.bulbagarden.net/wiki/Experience#Trivia"/>
    /// </summary>
    internal static double SlightlyFastExp(double level)
    {
        return ((3d * Math.Pow(level, 3d)) / 4d) + (10d * Math.Pow(level, 2d)) - 30d;
    }
    #endregion

    #region EXP to Level
    internal static int SolveLevel(ExperienceGain func, int exp)
    {
        const int MAX = 100, MIN = 1;
        if (exp <= 0) return MIN;

        for(var i = MAX; i >= MIN; i--)
        {
            var next = ExpFromLevel(i, func);
            if (next < exp) return i - 1;
            if (next == exp) return i;
        }

        return MIN;
    }

    internal static double FastLevel(double exp) => Math.Pow((exp * 5d) / 4d, 1d / 3d);

    internal static double MediumFastLevel(double exp) => Math.Cbrt(exp);

    internal static double SlowLevel(double exp) => Math.Pow((exp * 4d) / 5d, 1d / 3d);
    #endregion

    /// <summary>
    /// Determine the amount of EXP needed to got to the next level
    /// </summary>
    /// <param name="level">The lower level</param>
    /// <param name="function">The experience gain function</param>
    /// <returns>The amount of EXP needed to get to the next level</returns>
    public static int ExpDelta(int level, ExperienceGain function)
    {
        if (level == 100 || level < 1) 
            return 0;

        return ExpDelta(level, level + 1, function);
    }

    /// <summary>
    /// Determine the amount of EXP needed to go from one level to the next
    /// </summary>
    /// <param name="levelA">The start level</param>
    /// <param name="levelB">The target level</param>
    /// <param name="function">The experience gain function</param>
    /// <returns>The amount of EXP needed to go between the two levels</returns>
    public static int ExpDelta(int levelA, int levelB, ExperienceGain function)
    {
        if (levelA == levelB) return 0;

        var a = ExpFromLevel(levelA, function);
        var b = ExpFromLevel(levelB, function);
        return a > b ? a - b : b - a;
    }

    /// <summary>
    /// Get the total amount of experience at the given level
    /// </summary>
    /// <param name="level">The level</param>
    /// <param name="function">The experience gain function</param>
    /// <returns>The amount of experience at the given level</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if the level is not 1 >= level <= 100</exception>
    /// <exception cref="NotImplementedException">Thrown if the experience gain function is invalid</exception>
    public static int ExpFromLevel(int level, ExperienceGain function)
    {
        if (level == 1) return 0;

        if (level < 1 || level > 100)
            throw new ArgumentOutOfRangeException(nameof(level), level, "Level must be inclusive between 1 and 100");

        var result = function switch
        {
            ExperienceGain.Erratic => ErraticExp(level),
            ExperienceGain.Fast => FastExp(level),
            ExperienceGain.MediumFast => MediumFastExp(level),
            ExperienceGain.MediumSlow => MediumSlowExp(level),
            ExperienceGain.Slow => SlowExp(level),
            ExperienceGain.Fluctuating => FluctuatingExp(level),
            ExperienceGain.SlightlySlow => SlightlySlowExp(level),
            ExperienceGain.SlightlyFast => SlightlyFastExp(level),
            _ => throw new NotImplementedException()
        };
        return (int)Math.Floor(result);
    }

    /// <summary>
    /// Get the estimated level from the given amount of EXP
    /// </summary>
    /// <param name="exp">The EXP</param>
    /// <param name="function">The experience gain function</param>
    /// <returns>The estimated level from the given EXP</returns>
    /// <exception cref="NotImplementedException">Thrown if the experience gain function is invalid</exception>
    public static int LevelFromExp(int exp, ExperienceGain function)
    {
        if (exp == 0) return 1;

        var result = function switch
        {
            ExperienceGain.Fast => FastLevel(exp),
            ExperienceGain.MediumFast => MediumFastLevel(exp),
            ExperienceGain.Slow => SlowLevel(exp),
            _ => SolveLevel(function, exp)
        };
        return (int)Math.Round(result);
    }
}
