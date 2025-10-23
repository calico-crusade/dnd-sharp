namespace DndSharp.PokeDex;

public enum NatureMod
{
    Neutral = 0,
    Positive = 1,
    Negative = 2
}

public static class Calculations
{
    public const int IV_MIN = 0;
    public const int IV_MAX = 31;
    public const int EV_MIN = 0;
    public const int EV_MAX = 252;
    public const int LEVEL_MIN = 1;
    public const int LEVEL_MAX = 100;

    public static int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }

    public static int ClampLevel(int level) => Clamp(level, LEVEL_MIN, LEVEL_MAX);

    public static int ClampEv(int ev) => Clamp(ev, EV_MIN, EV_MAX);

    public static int ClampIv(int iv) => Clamp(iv, IV_MIN, IV_MAX);

    public static int Hp(int @base, int level, int ev = 0, int iv = 0)
    {
        level = ClampLevel(level);
        ev = ClampEv(ev);
        iv = ClampIv(iv);
        var value = Math.Floor(0.01 * (2 * @base + iv + Math.Floor(0.25 * ev)) * level) + level + 10;
        return (int)value;
    }

    public static int HpMax(int @base, int level = LEVEL_MAX)
    {
        return Hp(@base, level, EV_MAX, IV_MAX);
    }

    public static int HpMin(int @base, int level = LEVEL_MIN)
    {
        return Hp(@base, level, EV_MIN, IV_MIN);
    }

    public static int Stat(int @base, int level, NatureMod nature = NatureMod.Neutral, int ev = 0, int iv = 0)
    {
        level = ClampLevel(level);
        ev = ClampEv(ev);
        iv = ClampIv(iv);
        var nm = nature switch
        {
            NatureMod.Positive => 1.1,
            NatureMod.Negative => 0.9,
            _ => 1
        };
        var value = (Math.Floor(0.01 * (2 * @base + iv + Math.Floor(0.25 * ev)) * level) + 5) * nm;
        return (int)Math.Floor(value);
    }

    public static int StatMax(int @base, int level = LEVEL_MAX, NatureMod nature = NatureMod.Positive)
    {
        return Stat(@base, level, nature, EV_MAX, IV_MAX);
    }

    public static int StatMin(int @base, int level = LEVEL_MIN, NatureMod nature = NatureMod.Negative)
    {
        return Stat(@base, level, nature, EV_MIN, IV_MIN);
    }
}
