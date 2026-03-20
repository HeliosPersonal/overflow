using System.Text.RegularExpressions;

namespace Overflow.SearchService.Extensions;

internal static partial class HtmlHelpers
{
    public static string StripTags(string html) => HtmlTagRegex().Replace(html, string.Empty);

    [GeneratedRegex("<.*?>")]
    private static partial Regex HtmlTagRegex();
}