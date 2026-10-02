using Fluxo.Core.Importing;

namespace Fluxo.Tests;

public class CsvParserTests
{
    [Fact]
    public void Le_CSV_de_banco_com_ponto_e_virgula_e_virgula_decimal()
    {
        const string csv = "﻿Data;Histórico;Valor\r\n05/09/2026;PIX ENVIADO JOAO;-150,00\r\n06/09/2026;SALARIO;5.000,00\r\n";
        var result = CsvParser.Parse(csv);
        Assert.Empty(result.Errors);
        Assert.Equal(2, result.Entries.Count);
        Assert.Equal(-15000, result.Entries[0].Amount.Cents);
        Assert.Equal(500000, result.Entries[1].Amount.Cents);
        Assert.Equal(new DateOnly(2026, 9, 6), result.Entries[1].Date);
    }

    [Fact]
    public void Campo_entre_aspas_com_separador_aspas_e_quebra_de_linha()
    {
        const string csv = "date,description,amount\n2026-09-01,\"Mercado \"\"Bom Preço\"\", loja 2\",-89.90\n2026-09-02,\"linha\nquebrada\",-1.00\n";
        var result = CsvParser.Parse(csv);
        Assert.Empty(result.Errors);
        Assert.Equal("Mercado \"Bom Preço\", loja 2", result.Entries[0].Description);
        Assert.Equal("linha\nquebrada", result.Entries[1].Description);
        Assert.Equal(-8990, result.Entries[0].Amount.Cents);
    }

    [Fact]
    public void Colunas_separadas_de_credito_e_debito()
    {
        const string csv = "Data;Lançamento;Crédito;Débito\n01/09/2026;SALARIO;3.000,00;\n02/09/2026;ALUGUEL;;1.500,00\n";
        var result = CsvParser.Parse(csv);
        Assert.Empty(result.Errors);
        Assert.Equal([300000L, -150000L], result.Entries.Select(e => e.Amount.Cents));
    }

    [Fact]
    public void Linha_ruim_vira_erro_com_numero_e_o_resto_entra()
    {
        const string csv = "Data;Descrição;Valor\n01/09/2026;OK;-10,00\n32/09/2026;DATA RUIM;-5,00\n03/09/2026;VALOR RUIM;dez reais\n04/09/2026;OK 2;-1,00\n";
        var result = CsvParser.Parse(csv);
        Assert.Equal(2, result.Entries.Count);
        Assert.Equal([3, 4], result.Errors.Select(e => e.Line));
    }

    [Fact]
    public void Linha_de_saldo_sem_data_e_ignorada_sem_erro()
    {
        const string csv = "Data;Descrição;Valor\n;SALDO ANTERIOR;1.000,00\n01/09/2026;CAFE;-5,00\nTotal;;995,00\n";
        var result = CsvParser.Parse(csv);
        Assert.Single(result.Entries);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Cabecalho_sem_colunas_conhecidas_explica_o_que_achou()
    {
        var result = CsvParser.Parse("foo;bar\n1;2\n");
        Assert.Empty(result.Entries);
        Assert.Contains("Colunas encontradas: foo, bar", Assert.Single(result.Errors).Message);
    }

    [Theory]
    [InlineData("a;b;c", ';')]
    [InlineData("a,b,c", ',')]
    [InlineData("a\tb\tc", '\t')]
    [InlineData("\"x;y\",b,c", ',')]
    public void Descobre_o_separador(string header, char expected) => Assert.Equal(expected, CsvParser.DetectDelimiter(header));
}
