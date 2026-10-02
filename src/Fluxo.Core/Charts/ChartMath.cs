using System.Globalization;

namespace Fluxo.Core.Charts;

public sealed record DonutSlice(string Path, string Color, double StartAngle, double EndAngle);

/// <summary>
/// Geometria dos gráficos, sem biblioteca. A tela só desenha o SVG que sai daqui, e por isso
/// dá pra testar a matemática sem abrir navegador.
/// </summary>
public static class ChartMath
{
    /// <summary>
    /// Escala "bonita" pro eixo: em vez de 0, 1.237, 2.474... sai 0, 1.000, 2.000, 3.000. É o
    /// algoritmo clássico de "nice numbers" (Heckbert): passo é 1, 2, 5 ou 10 vezes uma
    /// potência de 10.
    /// </summary>
    public static (double Max, double Step) NiceScale(double maxValue, int maxTicks = 5)
    {
        if (maxValue <= 0) return (1, 1);
        var range = NiceNumber(maxValue, round: false);
        var step = NiceNumber(range / (maxTicks - 1), round: true);
        var niceMax = Math.Ceiling(maxValue / step) * step;
        return (niceMax, step);
    }

    private static double NiceNumber(double value, bool round)
    {
        var exponent = Math.Floor(Math.Log10(value));
        var fraction = value / Math.Pow(10, exponent);
        double nice = round
            ? fraction < 1.5 ? 1 : fraction < 3 ? 2 : fraction < 7 ? 5 : 10
            : fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10;
        return nice * Math.Pow(10, exponent);
    }

    /// <summary>
    /// Fatias de rosca em SVG (arcos). Fatia de 100% vira dois arcos de 180°, porque um arco SVG
    /// com início e fim no mesmo ponto não desenha nada.
    /// </summary>
    public static IReadOnlyList<DonutSlice> Donut(IReadOnlyList<(double Value, string Color)> parts, double cx, double cy, double rOuter, double rInner)
    {
        var total = parts.Sum(p => Math.Max(p.Value, 0));
        var slices = new List<DonutSlice>();
        if (total <= 0) return slices;

        var angle = 0.0;
        foreach (var (value, color) in parts)
        {
            if (value <= 0) continue;
            var sweep = value / total * 360.0;
            var end = angle + sweep;
            var path = sweep >= 359.999
                ? Ring(cx, cy, rOuter, rInner)
                : Arc(cx, cy, rOuter, rInner, angle, end);
            slices.Add(new DonutSlice(path, color, angle, end));
            angle = end;
        }
        return slices;
    }

    private static string Arc(double cx, double cy, double ro, double ri, double start, double end)
    {
        var large = end - start > 180 ? 1 : 0;
        var (x1, y1) = Point(cx, cy, ro, start);
        var (x2, y2) = Point(cx, cy, ro, end);
        var (x3, y3) = Point(cx, cy, ri, end);
        var (x4, y4) = Point(cx, cy, ri, start);
        return $"M{F(x1)} {F(y1)} A{F(ro)} {F(ro)} 0 {large} 1 {F(x2)} {F(y2)} " +
               $"L{F(x3)} {F(y3)} A{F(ri)} {F(ri)} 0 {large} 0 {F(x4)} {F(y4)} Z";
    }

    private static string Ring(double cx, double cy, double ro, double ri) =>
        $"M{F(cx)} {F(cy - ro)} A{F(ro)} {F(ro)} 0 1 1 {F(cx)} {F(cy + ro)} A{F(ro)} {F(ro)} 0 1 1 {F(cx)} {F(cy - ro)} Z " +
        $"M{F(cx)} {F(cy - ri)} A{F(ri)} {F(ri)} 0 1 0 {F(cx)} {F(cy + ri)} A{F(ri)} {F(ri)} 0 1 0 {F(cx)} {F(cy - ri)} Z";

    /// <summary>0° é meio-dia, sentido horário, como um relógio.</summary>
    public static (double X, double Y) Point(double cx, double cy, double r, double degrees)
    {
        var rad = (degrees - 90) * Math.PI / 180;
        return (cx + r * Math.Cos(rad), cy + r * Math.Sin(rad));
    }

    public static string F(double v) => Math.Round(v, 2).ToString(CultureInfo.InvariantCulture);
}
