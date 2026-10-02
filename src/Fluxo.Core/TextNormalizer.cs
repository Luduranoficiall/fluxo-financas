using System.Globalization;
using System.Text;

namespace Fluxo.Core;

public static class TextNormalizer
{
    /// <summary>Minúsculo, sem acento e com espaço único: "Histórico  PIX" vira "historico pix".</summary>
    public static string Normalize(string text)
    {
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        var lastWasSpace = true;
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace) sb.Append(' ');
                lastWasSpace = true;
                continue;
            }
            sb.Append(char.ToLowerInvariant(c));
            lastWasSpace = false;
        }
        return sb.ToString().TrimEnd();
    }
}
