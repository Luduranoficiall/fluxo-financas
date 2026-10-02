using System.Text.RegularExpressions;

namespace Fluxo.Core.Categorization;

/// <summary>Ordem de prioridade: a da pessoa, depois a aprendida, depois a que vem pronta.</summary>
public enum RuleOrigin { User, Learned, Starter }

/// <summary>Se a descrição normalizada contém <see cref="Pattern"/>, o lançamento vai pra categoria.</summary>
public sealed record CategoryRule(string Pattern, string CategoryId, RuleOrigin Origin);

/// <summary>
/// Decide a categoria de cada lançamento. Quando mais de uma regra bate, ganha a criada pela
/// pessoa sobre a aprendida, e depois a mais específica (padrão mais longo): "uber eats" vence
/// "uber", então a comida não cai em Transporte.
/// </summary>
public static partial class RuleEngine
{
    /// <summary>
    /// Texto usado pra comparar regra com descrição: sem acento, minúsculo e com qualquer símbolo
    /// virando espaço. Sem isso, "IFOOD *RESTAURANTE" não bateria com a regra "ifood restaurante".
    /// Vale pros dois lados: a regra é guardada já passada por aqui.
    /// </summary>
    public static string MatchText(string text) =>
        string.Join(' ', NonAlphanumeric().Replace(TextNormalizer.Normalize(text), " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    public static CategoryRule? Match(string description, IReadOnlyList<CategoryRule> rules)
    {
        var text = " " + MatchText(description) + " ";
        return rules
            // Espaço nas pontas: a regra "uber" casa com "uber trip", nunca com "suberb".
            .Where(r => r.Pattern.Length > 0 && text.Contains(" " + r.Pattern + " ", StringComparison.Ordinal))
            .OrderBy(r => (int)r.Origin)
            .ThenByDescending(r => r.Pattern.Length)
            .FirstOrDefault();
    }

    /// <summary>Aplica as regras sem nunca mexer no que a pessoa categorizou na mão.</summary>
    public static IReadOnlyList<Transaction> Apply(IEnumerable<Transaction> transactions, IReadOnlyList<CategoryRule> rules) =>
        transactions.Select(t =>
        {
            if (t.CategorySource == CategorySource.Manual) return t;
            var rule = Match(t.Description, rules);
            return rule is null
                ? t with { CategoryId = null, CategorySource = CategorySource.None }
                : t with { CategoryId = rule.CategoryId, CategorySource = CategorySource.Rule };
        }).ToList();

    private static readonly HashSet<string> Noise =
    [
        "compra", "cartao", "credito", "debito", "pix", "enviado", "recebido", "transf", "transferencia",
        "ted", "doc", "pag", "pagamento", "pgto", "boleto", "deb", "aut", "automatico", "elo", "visa",
        "master", "mastercard", "nacional", "internacional", "parcela", "de", "da", "do", "para", "em",
        "com", "br", "ltda", "me", "sa", "eireli", "s/a", "online", "app", "no", "na",
    ];

    /// <summary>
    /// Tira da descrição o que muda de um lançamento pro outro (data, número de cartão, parcela,
    /// "COMPRA CARTAO", "PIX ENVIADO") e fica com o nome do estabelecimento. É isso que vira a
    /// regra aprendida: corrigir um "COMPRA CARTAO 1234 IFOOD *RESTAURANTE 05/09" ensina a
    /// categoria de todo "ifood restaurante" que vier depois.
    /// </summary>
    public static string MerchantKey(string description)
    {
        var tokens = MatchText(description).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        static bool Meaningful(string t) => t.Length >= 2 && !Noise.Contains(t) && t.Any(char.IsAsciiLetter) && !t.Any(char.IsAsciiDigit);

        // Primeiro nome que importa, mais o seguinte só se vier colado nele. Pular ruído no meio
        // ("uber do brasil" -> "uber brasil") geraria um padrão que não aparece no texto.
        var first = Array.FindIndex(tokens, Meaningful);
        if (first < 0) return "";
        return first + 1 < tokens.Length && Meaningful(tokens[first + 1])
            ? $"{tokens[first]} {tokens[first + 1]}"
            : tokens[first];
    }

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();
}
