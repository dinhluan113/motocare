using System.Text.RegularExpressions;

namespace MotoCare.Api.Infrastructure;

public static partial class Normalize
{
    public static string Phone(string value) =>
        DigitsOnly().Replace(value ?? string.Empty, string.Empty);

    public static string LicensePlate(string value) =>
        NonAlphaNumeric().Replace(value ?? string.Empty, string.Empty).ToUpperInvariant();

    [GeneratedRegex("[^0-9]")]
    private static partial Regex DigitsOnly();

    [GeneratedRegex("[^A-Za-z0-9]")]
    private static partial Regex NonAlphaNumeric();
}
