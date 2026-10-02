using Fluxo.Core;

namespace Fluxo.Web.Components;

/// <summary>Atalhos de exibição. Tudo passa por <see cref="PtBr"/>, nunca pela cultura do navegador.</summary>
public static class Fmt
{
    public static string Brl(Money m) => m.ToString();

    /// <summary>Rótulo curto pra eixo de gráfico: "R$ 5,2 mil", "R$ 800".</summary>
    public static string Compact(double reais) => reais >= 1000
        ? $"R$ {PtBr.Number(reais / 1000, 1)} mil"
        : $"R$ {PtBr.Number(reais)}";

    public static string Percent(double share) => $"{PtBr.Number(share * 100)}%";

    public static string MonthName(DateOnly d) => PtBr.MonthYear(d.Year, d.Month);

    public static string Date(DateOnly d) => $"{d.Day:00}/{d.Month:00}";
}
