namespace Fluxo.Core.Reports;

public sealed record CategoryTotal(Category? Category, Money Total, double Share);

public sealed record MonthSummary(int Year, int Month, Money Income, Money Expense)
{
    public Money Balance => Income - Expense;
    public string Label => PtBr.ShortMonth(Year, Month);
}

public enum BudgetStatus { Ok, Atencao, Estourado }

public sealed record BudgetProgress(Category Category, Money Limit, Money Spent, Money Projected, BudgetStatus Status)
{
    public double Percent => Limit.Cents == 0 ? 0 : (double)Spent.Cents / Limit.Cents;
}

public sealed record GoalProgress(SavingsGoal Goal, double Percent, int MonthsLeft, Money MonthlyNeeded, bool Done);

public static class ReportBuilder
{
    public static MonthSummary Month(IEnumerable<Transaction> txs, int year, int month)
    {
        var income = Money.Zero;
        var expense = Money.Zero;
        foreach (var t in txs.Where(t => t.Date.Year == year && t.Date.Month == month))
        {
            if (t.Amount.IsNegative) expense += t.Amount.Abs();
            else income += t.Amount;
        }
        return new MonthSummary(year, month, income, expense);
    }

    /// <summary>Os últimos N meses até o mês informado, incluindo meses sem lançamento (zerados).</summary>
    public static IReadOnlyList<MonthSummary> LastMonths(IReadOnlyList<Transaction> txs, int year, int month, int count)
    {
        var list = new List<MonthSummary>(count);
        var cursor = new DateOnly(year, month, 1).AddMonths(-(count - 1));
        for (var i = 0; i < count; i++, cursor = cursor.AddMonths(1))
            list.Add(Month(txs, cursor.Year, cursor.Month));
        return list;
    }

    /// <summary>Gastos do mês por categoria, do maior pro menor. Sem categoria aparece como null.</summary>
    public static IReadOnlyList<CategoryTotal> ExpensesByCategory(IEnumerable<Transaction> txs, IReadOnlyList<Category> categories, int year, int month)
    {
        var groups = txs
            .Where(t => t.Date.Year == year && t.Date.Month == month && t.Amount.IsNegative)
            .GroupBy(t => t.CategoryId)
            .Select(g => (Id: g.Key, Total: g.Aggregate(Money.Zero, (acc, t) => acc + t.Amount.Abs())))
            .OrderByDescending(x => x.Total)
            .ToList();
        var sum = groups.Aggregate(0L, (acc, g) => acc + g.Total.Cents);
        return groups
            .Select(g => new CategoryTotal(categories.FirstOrDefault(c => c.Id == g.Id), g.Total, sum == 0 ? 0 : (double)g.Total.Cents / sum))
            .ToList();
    }

    /// <summary>
    /// Quanto já foi do limite e pra onde vai até o fim do mês, no ritmo atual. Projeção só faz
    /// sentido no mês corrente; mês fechado projeta o próprio gasto.
    /// </summary>
    public static IReadOnlyList<BudgetProgress> Budgets(LedgerState state, DateOnly today, int year, int month)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        var isCurrent = today.Year == year && today.Month == month;
        var elapsed = isCurrent ? today.Day : daysInMonth;

        return state.Budgets
            .Select(b =>
            {
                var category = state.Categories.FirstOrDefault(c => c.Id == b.CategoryId);
                if (category is null) return null;
                var spent = state.Transactions
                    .Where(t => t.CategoryId == b.CategoryId && t.Amount.IsNegative && t.Date.Year == year && t.Date.Month == month)
                    .Aggregate(Money.Zero, (acc, t) => acc + t.Amount.Abs());
                var projected = new Money(spent.Cents * daysInMonth / Math.Max(elapsed, 1));
                var status = spent > b.MonthlyLimit ? BudgetStatus.Estourado
                    : projected > b.MonthlyLimit ? BudgetStatus.Atencao
                    : BudgetStatus.Ok;
                return new BudgetProgress(category, b.MonthlyLimit, spent, projected, status);
            })
            .Where(p => p is not null)
            .Select(p => p!)
            .OrderByDescending(p => p.Percent)
            .ToList();
    }

    /// <summary>Quanto guardar por mês, a partir de hoje, pra bater a meta no prazo.</summary>
    public static GoalProgress Goal(SavingsGoal goal, DateOnly today)
    {
        var remaining = goal.Target - goal.Saved;
        var done = remaining <= Money.Zero;
        var months = Math.Max((goal.Deadline.Year - today.Year) * 12 + goal.Deadline.Month - today.Month, 0);
        var divisor = Math.Max(months, 1);
        // Arredonda pra cima: guardar 1 centavo a menos por mês faria a meta não fechar no prazo.
        var monthly = done ? Money.Zero : new Money((remaining.Cents + divisor - 1) / divisor);
        var percent = goal.Target.Cents == 0 ? 1 : Math.Min(1, (double)goal.Saved.Cents / goal.Target.Cents);
        return new GoalProgress(goal, percent, months, monthly, done);
    }
}
