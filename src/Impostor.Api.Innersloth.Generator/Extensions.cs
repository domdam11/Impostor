using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;

namespace Impostor.Api.Innersloth.Generator;

internal static class Extensions
{
    public static string NormalizePath(this string path)
    {
        return path.Replace("\\", "/");
    }

    public static bool TryTrimStart(this string text, string value, [NotNullWhen(true)] out string? result)
    {
        if (text.StartsWith(value))
        {
            result = text[value.Length..];
            return true;
        }

        result = null;
        return false;
    }

    public static string ToCSharpString(this Vector2 value)
    {
        return $"new Vector2({value.X.ToString(CultureInfo.InvariantCulture)}f, {value.Y.ToString(CultureInfo.InvariantCulture)}f)";
    }
    public static string ToCSharpString(this Vector2[] value)
    {
        var output = "";
        var length = value.Length;
        for (var i = 0; i < length; i++)
        {
            output += value[i].ToCSharpString();
            if (i < length - 1)
            {
                output += ", ";
            }
        }

        return "new Vector2[] { " + output +"}";
    }
}
