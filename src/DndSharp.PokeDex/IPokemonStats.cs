namespace DndSharp.PokeDex;

public interface IPokemonStats
{
    int Hp { get; }

    int Attack { get; }

    int Defense { get; }

    int SpecialAttack { get; }

    int SpecialDefense { get; }

    int Speed { get; }

    void Validate(List<string> errors);
}
