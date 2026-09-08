using Microsoft.Extensions.Logging;
using System;
using System.Windows.Media;

namespace EvenBetterFastSim.Logging;

/// <summary>
/// Used for metadata of a string that is logged
/// </summary>
public class LoggedString
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public LogLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public Brush TextColor => GetColor();
    private Brush GetColor()
    {
        return Level switch
        {
            LogLevel.Trace => Brushes.Black,
            LogLevel.Debug => Brushes.Black,
            LogLevel.Information => Brushes.Black,
            LogLevel.Warning => Brushes.Orange,
            LogLevel.Error => Brushes.Red,
            LogLevel.Critical => Brushes.DarkRed,
            LogLevel.None => Brushes.Black,
            _ => throw new ArgumentOutOfRangeException()
        };
    }
    public override string ToString()
    {
        return $"[{Timestamp:HH:mm:ss.fff}] [{Level}] {Message}";
    }
}