namespace DndSharp.PokeDex.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class PkResourceAttribute(string name) : Attribute
{
    public string Name { get; } = $"/api/v2/{name}/";
}
