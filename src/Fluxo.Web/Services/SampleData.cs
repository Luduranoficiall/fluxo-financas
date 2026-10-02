using Fluxo.Core;

namespace Fluxo.Web.Services;

/// <summary>
/// Extrato fictício dos quatro meses fechados antes do atual. Gerado com semente fixa, então é
/// sempre o mesmo. O mês corrente fica de fora: no começo do mês ele teria um ou dois
/// lançamentos e o painel abriria quase vazio.
/// </summary>
public static class SampleData
{
    public static ParseResult Statement(DateOnly today)
    {
        var rng = new Random(42);
        var entries = new List<ImportedEntry>();
        var start = new DateOnly(today.Year, today.Month, 1).AddMonths(-4);

        for (var m = 0; m < 4; m++)
        {
            var month = start.AddMonths(m);
            var lastDay = DateTime.DaysInMonth(month.Year, month.Month);
            void Add(int day, string desc, decimal value)
            {
                if (day > lastDay) return;
                entries.Add(new ImportedEntry(month.AddDays(day - 1), desc, Money.FromDecimal(value), null));
            }

            Add(5, "SALARIO EMPRESA EXEMPLO LTDA", 5200m);
            Add(10, "PIX ENVIADO ALUGUEL APTO 12", -1800m);
            Add(12, "DEB AUTOMATICO ENEL SP", -(180m + rng.Next(0, 60)));
            Add(15, "NETFLIX.COM", -39.90m);
            Add(16, "SPOTIFY", -21.90m);
            Add(18, "SMART FIT MENSALIDADE", -119.90m);
            if (m % 2 == 1) Add(20, "PIX RECEBIDO FREELA SITE", 900m + rng.Next(0, 4) * 100);

            for (var week = 0; week < 5; week++)
            {
                Add(2 + week * 7, "COMPRA CARTAO 4321 CARREFOUR HIPER", -(150m + rng.Next(0, 170)));
                Add(4 + week * 7, $"COMPRA CARTAO 4321 IFOOD *RESTAURANTE {2 + week * 7:00}/{month.Month:00}", -(32m + rng.Next(0, 40)));
                Add(6 + week * 7, "UBER TRIP SAO PAULO", -(14m + rng.Next(0, 22)));
                if (week % 2 == 0) Add(3 + week * 7, "PADARIA BELLA VISTA", -(9m + rng.Next(0, 15)));
            }
            Add(22, "DROGASIL 0451", -(40m + rng.Next(0, 90)));
            Add(25, "POSTO SHELL AV BRASIL", -(180m + rng.Next(0, 60)));
            Add(27, "CINEMARK INGRESSO", -64m);
        }

        return new ParseResult(entries, []);
    }

    public static void ApplyBudgetsAndGoals(Ledger ledger, DateOnly today)
    {
        ledger.SetBudget("alimentacao", Money.Parse("550,00"));
        ledger.SetBudget("mercado", Money.Parse("1.100,00"));
        ledger.SetBudget("transporte", Money.Parse("500,00"));
        ledger.SaveGoal(new SavingsGoal("", "Reserva de emergência", Money.Parse("15.000,00"), Money.Parse("4.200,00"), today.AddMonths(14)));
        ledger.SaveGoal(new SavingsGoal("", "Viagem de fim de ano", Money.Parse("4.000,00"), Money.Parse("1.350,00"), today.AddMonths(3)));
    }
}
