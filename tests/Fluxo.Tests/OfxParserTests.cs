using System.Text;
using Fluxo.Core;
using Fluxo.Core.Importing;

namespace Fluxo.Tests;

public class OfxParserTests
{
    // OFX 1.x como os bancos daqui exportam: SGML, folha sem fechamento, vírgula decimal, fuso.
    internal const string SgmlStatement = """
        OFXHEADER:100
        DATA:OFXSGML
        VERSION:102
        SECURITY:NONE
        ENCODING:USASCII
        CHARSET:1252
        COMPRESSION:NONE
        OLDFILEUID:NONE
        NEWFILEUID:NONE

        <OFX>
        <SIGNONMSGSRSV1><SONRS><STATUS><CODE>0<SEVERITY>INFO</STATUS><DTSERVER>20260930120000[-3:BRT]<LANGUAGE>POR</SONRS></SIGNONMSGSRSV1>
        <BANKMSGSRSV1>
        <STMTTRNRS>
        <STMTRS>
        <CURDEF>BRL
        <BANKACCTFROM>
        <BANKID>0341
        <ACCTID>12345-6
        <ACCTTYPE>CHECKING
        </BANKACCTFROM>
        <BANKTRANLIST>
        <DTSTART>20260901
        <DTEND>20260930
        <STMTTRN>
        <TRNTYPE>DEBIT
        <DTPOSTED>20260905120000[-3:BRT]
        <TRNAMT>-45,90
        <FITID>202609050001
        <MEMO>COMPRA CARTAO 1234 IFOOD *RESTAURANTE 05/09
        </STMTTRN>
        <STMTTRN>
        <TRNTYPE>CREDIT
        <DTPOSTED>20260905
        <TRNAMT>5000.00
        <FITID>202609050002
        <NAME>SALARIO
        <MEMO>SALARIO
        </STMTTRN>
        <STMTTRN>
        <TRNTYPE>DEBIT
        <DTPOSTED>20260910
        <TRNAMT>-120,00
        <FITID>202609100003
        <MEMO>PAG*FARMÁCIA SÃO JOÃO &amp; CIA
        </STMTTRN>
        </BANKTRANLIST>
        </STMTRS>
        </STMTTRNRS>
        </BANKMSGSRSV1>
        </OFX>
        """;

    [Fact]
    public void Le_OFX_SGML_de_banco_brasileiro()
    {
        var result = OfxParser.Parse(SgmlStatement);

        Assert.Empty(result.Errors);
        Assert.Equal("12345-6", result.AccountId);
        Assert.Equal(3, result.Entries.Count);

        var ifood = result.Entries[0];
        Assert.Equal(new DateOnly(2026, 9, 5), ifood.Date);
        Assert.Equal(-4590, ifood.Amount.Cents);
        Assert.Equal("202609050001", ifood.ExternalId);
        Assert.Contains("IFOOD", ifood.Description);
    }

    [Fact]
    public void Name_e_memo_iguais_nao_viram_descricao_repetida()
    {
        var salario = OfxParser.Parse(SgmlStatement).Entries[1];
        Assert.Equal("SALARIO", salario.Description);
        Assert.Equal(500000, salario.Amount.Cents);
    }

    [Fact]
    public void Decodifica_entidade_e_acento_em_Windows_1252()
    {
        var bytes = Encoding.GetEncoding(1252).GetBytes(SgmlStatement);
        var result = OfxParser.Parse(bytes);
        Assert.Equal("PAG*FARMÁCIA SÃO JOÃO & CIA", result.Entries[2].Description);
    }

    [Fact]
    public void Le_OFX_2_em_XML()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <?OFX OFXHEADER="200" VERSION="220"?>
            <OFX>
              <BANKMSGSRSV1><STMTTRNRS><STMTRS>
                <BANKACCTFROM><BANKID>260</BANKID><ACCTID>998877</ACCTID></BANKACCTFROM>
                <BANKTRANLIST>
                  <STMTTRN><TRNTYPE>DEBIT</TRNTYPE><DTPOSTED>20260801</DTPOSTED><TRNAMT>-19.90</TRNAMT><FITID>a1</FITID><MEMO>NETFLIX.COM</MEMO></STMTTRN>
                  <STMTTRN><TRNTYPE>DEBIT</TRNTYPE><DTPOSTED>20260802</DTPOSTED><TRNAMT>-8.50</TRNAMT><FITID>a2</FITID><MEMO>UBER TRIP</MEMO></STMTTRN>
                </BANKTRANLIST>
              </STMTRS></STMTTRNRS></BANKMSGSRSV1>
            </OFX>
            """;
        var result = OfxParser.Parse(xml);
        Assert.Empty(result.Errors);
        Assert.Equal("998877", result.AccountId);
        Assert.Equal(["NETFLIX.COM", "UBER TRIP"], result.Entries.Select(e => e.Description));
        Assert.Equal(-850, result.Entries[1].Amount.Cents);
    }

    [Fact]
    public void Lancamento_ruim_vira_erro_e_o_resto_entra()
    {
        var broken = SgmlStatement.Replace("<TRNAMT>-120,00", "<TRNAMT>abc");
        var result = OfxParser.Parse(broken);
        Assert.Equal(2, result.Entries.Count);
        var error = Assert.Single(result.Errors);
        Assert.Contains("valor inválido", error.Message);
    }

    [Fact]
    public void Arquivo_que_nao_e_OFX_explica_o_problema()
    {
        var result = OfxParser.Parse("data;valor\n01/01/2026;10");
        Assert.Empty(result.Entries);
        Assert.Contains("não parece um extrato OFX", Assert.Single(result.Errors).Message);
    }

    [Theory]
    [InlineData("20260905", 2026, 9, 5)]
    [InlineData("20260905120000", 2026, 9, 5)]
    [InlineData("20260905120000.000[-3:BRT]", 2026, 9, 5)]
    public void Datas_com_hora_e_fuso(string raw, int y, int m, int d)
    {
        Assert.True(OfxParser.TryParseDate(raw, out var date));
        Assert.Equal(new DateOnly(y, m, d), date);
    }
}
