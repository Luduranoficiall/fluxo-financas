using System.Globalization;
using System.Text;

namespace Fluxo.Core;

/// <summary>
/// Formatação brasileira feita à mão, sem depender dos dados de idioma do sistema. O Blazor
/// WebAssembly publicado só baixa os dados do idioma do navegador de quem abre: com
/// <c>CultureInfo("pt-BR")</c>, quem usa o navegador em inglês veria "BRL6,300.00" e "October".
/// </summary>
public static class PtBr
{
    private static readonly string[] Months =
        ["janeiro", "fevereiro", "março", "abril", "maio", "junho", "julho", "agosto", "setembro", "outubro", "novembro", "dezembro"];

    public static string MonthName(int month) => Months[month - 1];

    /// <summary>"set/26".</summary>
    public static string ShortMonth(int year, int month) => $"{Months[month - 1][..3]}/{year % 100:00}";

    /// <summary>"Setembro de 2026".</summary>
    public static string MonthYear(int year, int month) =>
        $"{char.ToUpperInvariant(Months[month - 1][0])}{Months[month - 1][1..]} de {year}";

    /// <summary>"R$ 1.234,56" e "-R$ 12,30".</summary>
    public static string Currency(long cents)
    {
        var negative = cents < 0;
        var abs = negative ? -(decimal)cents : cents;
        var whole = (long)(abs / 100);
        var fraction = (long)(abs % 100);
        return $"{(negative ? "-" : "")}R$ {Group(whole)},{fraction:00}";
    }

    /// <summary>Número com vírgula decimal e ponto de milhar: 1234.5 com 1 casa vira "1.234,5".</summary>
    public static string Number(double value, int decimals = 0)
    {
        var rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        var negative = rounded < 0;
        var text = Math.Abs(rounded).ToString("F" + decimals, CultureInfo.InvariantCulture);
        var parts = text.Split('.');
        var result = Group(long.Parse(parts[0], CultureInfo.InvariantCulture));
        if (parts.Length > 1 && parts[1].TrimEnd('0').Length > 0) result += "," + parts[1].TrimEnd('0');
        return (negative ? "-" : "") + result;
    }

    private static string Group(long value)
    {
        var digits = value.ToString(CultureInfo.InvariantCulture);
        var sb = new StringBuilder(digits.Length + digits.Length / 3);
        for (var i = 0; i < digits.Length; i++)
        {
            if (i > 0 && (digits.Length - i) % 3 == 0) sb.Append('.');
            sb.Append(digits[i]);
        }
        return sb.ToString();
    }
}
