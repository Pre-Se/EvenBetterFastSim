using System;
using System.Globalization;
using System.Windows.Data;

namespace EvenBetterFastSim.Helpers;

public class MessageHeaderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string header)
            return value ?? string.Empty;

        var result = header;

        // Strip leading timestamp (HH:mm:ss.fff ) from control messages
        if (result.Length > 12 && result[2] == ':' && result[5] == ':' && result[8] == '.')
            result = result[13..];

        // Strip "Sent " / "Received " (data messages w/ space)
        // or "Sent" / "Received" (control messages attached to next word)
        if (result.StartsWith("Sent "))
            result = result[5..];
        else if (result.StartsWith("Received "))
            result = result[9..];
        else if (result.StartsWith("Sent", StringComparison.Ordinal))
            result = result[4..];
        else if (result.StartsWith("Received", StringComparison.Ordinal))
            result = result[8..];

        return result.Trim();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
