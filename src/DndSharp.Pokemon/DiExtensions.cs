namespace DndSharp.Pokemon;

public static class DiExtensions
{
    public static IServiceCollection AddPokemonServices(this IServiceCollection services)
    {
        return services
            .AddSingleton<IPokemonTranslationService, PokemonTranslationService>();
    }
}
