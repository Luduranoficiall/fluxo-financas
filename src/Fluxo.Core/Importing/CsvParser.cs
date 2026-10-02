using System.Globalization;
using System.Text;

namespace Fluxo.Core.Importing;

/// <summary>
/// Lê CSV de extrato sem saber de qual banco veio. Descobre o separador (; , ou tab), acha as
/// colunas pelo nome do cabeçalho (cada banco chama de um jeito: "Histórico", "Lançamento",
/// "Descrição"...), aceita valor numa coluna só ou em duas (crédito e débito) e segue o
/// RFC 4180 pra campo entre aspas. Linha ruim não derruba o arquivo: vira erro com o número
/// da linha e o resto entra.
/// </summary>
public static class CsvParser
{
    private static readonly string[] DateAliases = ["data", "date", "data lancamento", "data do lancamento", "dt lancamento", "data movimento"];
    private static readonly string[] DescriptionAliases = ["descricao", "historico", "lancamento", "description", "memo", "detalhe", "estabelecimento"];
    private static readonly string[] AmountAliases = ["valor", "amount", "value", "valor (r$)", "valor r$", "quantia"];
    private static readonly string[] CreditAliases = ["credito", "entrada", "entradas", "credit"];
    private static readonly string[] DebitAliases = ["debito", "saida", "saidas", "debit"];

    private static readonly string[] DateFormats = ["dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "yyyy-MM-dd", "dd-MM-yyyy", "dd.MM.yyyy"];

    public static ParseResult Parse(string content)
    {
        if (content.Length > 0 && content[0] == '﻿') content = content[1..];
        var lines = SplitRecords(content);
        var errors = new List<ImportError>();
        var entries = new List<ImportedEntry>();

        var headerIndex = lines.FindIndex(l => l.Text.Trim().Length > 0);
        if (headerIndex < 0) return new ParseResult([], [new ImportError(1, "Arquivo vazio.")]);

        var delimiter = DetectDelimiter(lines[headerIndex].Text);
        var header = SplitFields(lines[headerIndex].Text, delimiter).Select(Normalize).ToList();

        int dateCol = FindColumn(header, DateAliases);
        int descCol = FindColumn(header, DescriptionAliases);
        int amountCol = FindColumn(header, AmountAliases);
        int creditCol = FindColumn(header, CreditAliases);
        int debitCol = FindColumn(header, DebitAliases);

        if (dateCol < 0 || descCol < 0 || (amountCol < 0 && creditCol < 0 && debitCol < 0))
        {
            return new ParseResult([], [new ImportError(lines[headerIndex].Number,
                "Não achei as colunas de data, descrição e valor no cabeçalho. " +
                $"Colunas encontradas: {string.Join(", ", header)}.")]);
        }

        foreach (var (number, text) in lines.Skip(headerIndex + 1))
        {
            if (text.Trim().Length == 0) continue;
            var fields = SplitFields(text, delimiter);
            string Field(int col) => col >= 0 && col < fields.Count ? fields[col].Trim() : "";

            var rawDate = Field(dateCol);
            if (!DateOnly.TryParseExact(rawDate, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                // Linha de saldo ou rodapé ("SALDO ANTERIOR") sem data não é erro, só não é lançamento.
                if (rawDate.Length == 0 || !rawDate.Any(char.IsDigit)) continue;
                errors.Add(new ImportError(number, $"Linha {number}: data inválida ('{rawDate}')."));
                continue;
            }

            Money amount;
            if (amountCol >= 0 && Field(amountCol).Length > 0)
            {
                if (!Money.TryParse(Field(amountCol), out amount))
                {
                    errors.Add(new ImportError(number, $"Linha {number}: valor inválido ('{Field(amountCol)}')."));
                    continue;
                }
            }
            else
            {
                var hasCredit = Money.TryParse(Field(creditCol), out var credit);
                var hasDebit = Money.TryParse(Field(debitCol), out var debit);
                if (!hasCredit && !hasDebit)
                {
                    errors.Add(new ImportError(number, $"Linha {number}: sem valor de crédito nem de débito."));
                    continue;
                }
                // Débito costuma vir positivo na coluna de saída; o sinal vem da coluna, não do número.
                amount = (hasCredit ? credit.Abs() : Money.Zero) - (hasDebit ? debit.Abs() : Money.Zero);
            }

            var description = Field(descCol);
            entries.Add(new ImportedEntry(date, description.Length > 0 ? description : "(sem descrição)", amount, null));
        }

        return new ParseResult(entries, errors);
    }

    internal static char DetectDelimiter(string headerLine)
    {
        char[] candidates = [';', ',', '\t'];
        return candidates.OrderByDescending(c => SplitFields(headerLine, c).Count).First();
    }

    /// <summary>Quebra em registros respeitando quebra de linha dentro de aspas.</summary>
    internal static List<(int Number, string Text)> SplitRecords(string content)
    {
        var records = new List<(int, string)>();
        var sb = new StringBuilder();
        var inQuotes = false;
        var line = 1;
        var recordStart = 1;

        for (var i = 0; i < content.Length; i++)
        {
            var c = content[i];
            if (c == '"') inQuotes = !inQuotes;
            if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n') i++;
                line++;
                if (!inQuotes)
                {
                    records.Add((recordStart, sb.ToString()));
                    sb.Clear();
                    recordStart = line;
                    continue;
                }
                sb.Append('\n');
                continue;
            }
            sb.Append(c);
        }
        if (sb.Length > 0) records.Add((recordStart, sb.ToString()));
        return records;
    }

    internal static List<string> SplitFields(string record, char delimiter)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < record.Length; i++)
        {
            var c = record[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < record.Length && record[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == delimiter) { fields.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        fields.Add(sb.ToString());
        return fields;
    }

    private static int FindColumn(List<string> header, string[] aliases)
    {
        for (var i = 0; i < header.Count; i++)
            if (aliases.Contains(header[i])) return i;
        for (var i = 0; i < header.Count; i++)
            if (aliases.Any(a => header[i].StartsWith(a + " ", StringComparison.Ordinal))) return i;
        return -1;
    }

    private static string Normalize(string s) => TextNormalizer.Normalize(s.Trim().Trim('"'));
}
