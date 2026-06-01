using System.Text;
using System.Text.RegularExpressions;

public static class SimpleMarkdownFormatter
{
    private static readonly Regex BoldRegex = new Regex(@"\*\*(.+?)\*\*", RegexOptions.Compiled);

    public static string Format(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return string.Empty;
        }

        var normalized = rawText.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n');
        var builder = new StringBuilder();

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index] ?? string.Empty;
            var trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                if (builder.Length > 0 && !EndsWithDoubleLineBreak(builder))
                {
                    builder.Append("\n\n");
                }

                continue;
            }

            if (TryAppendHeading(builder, trimmed))
            {
                continue;
            }

            if (IsBulletLine(trimmed))
            {
                builder.Append("• ");
                builder.Append(FormatInline(trimmed.Substring(2).Trim()));
                builder.Append('\n');
                continue;
            }

            builder.Append(FormatInline(trimmed));

            if (index < lines.Length - 1)
            {
                builder.Append('\n');
            }
        }

        return builder.ToString().Trim();
    }

    private static bool TryAppendHeading(StringBuilder builder, string line)
    {
        var level = 0;

        while (level < line.Length && line[level] == '#')
        {
            level++;
        }

        if (level == 0 || level >= line.Length || line[level] != ' ')
        {
            return false;
        }

        var text = line.Substring(level + 1).Trim();

        if (text.Length == 0)
        {
            return false;
        }

        var size = level <= 1
            ? 24
            : level == 2
                ? 21
                : 19;

        builder.Append("<b><size=");
        builder.Append(size);
        builder.Append('>');
        builder.Append(FormatInline(text));
        builder.Append("</size></b>\n");
        return true;
    }

    private static bool IsBulletLine(string line)
    {
        return line.StartsWith("- ") || line.StartsWith("* ");
    }

    private static string FormatInline(string text)
    {
        var escaped = EscapeRichText(text ?? string.Empty);
        return BoldRegex.Replace(escaped, "<b>$1</b>");
    }

    private static string EscapeRichText(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    private static bool EndsWithDoubleLineBreak(StringBuilder builder)
    {
        return builder.Length >= 2
               && builder[builder.Length - 1] == '\n'
               && builder[builder.Length - 2] == '\n';
    }
}
