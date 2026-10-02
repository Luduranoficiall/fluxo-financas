using System.Globalization;
using System.Net;
using System.Text;

namespace Fluxo.Core.Importing;

/// <summary>
/// Lê extrato OFX, o arquivo que todo banco brasileiro exporta. Existem dois formatos:
/// OFX 1.x é SGML (tag folha não fecha: <c>&lt;TRNAMT&gt;-50.00</c> e pronto), e OFX 2.x é
/// XML de verdade. Nenhum parser XML lê o 1.x, então aqui é um tokenizador próprio que entende
/// os dois. Também lida com o que os bancos daqui fazem na prática: arquivo em Windows-1252,
/// valor com vírgula decimal e data com fuso no formato <c>20260905120000[-3:BRT]</c>.
/// </summary>
public static class OfxParser
{
    static OfxParser() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    /// <summary>Decide a codificação pelo cabeçalho do próprio arquivo (CHARSET/ENCODING).</summary>
    public static ParseResult Parse(byte[] content)
    {
        var head = Encoding.ASCII.GetString(content, 0, Math.Min(content.Length, 600)).ToUpperInvariant();
        Encoding encoding = Encoding.UTF8;
        if (head.Contains("CHARSET:1252") || head.Contains("ENCODING:USASCII") || head.Contains("WINDOWS-1252") || head.Contains("ISO-8859-1"))
            encoding = Encoding.GetEncoding(1252);
        var text = encoding.GetString(content);
        if (text.Length > 0 && text[0] == '﻿') text = text[1..];
        return Parse(text);
    }

    public static ParseResult Parse(string content)
    {
        var start = content.IndexOf("<OFX>", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return new ParseResult([], [new ImportError(1, "Não achei a tag <OFX>. Esse arquivo não parece um extrato OFX.")]);

        var root = BuildTree(content[start..]);
        var entries = new List<ImportedEntry>();
        var errors = new List<ImportError>();
        string? account = root.Find("ACCTID")?.Value;

        var index = 0;
        foreach (var trn in root.FindAll("STMTTRN"))
        {
            index++;
            var rawDate = trn.Child("DTPOSTED")?.Value;
            var rawAmount = trn.Child("TRNAMT")?.Value;
            var fitId = trn.Child("FITID")?.Value;
            var memo = trn.Child("MEMO")?.Value;
            var name = trn.Child("NAME")?.Value;

            if (!TryParseDate(rawDate, out var date))
            {
                errors.Add(new ImportError(index, $"Lançamento {index}: data inválida ('{rawDate}')."));
                continue;
            }
            if (!Money.TryParse(rawAmount, out var amount))
            {
                errors.Add(new ImportError(index, $"Lançamento {index}: valor inválido ('{rawAmount}')."));
                continue;
            }

            // Banco que manda NAME e MEMO iguais (ou um vazio) não deve virar "PIX PIX".
            var description = string.Join(" - ",
                new[] { name, memo }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase));
            if (description.Length == 0) description = "(sem descrição)";

            entries.Add(new ImportedEntry(date, description, amount, string.IsNullOrWhiteSpace(fitId) ? null : fitId.Trim()));
        }

        if (index == 0)
            errors.Add(new ImportError(1, "O arquivo é OFX, mas não tem nenhum lançamento (STMTTRN)."));

        return new ParseResult(entries, errors, account?.Trim());
    }

    /// <summary>yyyyMMdd, com hora, milissegundos e fuso opcionais. Só a data importa aqui.</summary>
    public static bool TryParseDate(string? raw, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var digits = raw.Trim();
        if (digits.Length < 8) return false;
        return DateOnly.TryParseExact(digits[..8], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    // ---------- árvore SGML/XML tolerante ----------

    public sealed class Node(string name)
    {
        public string Name { get; } = name;
        public string? Value { get; set; }
        public List<Node> Children { get; } = [];

        public Node? Child(string name) => Children.FirstOrDefault(c => c.Name == name);

        public Node? Find(string name)
        {
            foreach (var c in Children)
            {
                if (c.Name == name) return c;
                var deep = c.Find(name);
                if (deep is not null) return deep;
            }
            return null;
        }

        public IEnumerable<Node> FindAll(string name)
        {
            foreach (var c in Children)
            {
                if (c.Name == name) yield return c;
                foreach (var deep in c.FindAll(name)) yield return deep;
            }
        }
    }

    /// <summary>
    /// Tag seguida de texto antes da próxima tag é folha (fecha sozinha, como no SGML). Tag sem
    /// texto abre um agregado. Uma tag de fechamento desempilha até achar a dona dela, o que
    /// também aguenta o arquivo do banco que esquece de fechar um agregado no meio.
    /// </summary>
    internal static Node BuildTree(string body)
    {
        var root = new Node("#root");
        var stack = new Stack<Node>();
        stack.Push(root);
        var i = 0;

        while (i < body.Length)
        {
            var lt = body.IndexOf('<', i);
            if (lt < 0) break;
            var gt = body.IndexOf('>', lt);
            if (gt < 0) break;

            var tag = body[(lt + 1)..gt].Trim();
            i = gt + 1;
            if (tag.Length == 0 || tag[0] == '?' || tag[0] == '!') continue;

            if (tag[0] == '/')
            {
                var closing = tag[1..].Trim().ToUpperInvariant();
                if (stack.Any(n => n.Name == closing))
                {
                    while (stack.Count > 1)
                    {
                        var popped = stack.Pop();
                        if (popped.Name == closing) break;
                    }
                }
                continue;
            }

            var selfClosing = tag.EndsWith('/');
            var name = (selfClosing ? tag[..^1] : tag).Trim().Split(' ', 2)[0].ToUpperInvariant();
            var node = new Node(name);
            stack.Peek().Children.Add(node);
            if (selfClosing) continue;

            var nextLt = body.IndexOf('<', i);
            var text = (nextLt < 0 ? body[i..] : body[i..nextLt]).Trim();
            if (text.Length > 0)
            {
                node.Value = WebUtility.HtmlDecode(text);
                i = nextLt < 0 ? body.Length : nextLt;
                // No XML a folha vem com fechamento explícito; consome pra não desempilhar o pai.
                var close = $"</{name}>";
                if (string.Compare(body, i, close, 0, close.Length, StringComparison.OrdinalIgnoreCase) == 0)
                    i += close.Length;
            }
            else
            {
                stack.Push(node);
            }
        }

        return root;
    }
}
