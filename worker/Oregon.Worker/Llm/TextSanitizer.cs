using System.Text;

namespace Oregon.Worker.Llm;

/// <summary>Normalizes model output before it reaches game state: strips control characters and bounds length.</summary>
internal static class TextSanitizer
{
    public static string Clean(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        StringBuilder builder = new(Math.Min(value.Length, maxLength));
        int newlines = 0;
        foreach (char c in value.Trim())
        {
            if (c == '\n')
            {
                // Collapse runs of blank lines; keep paragraph breaks.
                if (++newlines <= 2)
                {
                    builder.Append(c);
                }

                continue;
            }

            newlines = 0;
            if (!char.IsControl(c))
            {
                builder.Append(c);
            }

            if (builder.Length >= maxLength)
            {
                break;
            }
        }

        return builder.ToString().Trim();
    }
}
