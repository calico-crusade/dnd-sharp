namespace DndSharp.Cli;

public class LogWriter(
    ILogger _logger,
    LogLevel _level = LogLevel.Information) : TextWriter
{
    public override Encoding Encoding { get; } = Encoding.UTF8;

    private readonly StringBuilder _currentLine = new();

    public override void Write(char value)
    {
        if (value == '\r') return;

        if (value == '\n')
        {
            _logger.Log(_level, "{data}", _currentLine.ToString());
            _currentLine.Clear();
            return;
        }

        _currentLine.Append(value);
    }

    public override void Flush()
    {
        if (_currentLine.Length > 0)
        {
            _logger.Log(_level, "{data}", _currentLine.ToString());
            _currentLine.Clear();
        }
    }
}
