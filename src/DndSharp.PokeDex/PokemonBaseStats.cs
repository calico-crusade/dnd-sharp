using CardboardBox.Extensions;

namespace DndSharp.PokeDex;

using Models.Pokemon;

public class PokemonBaseStats(PkPokemon _pokemon) : IPokemonStats
{
    private int? _hp, _atk, _def, _spa, _spd, _spe;

    public int Hp => _hp ??= Stat("hp");

    public int Attack => _atk ??= Stat("attack");

    public int Defense => _def ??= Stat("defense");

    public int SpecialAttack => _spa ??= Stat("special-attack");

    public int SpecialDefense => _spd ??= Stat("special-defense");

    public int Speed => _spe ??= Stat("speed");

    public void Recompute()
    {
        _hp = _atk = _def = _spa = _spd = _spe = null;
    }

    public virtual void Validate(List<string> errors) 
    {
        if (_pokemon is null)
            errors.Add("PokemonBaseStats: Pokemon cannot be null");
    }

    public virtual (bool, string[] errors) Validate()
    {
        List<string> errors = [];
        Validate(errors);
        return (errors.Count == 0, [.. errors]);
    }

    private int Stat(string stat)
    {
        return _pokemon?.Stats.FirstOrDefault(t => t.Resource.Name.EqualsIc(stat))?.BaseStat ?? -1;
    }
}
