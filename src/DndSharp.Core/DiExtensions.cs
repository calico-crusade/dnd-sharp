using CardboardBox;
using CardboardBox.Json;
using FuzzySharp.Extractor;
using FuzzySharp.SimilarityRatio;
using FuzzySharp.SimilarityRatio.Scorer;
using FuzzySharp.SimilarityRatio.Scorer.Composite;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace DndSharp.Core;

public static class DiExtensions
{
    private const int DEFAULT_FUZZY_SCORE_CUTOFF = 50;

    public static IServiceCollection AddCoreServices(this IServiceCollection services)
    {
        return services
            .AddJson()
            .AddAppSettings()
            .AddSerilog();
    }

    public static string TrimStart(this string input, string strip, StringComparison compare = StringComparison.InvariantCulture)
    {
        if (input.StartsWith(strip, compare))
            return input[strip.Length..];
        return input;
    }

    public static string PadCenter(this string str, int length, char paddingChar = ' ')
    {
        int spaces = length - str.Length;
        int padLeft = spaces / 2 + str.Length;
        return str.PadLeft(padLeft, paddingChar).PadRight(length, paddingChar);
    }

    public static IEnumerable<Type> AllTypes(this Assembly assmebly)
    {
        List<Assembly> assemblies = [assmebly];
        var loaded = new HashSet<string>();
        var typesLoaded = new HashSet<string>();

        while(assemblies.Count > 0)
        {
            var assembly = assemblies[0];
            assemblies.RemoveAt(0);

            if (!loaded.Add(assembly.FullName!))
                continue;

            var types = assembly.GetTypes();
            foreach(var type in types)
            {
                if (!typesLoaded.Add(type.FullName!))
                    continue;

                yield return type;
            }
            
            var references = assembly.GetReferencedAssemblies();
            foreach (var reference in references)
            {
                if (loaded.Contains(reference.FullName!))
                    continue;

                try
                {
                    var asm = Assembly.Load(reference.FullName!);
                    assemblies.Add(asm);
                }
                catch { }
            }
        }
    }

    public static IEnumerable<Type> AllTypesOf(this Type type)
    {
        foreach(var item in Assembly.GetExecutingAssembly().AllTypes())
            if (type.IsAssignableFrom(item) && item != type)
                yield return item;
    }

    public static IEnumerable<Type> AllTypesWithAttribute(this Type attribute)
    {
        foreach (var item in Assembly.GetExecutingAssembly().AllTypes())
            if (item.GetCustomAttribute(attribute, true) is not null)
                yield return item;
    }

    public static IEnumerable<(Type type, T attribute)> AllTypesWithAttribute<T>(this Assembly assmebly)
        where T : Attribute
    {
        foreach(var item in assmebly.AllTypes())
        {
            var attr = item.GetCustomAttribute<T>();
            if (attr is null) continue;

            yield return (item, attr);
        }
    }

    public static async IAsyncEnumerable<ExtractedResult<string>> Search(this IAsyncEnumerable<string> items, 
        string term, int? cutoff = null, IRatioScorer? scorer = null)
    {
        cutoff ??= DEFAULT_FUZZY_SCORE_CUTOFF;
        scorer ??= ScorerCache.Get<WeightedRatioScorer>();

        int index = 0;
        await foreach (var item in items)
        {
            int ratio = scorer.Score(term, item);
            if (ratio >= cutoff.Value)
                yield return new ExtractedResult<string>(item, ratio, index);

            index++;
        }
    }

    public static async IAsyncEnumerable<ExtractedResult<T>> Search<T>(this IAsyncEnumerable<T> items, 
        string term, Func<T, string> key, int? cutoff = null, IRatioScorer? scorer = null)
    {
        cutoff ??= DEFAULT_FUZZY_SCORE_CUTOFF;
        scorer ??= ScorerCache.Get<WeightedRatioScorer>();

        int index = 0;
        await foreach(var item in items)
        {
            int ratio = scorer.Score(term, key(item));
            if (ratio >= cutoff.Value)
                yield return new ExtractedResult<T>(item, ratio, index);

            index++;
        }
    }
}
