using Discord;
using DndSharp.PokeDex;
using DndSharp.PokeDex.Models.Pokemon;
using DndSharp.Pokemon;

namespace DndSharp.Discord;

public sealed class PokemonCommandService(IPokeApiService poke, IPokemonTranslationService translation)
{
    public async Task<PkPokemon?> FindAsync(string query, CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > 100)
            throw new ArgumentException("Specify a Pokémon name or Pokédex number (up to 100 characters).");
        query = query.Trim().ToLowerInvariant().Replace(' ', '-');
        var index = await poke.Index<PkPokemon>(token: token);
        if (index is null)
            throw new ArgumentException("Pokémon data could not be loaded. Check the bot's PokeApi:BasePath configuration.");
        if (int.TryParse(query, out var id))
            return id > 0 ? await poke.Fetch<PkPokemon>(id, token: token) : null;

        // Resolve exact names from the existing index instead of guessing a fuzzy match.
        var match = index.Results.FirstOrDefault(item =>
            string.Equals(item.Name, query, StringComparison.OrdinalIgnoreCase));
        return match is null ? null : await poke.Fetch<PkPokemon>(match.Url, token: token);
    }

    public async Task<PokemonCharacter?> TranslateAsync(PkPokemon pokemon, int level, CancellationToken token = default)
    {
        if (level is < 1 or > 100)
            throw new ArgumentException("Level must be between 1 and 100.");
        var character = await translation.Translate(pokemon.Id, token: token);
        if (character is not null) character.Level = level;
        return character;
    }

    public static string? ArtworkUrl(PkPokemon pokemon) =>
        pokemon.Sprites.Other.GetValueOrDefault("official-artwork")?.FrontDefault ?? pokemon.Sprites.FrontDefault;

    public static Embed RawStats(PkPokemon pokemon)
    {
        var stats = new PokemonBaseStats(pokemon);
        var embed = new EmbedBuilder()
            .WithTitle($"#{pokemon.Id:0000} · {pokemon.Name}")
            .WithDescription(string.Join(" / ", pokemon.Types.OrderBy(item => item.Slot).Select(item => item.Resource.Name)))
            .WithColor(new Color(0xD7B85B))
            .WithFooter("Species base stats from PokéAPI; no level, nature, EV, or IV modifiers.");
        var artwork = ArtworkUrl(pokemon);
        if (!string.IsNullOrEmpty(artwork)) embed.WithThumbnailUrl(artwork);
        embed.AddField("HP", stats.Hp, true)
            .AddField("Attack", stats.Attack, true)
            .AddField("Defense", stats.Defense, true)
            .AddField("Sp. Attack", stats.SpecialAttack, true)
            .AddField("Sp. Defense", stats.SpecialDefense, true)
            .AddField("Speed", stats.Speed, true)
            .AddField("Base stat total", stats.Hp + stats.Attack + stats.Defense + stats.SpecialAttack + stats.SpecialDefense + stats.Speed);
        return embed.Build();
    }
}
