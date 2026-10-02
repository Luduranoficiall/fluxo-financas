namespace Fluxo.Core;

public enum CategoryKind { Despesa, Receita }

public sealed record Category(string Id, string Name, CategoryKind Kind, string Color);

/// <summary>De onde veio a categoria de um lançamento. Manual nunca é sobrescrita por regra.</summary>
public enum CategorySource { None, Rule, Manual }

public sealed record Transaction
{
    public required string Id { get; init; }
    public required DateOnly Date { get; init; }
    public required string Description { get; init; }
    /// <summary>Negativo é saída, positivo é entrada.</summary>
    public required Money Amount { get; init; }
    /// <summary>Chave estável pra não importar o mesmo lançamento duas vezes.</summary>
    public required string ImportKey { get; init; }
    public string? CategoryId { get; init; }
    public CategorySource CategorySource { get; init; }
}

public sealed record Budget(string CategoryId, Money MonthlyLimit);

public sealed record SavingsGoal(string Id, string Name, Money Target, Money Saved, DateOnly Deadline);

/// <summary>Uma linha lida de um arquivo, antes de virar lançamento.</summary>
public sealed record ImportedEntry(DateOnly Date, string Description, Money Amount, string? ExternalId);

public sealed record ImportError(int Line, string Message);

public sealed record ParseResult(IReadOnlyList<ImportedEntry> Entries, IReadOnlyList<ImportError> Errors, string? AccountId = null);

public static class DefaultCategories
{
    public static IReadOnlyList<Category> All { get; } =
    [
        new("mercado", "Mercado", CategoryKind.Despesa, "#4E9F6E"),
        new("alimentacao", "Restaurante e delivery", CategoryKind.Despesa, "#D9822B"),
        new("transporte", "Transporte", CategoryKind.Despesa, "#3D7EB8"),
        new("moradia", "Moradia e contas", CategoryKind.Despesa, "#8C6BB1"),
        new("saude", "Saúde", CategoryKind.Despesa, "#C2455B"),
        new("lazer", "Lazer e assinaturas", CategoryKind.Despesa, "#2E9C9C"),
        new("outros", "Outros gastos", CategoryKind.Despesa, "#7A7A7A"),
        new("salario", "Salário", CategoryKind.Receita, "#3F8F3F"),
        new("renda-extra", "Renda extra", CategoryKind.Receita, "#7FAF3A"),
    ];
}
