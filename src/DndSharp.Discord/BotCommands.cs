using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace DndSharp.Discord;

public sealed class BotCommands(
    DiscordRollService rolls,
    PokemonCommandService pokemonService,
    IPokemonCardRenderer cards,
    DiscordBot bot,
    ILogger<BotCommands> logger) : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("roll", "Roll custom dice text or a preset die.")]
    public Task Roll(
        [Summary(description: "Custom dice, e.g. 2d6+3 or 2d6 (fire) + 1d8 (cold)")] string? dice = null,
        [Summary(description: "Choose a standard die instead of custom dice text")] PresetDie? preset = null) =>
        Execute(async token =>
        {
            var result = await rolls.RollAsync(dice, preset, token: token);
            await ModifyOriginalResponseAsync(message =>
            {
                message.Content = result;
                message.AllowedMentions = AllowedMentions.None;
            });
        });

    [SlashCommand("poke", "Show a Pokémon's raw species base stats.")]
    public Task Poke([Summary(description: "Pokémon name or Pokédex number")] string pokemon) =>
        Execute(async token =>
        {
            var mon = await Find(pokemon, token);
            if (mon is null) return;
            await ModifyOriginalResponseAsync(message => message.Embed = PokemonCommandService.RawStats(mon));
        });

    [SlashCommand("poke-dnd", "Generate a Pokémon-style card with D&D ability scores.")]
    public Task PokeDnd(
        [Summary(description: "Pokémon name or Pokédex number")] string pokemon,
        [Summary(description: "Pokémon level, from 1 to 100"), MinValue(1), MaxValue(100)] int level) =>
        Execute(async token =>
        {
            var mon = await Find(pokemon, token);
            if (mon is null) return;
            var character = await pokemonService.TranslateAsync(mon, level, token)
                ?? throw new InvalidOperationException("Pokémon translation failed.");
            using var card = await cards.RenderAsync(character, token);
            await ModifyOriginalResponseAsync(message =>
            {
                message.Content = $"**{character.Name} · Level {character.Level}**\n" +
                    "Generated with randomized nature, IVs, and EVs from the existing translator.";
                message.Attachments = new[] { new FileAttachment(card, $"pokemon-{mon.Id}-level-{level}.png") };
                message.AllowedMentions = AllowedMentions.None;
            });
        });

    private async Task<PokeDex.Models.Pokemon.PkPokemon?> Find(string query, CancellationToken token)
    {
        var mon = await pokemonService.FindAsync(query, token);
        if (mon is null)
            await ModifyOriginalResponseAsync(message => message.Content =
                "Pokémon not found in the configured PokéAPI data. Try an exact name such as pikachu, a form such as raichu-alola, or a Pokédex number.");
        return mon;
    }

    private async Task Execute(Func<CancellationToken, Task> action)
    {
        await DeferAsync();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(bot.StoppingToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        try
        {
            await action(timeout.Token);
        }
        catch (ArgumentException ex)
        {
            await ModifyOriginalResponseAsync(message => message.Content = ex.Message);
        }
        catch (OperationCanceledException)
        {
            await ModifyOriginalResponseAsync(message => message.Content = "The command timed out or the bot is shutting down. Please try again.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Discord command failed for interaction {Id}.", Context.Interaction.Id);
            await ModifyOriginalResponseAsync(message => message.Content = "Could not complete the command. Please try again; the bot logs have the details.");
        }
    }
}
