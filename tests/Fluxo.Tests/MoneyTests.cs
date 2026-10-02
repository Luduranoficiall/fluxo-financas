using System.Globalization;
using Fluxo.Core;

namespace Fluxo.Tests;

public class MoneyTests
{
    [Theory]
    [InlineData("1.234,56", 123456)]
    [InlineData("1234,56", 123456)]
    [InlineData("-12,30", -1230)]
    [InlineData("R$ 10,00", 1000)]
    [InlineData("R$ 1.000,00", 100000)]
    [InlineData("(10,00)", -1000)]
    [InlineData("12,30-", -1230)]
    [InlineData("1234.56", 123456)]
    [InlineData("1,234.56", 123456)]
    [InlineData("-50.00", -5000)]
    [InlineData("1.234", 123400)]
    [InlineData("7,5", 750)]
    [InlineData("10", 1000)]
    [InlineData("+3,00", 300)]
    public void Le_os_formatos_de_extrato(string text, long cents)
    {
        Assert.True(Money.TryParse(text, out var money));
        Assert.Equal(cents, money.Cents);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("12,3x")]
    [InlineData("-")]
    public void Recusa_texto_que_nao_e_valor(string text) => Assert.False(Money.TryParse(text, out _));

    [Fact]
    public void Soma_nao_perde_centavo_como_double_perderia()
    {
        var total = Money.Zero;
        for (var i = 0; i < 1000; i++) total += Money.Parse("0,10");
        Assert.Equal(10000, total.Cents);

        var asDouble = 0.0;
        for (var i = 0; i < 1000; i++) asDouble += 0.1;
        Assert.NotEqual(100.0, asDouble); // o motivo de não usar double
    }

    [Theory]
    [InlineData(123456, "R$ 1.234,56")]
    [InlineData(-1230, "-R$ 12,30")]
    [InlineData(5, "R$ 0,05")]
    [InlineData(123456789012, "R$ 1.234.567.890,12")]
    public void Formata_em_real_sem_depender_do_idioma_do_sistema(long cents, string expected)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US"); // navegador em inglês
        try { Assert.Equal(expected, new Money(cents).ToString()); }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData(1234.5, 1, "1.234,5")]
    [InlineData(5200, 1, "5.200")]
    [InlineData(0.256, 2, "0,26")]
    public void Numero_com_virgula_decimal(double value, int decimals, string expected) =>
        Assert.Equal(expected, PtBr.Number(value, decimals));

    [Fact]
    public void Mes_por_extenso_em_portugues()
    {
        Assert.Equal("Setembro de 2026", PtBr.MonthYear(2026, 9));
        Assert.Equal("mar/26", PtBr.ShortMonth(2026, 3));
    }
}
