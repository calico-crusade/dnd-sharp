namespace DndSharp.Cli;

public static class TableGenerator
{
    private const string SEP = " | ";
    private const char RSEP = '-';

    public static void GenerateTable(this TextWriter writer, string[] cols, string[] rows, string[][] data, string separator = SEP, char rowSep = RSEP)
    {
        if (data.Length != rows.Length || data[0].Length != cols.Length)
            throw new ArgumentException("Data dimensions do not match header lengths.");

        var maxXC = cols.Max(t => t.Length);
        var maxY = rows.Max(t => t.Length);
        var maxXD = data.SelectMany(t => t).Max(t => t.Length);
        var maxX = Math.Max(maxXC, maxXD);

        //Writer header
        writer.Write("".PadLeft(maxY, ' '));
        writer.Write(separator);
        for (int i = 0; i < cols.Length; i++)
        {
            writer.Write(cols[i].PadCenter(maxX));
            if (i < cols.Length - 1)
                writer.Write(separator);
        }
        writer.WriteLine();

        //Writer separator
        writer.Write("".PadLeft(maxY, rowSep));
        writer.Write(separator);
        for (int i = 0; i < cols.Length; i++)
        {
            writer.Write("".PadCenter(maxX, rowSep));
            if (i < cols.Length - 1)
                writer.Write(separator);
        }
        writer.WriteLine();

        //Writer rows
        for (int y = 0; y < rows.Length; y++)
        {
            writer.Write(rows[y].PadCenter(maxY));
            writer.Write(separator);
            for (int x = 0; x < cols.Length; x++)
            {
                writer.Write(data[y][x].PadCenter(maxX));
                if (x < cols.Length - 1)
                    writer.Write(separator);
            }
            writer.WriteLine();
        }

        writer.Flush();
    }

    public static string GenerateTable(string[] cols, string[] rows, string[][] data, string separator = SEP, char rowSep = RSEP)
    {
        using var sw = new StringWriter();
        GenerateTable(sw, cols, rows, data, separator, rowSep);
        return sw.ToString();
    }

    public static void GenerateTable(this ILogger logger, string[] cols, string[] rows, string[][] data, string separator = SEP, char rowSep = RSEP, LogLevel level = LogLevel.Information)
    {
        using var sw = new LogWriter(logger, level);
        GenerateTable(sw, cols, rows, data, separator, rowSep);
    }

    public static void GenerateTable<T>(this TextWriter writer, string[] cols, string[] rows, T[][] data, Func<T, string>? selector = null, string separator = SEP, char rowSep = RSEP)
    {
        selector ??= (t) => t?.ToString() ?? string.Empty;
        var strData = new string[rows.Length][];
        for (int y = 0; y < rows.Length; y++)
        {
            var row = new string[cols.Length];
            for (int x = 0; x < cols.Length; x++)
                row[x] = selector(data[y][x]);
            strData[y] = row;
        }
        GenerateTable(writer, cols, rows, strData, separator, rowSep);
    }

    public static string GenerateTable<T>(string[] cols, string[] rows, T[][] data, Func<T, string>? selector = null, string separator = SEP, char rowSep = RSEP)
    {
        using var sw = new StringWriter();
        GenerateTable(sw, cols, rows, data, selector, separator, rowSep);
        return sw.ToString();
    }

    public static void GenerateTable<T>(this ILogger logger, string[] cols, string[] rows, T[][] data, Func<T, string>? selector = null, string separator = SEP, char rowSep = RSEP, LogLevel level = LogLevel.Information)
    {
        using var sw = new LogWriter(logger, level);
        GenerateTable(sw, cols, rows, data, selector, separator, rowSep);
    }
}
