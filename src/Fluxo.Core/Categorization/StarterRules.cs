namespace Fluxo.Core.Categorization;

/// <summary>
/// Regras que já vêm prontas pros nomes mais comuns em extrato brasileiro. Perdem pra qualquer
/// regra da pessoa ou aprendida, então nunca atrapalham quem já ensinou o app.
/// </summary>
public static class StarterRules
{
    public static IReadOnlyList<CategoryRule> All { get; } =
    [
        .. Map("mercado", "supermercado", "carrefour", "assai", "atacadao", "pao de acucar", "hortifruti"),
        .. Map("alimentacao", "ifood", "uber eats", "rappi", "restaurante", "lanchonete", "padaria", "mcdonalds", "burger king"),
        .. Map("transporte", "uber", "99app", "99 pop", "posto", "shell", "ipiranga", "estacionamento", "sem parar", "metro", "cptm"),
        .. Map("moradia", "aluguel", "condominio", "enel", "sabesp", "copasa", "cemig", "vivo", "claro", "tim", "net"),
        .. Map("saude", "farmacia", "drogasil", "drogaria", "raia", "pague menos", "unimed", "laboratorio"),
        .. Map("lazer", "netflix", "spotify", "disney", "prime video", "hbo", "youtube", "cinema", "ingresso", "steam", "academia", "smart fit"),
        .. Map("salario", "salario", "pagamento salario", "folha"),
    ];

    private static IEnumerable<CategoryRule> Map(string categoryId, params string[] patterns) =>
        patterns.Select(p => new CategoryRule(RuleEngine.MatchText(p), categoryId, RuleOrigin.Starter));
}
