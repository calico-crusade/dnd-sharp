namespace DndSharp.PokeDex;

public static class DiExtensions
{
    public static IServiceCollection AddPokeApi(this IServiceCollection resolver)
    {
        return resolver.AddSingleton<IPokeApiService, PokeApiService>();
    }
}
