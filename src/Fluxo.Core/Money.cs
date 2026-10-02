using System.Globalization;

namespace Fluxo.Core;

/// <summary>
/// Valor em centavos. Dinheiro nunca passa por double: 0,1 + 0,2 em ponto flutuante não dá 0,3,
/// e num extrato de um ano esse erro aparece no saldo.
/// </summary>
public readonly record struct Money(long Cents) : IComparable<Money>
{
    public static readonly Money Zero = new(0);

    public bool IsNegative => Cents < 0;
    public bool IsZero => Cents == 0;
    public Money Abs() => new(Math.Abs(Cents));

    public static Money operator +(Money a, Money b) => new(checked(a.Cents + b.Cents));
    public static Money operator -(Money a, Money b) => new(checked(a.Cents - b.Cents));
    public static Money operator -(Money a) => new(-a.Cents);
    public static bool operator <(Money a, Money b) => a.Cents < b.Cents;
    public static bool operator >(Money a, Money b) => a.Cents > b.Cents;
    public static bool operator <=(Money a, Money b) => a.Cents <= b.Cents;
    public static bool operator >=(Money a, Money b) => a.Cents >= b.Cents;

    public int CompareTo(Money other) => Cents.CompareTo(other.Cents);

    public static Money FromDecimal(decimal value) =>
        new((long)decimal.Round(value * 100m, 0, MidpointRounding.AwayFromZero));

    public decimal ToDecimal() => Cents / 100m;

    /// <summary>"R$ 1.234,56" ou "-R$ 12,30", igual em qualquer navegador.</summary>
    public override string ToString() => Core.PtBr.Currency(Cents);

    /// <summary>
    /// Lê valor nos formatos que aparecem em extrato brasileiro e em exportação de sistema:
    /// "1.234,56", "1234,56", "-12,30", "R$ 10,00", "(10,00)" (negativo de contabilidade),
    /// "1234.56" e "1,234.56". A regra pra decidir qual é o separador decimal é a última
    /// vírgula ou ponto do texto, desde que tenha 1 ou 2 dígitos depois dele.
    /// </summary>
    public static bool TryParse(string? text, out Money money)
    {
        money = Zero;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var s = text.Trim().Replace("R$", "", StringComparison.OrdinalIgnoreCase).Replace(" ", "").Replace(" ", "");
        var negative = false;
        if (s.StartsWith('(') && s.EndsWith(')')) { negative = true; s = s[1..^1]; }
        if (s.StartsWith('-')) { negative = !negative; s = s[1..]; }
        else if (s.StartsWith('+')) s = s[1..];
        if (s.EndsWith('-')) { negative = !negative; s = s[..^1]; } // "12,30-" de alguns bancos
        if (s.Length == 0) return false;

        var lastSep = s.LastIndexOfAny([',', '.']);
        string integerPart, fraction;
        if (lastSep >= 0 && s.Length - lastSep - 1 is 1 or 2)
        {
            integerPart = s[..lastSep];
            fraction = s[(lastSep + 1)..].PadRight(2, '0');
        }
        else
        {
            integerPart = s;
            fraction = "00";
        }

        integerPart = integerPart.Replace(".", "").Replace(",", "");
        if (integerPart.Length == 0) integerPart = "0";
        if (!integerPart.All(char.IsAsciiDigit) || !fraction.All(char.IsAsciiDigit)) return false;
        if (!long.TryParse(integerPart, NumberStyles.None, CultureInfo.InvariantCulture, out var whole)) return false;

        var cents = checked(whole * 100 + long.Parse(fraction, CultureInfo.InvariantCulture));
        money = new Money(negative ? -cents : cents);
        return true;
    }

    public static Money Parse(string text) =>
        TryParse(text, out var m) ? m : throw new FormatException($"Valor inválido: '{text}'.");
}
