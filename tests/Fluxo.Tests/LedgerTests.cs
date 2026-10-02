using Fluxo.Core;
using Fluxo.Core.Categorization;
using Fluxo.Core.Importing;
using Fluxo.Core.Persistence;

namespace Fluxo.Tests;

public class LedgerTests
{
    private static Ledger NewLedger()
    {
        var n = 0;
        return new Ledger(newId: () => $"t{++n}");
    }

    [Fact]
    public void Importar_o_mesmo_OFX_duas_vezes_nao_duplica()
    {
        var ledger = NewLedger();
        var first = ledger.Import(OfxParser.Parse(OfxParserTests.SgmlStatement), "ofx");
        var second = ledger.Import(OfxParser.Parse(OfxParserTests.SgmlStatement), "ofx");

        Assert.Equal(3, first.Added);
        Assert.Equal(0, second.Added);
        Assert.Equal(3, second.Duplicates);
        Assert.Equal(3, ledger.Transactions.Count);
    }

    [Fact]
    public void Dois_cafes_iguais_no_mesmo_dia_entram_os_dois_e_reimportar_nao_duplica()
    {
        const string csv = "Data;Descrição;Valor\n05/09/2026;PADARIA CENTRAL;-5,00\n05/09/2026;PADARIA CENTRAL;-5,00\n";
        var ledger = NewLedger();

        Assert.Equal(2, ledger.Import(CsvParser.Parse(csv), "csv").Added);
        var again = ledger.Import(CsvParser.Parse(csv), "csv");
        Assert.Equal(0, again.Added);
        Assert.Equal(2, again.Duplicates);
    }

    [Fact]
    public void Extrato_seguinte_com_um_cafe_a_mais_traz_so_o_novo()
    {
        const string one = "Data;Descrição;Valor\n05/09/2026;PADARIA CENTRAL;-5,00\n";
        const string two = "Data;Descrição;Valor\n05/09/2026;PADARIA CENTRAL;-5,00\n05/09/2026;PADARIA CENTRAL;-5,00\n";
        var ledger = NewLedger();
        ledger.Import(CsvParser.Parse(one), "csv");
        Assert.Equal(1, ledger.Import(CsvParser.Parse(two), "csv").Added);
    }

    [Fact]
    public void Corrigir_uma_categoria_ensina_os_outros_lancamentos_do_mesmo_lugar()
    {
        const string csv = "Data;Descrição;Valor\n01/09/2026;COMPRA CARTAO 1234 IFOOD *RESTAURANTE 01/09;-40,00\n" +
                           "08/09/2026;COMPRA CARTAO 1234 IFOOD *RESTAURANTE 08/09;-55,00\n" +
                           "09/09/2026;POSTO SHELL;-200,00\n";
        var ledger = NewLedger();
        ledger.Import(CsvParser.Parse(csv), "csv");

        var result = ledger.Recategorize("t1", "alimentacao");

        Assert.Equal("ifood restaurante", result.LearnedPattern);
        Assert.Equal(1, result.AlsoUpdated);
        Assert.Equal(CategorySource.Manual, ledger.Transactions[0].CategorySource);
        Assert.Equal("alimentacao", ledger.Transactions[1].CategoryId);
        Assert.Equal(CategorySource.Rule, ledger.Transactions[1].CategorySource);
        Assert.Null(ledger.Transactions[2].CategoryId);

        // Próximo extrato já chega categorizado.
        var next = ledger.Import(CsvParser.Parse("Data;Descrição;Valor\n15/09/2026;COMPRA CARTAO 9999 IFOOD *RESTAURANTE 15/09;-30,00\n"), "csv");
        Assert.Equal(1, next.AutoCategorized);
    }

    [Fact]
    public void Regra_nunca_sobrescreve_o_que_a_pessoa_escolheu_na_mao()
    {
        const string csv = "Data;Descrição;Valor\n01/09/2026;UBER TRIP;-20,00\n02/09/2026;UBER TRIP;-25,00\n";
        var ledger = NewLedger();
        ledger.Import(CsvParser.Parse(csv), "csv");
        ledger.Recategorize("t2", "lazer", learn: false);   // essa corrida foi de passeio
        ledger.AddRule("uber", "transporte");

        Assert.Equal("transporte", ledger.Transactions[0].CategoryId);
        Assert.Equal("lazer", ledger.Transactions[1].CategoryId);
    }

    [Fact]
    public void Regra_mais_especifica_e_regra_da_pessoa_ganham()
    {
        List<CategoryRule> rules =
        [
            new("uber", "transporte", RuleOrigin.User),
            new("uber eats", "alimentacao", RuleOrigin.User),
            new("netflix", "outros", RuleOrigin.Learned),
            new("netflix", "lazer", RuleOrigin.User),
        ];
        Assert.Equal("alimentacao", RuleEngine.Match("UBER *EATS PEDIDO", rules)!.CategoryId);
        Assert.Equal("transporte", RuleEngine.Match("UBER TRIP SP", rules)!.CategoryId);
        Assert.Equal("lazer", RuleEngine.Match("NETFLIX.COM", rules)!.CategoryId);
        Assert.Null(RuleEngine.Match("SUBERB LTDA", rules)); // "uber" no meio da palavra não conta
    }

    [Fact]
    public void Regra_pronta_perde_pra_aprendida_e_nao_e_semeada_duas_vezes()
    {
        var ledger = NewLedger();
        ledger.Import(CsvParser.Parse("Data;Descrição;Valor\n01/09/2026;NETFLIX.COM;-39,90\n02/09/2026;NETFLIX.COM;-39,90\n"), "csv");
        Assert.True(ledger.SeedStarterRules());
        Assert.False(ledger.SeedStarterRules());
        Assert.Equal("lazer", ledger.Transactions[1].CategoryId);

        ledger.Recategorize("t1", "moradia"); // a pessoa acha que streaming é conta fixa da casa
        Assert.Equal("moradia", ledger.Transactions[1].CategoryId);
    }

    [Theory]
    [InlineData("COMPRA CARTAO 1234 IFOOD *RESTAURANTE 05/09", "ifood restaurante")]
    [InlineData("PIX ENVIADO JOAO SILVA", "joao silva")]
    [InlineData("PAG*Farmácia São João", "farmacia sao")]
    [InlineData("UBER DO BRASIL TECNOLOGIA", "uber")]
    [InlineData("DEB AUTOMATICO ENEL 0987654", "enel")]
    [InlineData("1234 5678", "")]
    public void Nome_do_estabelecimento_sem_o_ruido(string description, string expected) =>
        Assert.Equal(expected, RuleEngine.MerchantKey(description));

    [Fact]
    public void Estado_sobrevive_ida_e_volta_pelo_JSON()
    {
        var ledger = NewLedger();
        ledger.Import(OfxParser.Parse(OfxParserTests.SgmlStatement), "ofx");
        ledger.Recategorize("t1", "alimentacao");
        ledger.SetBudget("alimentacao", Money.Parse("600,00"));
        ledger.SaveGoal(new SavingsGoal("", "Reserva", Money.Parse("10.000,00"), Money.Parse("2.500,00"), new DateOnly(2027, 6, 30)));

        var restored = LedgerJson.Deserialize(LedgerJson.Serialize(ledger.State));

        Assert.Equal(ledger.State.Transactions, restored.Transactions);
        Assert.Equal(ledger.State.Rules, restored.Rules);
        Assert.Equal(ledger.State.Budgets, restored.Budgets);
        Assert.Equal(ledger.State.Goals, restored.Goals);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{quebrado")]
    public void JSON_ilegivel_vira_estado_novo(string? json) => Assert.Empty(LedgerJson.Deserialize(json).Transactions);
}
