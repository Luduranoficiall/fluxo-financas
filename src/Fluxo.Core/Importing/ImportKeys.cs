using System.Security.Cryptography;
using System.Text;

namespace Fluxo.Core.Importing;

/// <summary>
/// Gera a chave que impede importar o mesmo lançamento duas vezes.
///
/// OFX traz o FITID, um id do próprio banco, então a chave é ele. CSV não traz id nenhum, e aí
/// mora a armadilha: dois cafés de R$ 5,00 no mesmo dia, mesmo lugar, são duas compras reais e
/// geram a mesma combinação de data + valor + descrição. Se a chave fosse só isso, o segundo
/// café sumiria como "duplicado". A saída é numerar a ocorrência dentro do arquivo (#0, #1...):
/// os dois cafés entram, e reimportar o mesmo arquivo gera exatamente as mesmas chaves.
/// </summary>
public static class ImportKeys
{
    public static IReadOnlyList<string> For(ParseResult parsed, string source)
    {
        var keys = new List<string>(parsed.Entries.Count);
        var occurrences = new Dictionary<string, int>();
        var account = parsed.AccountId ?? "-";

        foreach (var e in parsed.Entries)
        {
            if (e.ExternalId is not null)
            {
                keys.Add($"{source}:{account}:{e.ExternalId}");
                continue;
            }

            var natural = $"{e.Date:yyyy-MM-dd}|{e.Amount.Cents}|{TextNormalizer.Normalize(e.Description)}";
            occurrences.TryGetValue(natural, out var n);
            occurrences[natural] = n + 1;
            keys.Add($"{source}:{Hash(natural)}#{n}");
        }
        return keys;
    }

    private static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16].ToLowerInvariant();
}
