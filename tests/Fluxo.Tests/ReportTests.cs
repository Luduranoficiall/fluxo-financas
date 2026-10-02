using Fluxo.Core;
using Fluxo.Core.Charts;
using Fluxo.Core.Reports;

namespace Fluxo.Tests;

public class ReportTests
{
    private static Transaction T(string date, string amount, string? category = null) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Date = DateOnly.Parse(date),
        Description = "x",
        Amount = Money.Parse(amount),
        ImportKey = Guid.NewGuid().ToString(),
        CategoryId = category,
    };

    [Fact]
    public void Resumo_do_mes_separa_entrada_e_saida()
    {
        var txs = new[] { T("2026-09-01", "5000,00"), T("2026-09-02", "-1200,00"), T("2026-09-30", "-300,00"), T("2026-10-01", "-999,00") };
        var month = ReportBuilder.Month(txs, 2026, 9);
        Assert.Equal(500000, month.Income.Cents);
        Assert.Equal(150000, month.Expense.Cents);
        Assert.Equal(350000, month.Balance.Cents);
    }

    [Fact]
    public void Ultimos_meses_inclui_mes_vazio_e_vira_o_ano()
    {
        var months = ReportBuilder.LastMonths([T("2026-01-10", "-10,00")], 2026, 2, 4);
        Assert.Equal([(2025, 11), (2025, 12), (2026, 1), (2026, 2)], months.Select(m => (m.Year, m.Month)));
        Assert.Equal(1000, months[2].Expense.Cents);
        Assert.True(months[0].Expense.IsZero);
    }

    [Fact]
    public void Gastos_por_categoria_do_maior_pro_menor_com_fatia()
    {
        var txs = new[] { T("2026-09-01", "-300,00", "mercado"), T("2026-09-02", "-100,00", "lazer"), T("2026-09-03", "-100,00", null), T("2026-09-04", "50,00", "salario") };
        var totals = ReportBuilder.ExpensesByCategory(txs, DefaultCategories.All, 2026, 9);
        Assert.Equal("mercado", totals[0].Category!.Id);
        Assert.Equal(0.6, totals[0].Share, 3);
        Assert.Contains(totals, t => t.Category is null);
        Assert.Equal(1.0, totals.Sum(t => t.Share), 6);
    }

    [Fact]
    public void Orcamento_avisa_antes_de_estourar_pelo_ritmo_do_mes()
    {
        var state = new LedgerState
        {
            Transactions = [T("2026-09-01", "-200,00", "alimentacao"), T("2026-09-08", "-100,00", "alimentacao")],
            Budgets = [new Budget("alimentacao", Money.Parse("600,00"))],
        };
        // Dia 10 de 30 com R$ 300 gastos: no ritmo, fecha o mês em R$ 900. Ainda não estourou.
        var progress = Assert.Single(ReportBuilder.Budgets(state, new DateOnly(2026, 9, 10), 2026, 9));
        Assert.Equal(BudgetStatus.Atencao, progress.Status);
        Assert.Equal(90000, progress.Projected.Cents);
        Assert.Equal(0.5, progress.Percent, 3);

        var closed = Assert.Single(ReportBuilder.Budgets(state, new DateOnly(2026, 10, 2), 2026, 9));
        Assert.Equal(BudgetStatus.Ok, closed.Status); // mês fechado não projeta
    }

    [Fact]
    public void Meta_arredonda_a_parcela_mensal_pra_cima()
    {
        var goal = new SavingsGoal("g", "Viagem", Money.Parse("1.000,00"), Money.Parse("0,00"), new DateOnly(2026, 12, 15));
        var p = ReportBuilder.Goal(goal, new DateOnly(2026, 9, 20));
        Assert.Equal(3, p.MonthsLeft);
        Assert.Equal(33334, p.MonthlyNeeded.Cents); // 3 x 333,34 cobre os 1.000; 333,33 não cobriria
        Assert.False(p.Done);

        var done = ReportBuilder.Goal(goal with { Saved = Money.Parse("1.200,00") }, new DateOnly(2026, 9, 20));
        Assert.True(done.Done);
        Assert.Equal(1.0, done.Percent);
    }

    [Theory]
    [InlineData(1237, 1500, 500)]
    [InlineData(87, 100, 20)]
    [InlineData(5000, 5000, 1000)]
    [InlineData(0.8, 0.8, 0.2)]
    public void Escala_do_eixo_com_numero_redondo(double max, double niceMax, double step)
    {
        var (m, s) = ChartMath.NiceScale(max);
        Assert.Equal(niceMax, m, 6);
        Assert.Equal(step, s, 6);
    }

    [Fact]
    public void Fatias_da_rosca_fecham_360_graus_e_fatia_unica_vira_anel()
    {
        var slices = ChartMath.Donut([(30, "#a"), (0, "#b"), (70, "#c")], 50, 50, 40, 25);
        Assert.Equal(2, slices.Count);
        Assert.Equal(0, slices[0].StartAngle);
        Assert.Equal(360, slices[^1].EndAngle, 6);
        Assert.Equal(108, slices[0].EndAngle, 6);

        var single = Assert.Single(ChartMath.Donut([(10, "#a")], 50, 50, 40, 25));
        Assert.Equal(4, single.Path.Split('A').Length - 1); // dois arcos fora + dois dentro
    }

    [Fact]
    public void Ponto_zero_grau_fica_no_topo()
    {
        var (x, y) = ChartMath.Point(50, 50, 10, 0);
        Assert.Equal(50, x, 6);
        Assert.Equal(40, y, 6);
    }
}
