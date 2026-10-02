using Fluxo.Core.Categorization;
using Fluxo.Core.Importing;

namespace Fluxo.Core;

/// <summary>Tudo que a pessoa tem salvo. É isso que vai pro armazenamento.</summary>
public sealed record LedgerState
{
    public int Version { get; init; } = 1;
    public List<Category> Categories { get; init; } = [.. DefaultCategories.All];
    public List<CategoryRule> Rules { get; init; } = [];
    public List<Transaction> Transactions { get; init; } = [];
    public List<Budget> Budgets { get; init; } = [];
    public List<SavingsGoal> Goals { get; init; } = [];
}

public sealed record ImportSummary(int Added, int Duplicates, int AutoCategorized, IReadOnlyList<ImportError> Errors);

public sealed record RecategorizeResult(string? LearnedPattern, int AlsoUpdated);

/// <summary>
/// Regras de negócio do app em cima do estado. Não sabe nada de tela nem de onde os dados
/// ficam salvos, por isso é testável sem navegador.
/// </summary>
public sealed class Ledger
{
    private LedgerState _state;
    private readonly Func<string> _newId;

    public Ledger(LedgerState? state = null, Func<string>? newId = null)
    {
        _state = state ?? new LedgerState();
        _newId = newId ?? (() => Guid.NewGuid().ToString("N")[..12]);
    }

    public LedgerState State => _state;
    public event Action? Changed;

    public IReadOnlyList<Transaction> Transactions => _state.Transactions;
    public Category? CategoryOf(Transaction t) => _state.Categories.FirstOrDefault(c => c.Id == t.CategoryId);

    public ImportSummary Import(ParseResult parsed, string source)
    {
        var keys = ImportKeys.For(parsed, source);
        var existing = _state.Transactions.Select(t => t.ImportKey).ToHashSet();
        var added = new List<Transaction>();
        var duplicates = 0;

        for (var i = 0; i < parsed.Entries.Count; i++)
        {
            if (!existing.Add(keys[i])) { duplicates++; continue; }
            var e = parsed.Entries[i];
            added.Add(new Transaction
            {
                Id = _newId(),
                Date = e.Date,
                Description = e.Description,
                Amount = e.Amount,
                ImportKey = keys[i],
            });
        }

        var categorized = RuleEngine.Apply(added, _state.Rules);
        _state = _state with { Transactions = [.. _state.Transactions, .. categorized] };
        Changed?.Invoke();
        return new ImportSummary(added.Count, duplicates, categorized.Count(t => t.CategoryId is not null), parsed.Errors);
    }

    /// <summary>
    /// A pessoa corrigiu a categoria de um lançamento. Além de gravar a escolha, o app aprende:
    /// cria uma regra com o nome do estabelecimento e aplica nos outros lançamentos dele que
    /// ainda não foram categorizados na mão.
    /// </summary>
    public RecategorizeResult Recategorize(string transactionId, string? categoryId, bool learn = true)
    {
        var target = _state.Transactions.FirstOrDefault(t => t.Id == transactionId)
            ?? throw new KeyNotFoundException($"Lançamento {transactionId} não existe.");
        if (categoryId is not null && _state.Categories.All(c => c.Id != categoryId))
            throw new ArgumentException($"Categoria {categoryId} não existe.", nameof(categoryId));

        var txs = _state.Transactions
            .Select(t => t.Id == transactionId
                ? t with { CategoryId = categoryId, CategorySource = categoryId is null ? CategorySource.None : CategorySource.Manual }
                : t)
            .ToList();
        var rules = _state.Rules;
        string? learned = null;
        var alsoUpdated = 0;

        if (learn && categoryId is not null)
        {
            var key = RuleEngine.MerchantKey(target.Description);
            if (key.Length > 0)
            {
                learned = key;
                rules = [.. rules.Where(r => !(r.Origin == RuleOrigin.Learned && r.Pattern == key)),
                    new CategoryRule(key, categoryId, RuleOrigin.Learned)];
                var before = txs.ToDictionary(t => t.Id, t => t.CategoryId);
                txs = [.. RuleEngine.Apply(txs, rules)];
                alsoUpdated = txs.Count(t => t.Id != transactionId && before[t.Id] != t.CategoryId);
            }
        }

        _state = _state with { Transactions = txs, Rules = rules };
        Changed?.Invoke();
        return new RecategorizeResult(learned, alsoUpdated);
    }

    public void AddRule(string pattern, string categoryId)
    {
        var normalized = RuleEngine.MatchText(pattern);
        if (normalized.Length == 0) throw new ArgumentException("A regra precisa de um texto.", nameof(pattern));
        var rules = _state.Rules.Where(r => !(r.Origin == RuleOrigin.User && r.Pattern == normalized)).ToList();
        rules.Add(new CategoryRule(normalized, categoryId, RuleOrigin.User));
        _state = _state with { Rules = rules, Transactions = [.. RuleEngine.Apply(_state.Transactions, rules)] };
        Changed?.Invoke();
    }

    public void RemoveRule(CategoryRule rule)
    {
        var rules = _state.Rules.Where(r => r != rule).ToList();
        _state = _state with { Rules = rules, Transactions = [.. RuleEngine.Apply(_state.Transactions, rules)] };
        Changed?.Invoke();
    }

    public void SetBudget(string categoryId, Money limit)
    {
        var budgets = _state.Budgets.Where(b => b.CategoryId != categoryId).ToList();
        if (limit > Money.Zero) budgets.Add(new Budget(categoryId, limit));
        _state = _state with { Budgets = budgets };
        Changed?.Invoke();
    }

    public void SaveGoal(SavingsGoal goal)
    {
        var id = string.IsNullOrEmpty(goal.Id) ? _newId() : goal.Id;
        _state = _state with { Goals = [.. _state.Goals.Where(g => g.Id != id), goal with { Id = id }] };
        Changed?.Invoke();
    }

    public void RemoveGoal(string id)
    {
        _state = _state with { Goals = _state.Goals.Where(g => g.Id != id).ToList() };
        Changed?.Invoke();
    }

    public void Reset()
    {
        _state = new LedgerState();
        Changed?.Invoke();
    }

    /// <summary>Coloca as regras prontas, uma vez só: se já tem alguma, não mexe.</summary>
    public bool SeedStarterRules()
    {
        if (_state.Rules.Any(r => r.Origin == RuleOrigin.Starter)) return false;
        List<CategoryRule> rules = [.. _state.Rules, .. StarterRules.All];
        _state = _state with { Rules = rules, Transactions = [.. RuleEngine.Apply(_state.Transactions, rules)] };
        Changed?.Invoke();
        return true;
    }
}
