namespace DndSharp.PokeDex;

public record class PokemonIVs(
    int Hp,
    int Attack,
    int Defense,
    int SpecialAttack,
    int SpecialDefense,
    int Speed) : PokemonStats(Hp, Attack, Defense, SpecialAttack, SpecialDefense, Speed)
{
    public const int MIN_STAT = 0;
    public const int MAX_STAT = 31;

    public override void Validate(List<string> errors)
    {
        static void ValidateStat(int stat, string name, List<string> errors)
        {
            if (stat < MIN_STAT || stat > MAX_STAT)
                errors.Add($"{name} IVs must be between {MIN_STAT} and {MAX_STAT}.");
        }

        ValidateStat(Hp, nameof(Hp), errors);
        ValidateStat(Attack, nameof(Attack), errors);
        ValidateStat(Defense, nameof(Defense), errors);
        ValidateStat(SpecialAttack, nameof(SpecialAttack), errors);
        ValidateStat(SpecialDefense, nameof(SpecialDefense), errors);
        ValidateStat(Speed, nameof(Speed), errors);
    }

    public static readonly new PokemonIVs Empty = new(0, 0, 0, 0, 0, 0);
}