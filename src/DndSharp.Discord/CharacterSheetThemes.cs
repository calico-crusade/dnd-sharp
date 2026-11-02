using SkiaSharp;

namespace DndSharp.Discord;

/// <summary>Palette and label for one selectable character sheet theme.</summary>
public sealed record CharacterSheetTheme(string Id, string Label, SKColor Background, SKColor Paper,
    SKColor Header, SKColor Ink, SKColor Muted, SKColor InverseInk, SKColor Shield, SKColor Border, SKColor EmptySlot,
    SKColor? PrimaryAccent = null, SKColor? SecondaryAccent = null);

public static class CharacterSheetThemes
{
    public static IReadOnlyList<CharacterSheetTheme> All { get; } = Array.AsReadOnly(new[]
    {
        new CharacterSheetTheme("light", "Light", SKColor.Parse("11151D"), SKColor.Parse("F7F7F8"),
            SKColor.Parse("D5D8DC"), SKColor.Parse("20232C"), SKColor.Parse("444C59"), SKColor.Parse("F7F7F8"),
            SKColor.Parse("F8F9FB"), SKColor.Parse("9CA6B5"), SKColor.Parse("CDD1D8")),
        new CharacterSheetTheme("dark", "Dark", SKColor.Parse("101217"), SKColor.Parse("252A34"),
            SKColor.Parse("353C49"), SKColor.Parse("F1F2F4"), SKColor.Parse("CAD1DC"), SKColor.Parse("202630"),
            SKColor.Parse("252A34"), SKColor.Parse("AAB5C3"), SKColor.Parse("515C6B")),
        new CharacterSheetTheme("princess", "Princess", SKColor.Parse("100B1A"), SKColor.Parse("21162E"),
            SKColor.Parse("37214A"), SKColor.Parse("69E8FF"), SKColor.Parse("F3E3F4"), SKColor.Parse("181022"),
            SKColor.Parse("21162E"), SKColor.Parse("FF48C4"), SKColor.Parse("61446C"),
            SKColor.Parse("FF48C4"), SKColor.Parse("27DFFF"))
    });

    public static CharacterSheetTheme? Find(string id) => All.FirstOrDefault(theme => theme.Id == id);
}
