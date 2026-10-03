# Fluxo

Portal de finanças pessoais em C# de ponta a ponta: Blazor WebAssembly na tela e uma biblioteca
.NET 8 com toda a regra de negócio por baixo. Você importa o extrato do banco (OFX ou CSV), o app
separa por categoria, aprende com as suas correções, acompanha limite de gasto e metas de
economia, e mostra tudo em gráfico.

Roda inteiro no navegador. Nenhum dado sai do computador de quem usa: não tem servidor, não tem
banco de dados remoto, não tem cadastro.

## O que tem de engenharia aqui

O difícil de um app de finanças não é a tela, é o dado que chega dos bancos. Cada decisão abaixo
existe por causa de um problema real de extrato brasileiro.

| Problema | Como foi resolvido | Onde |
|---|---|---|
| `0,1 + 0,2` em ponto flutuante não dá `0,3`. Num ano de extrato o saldo erra. | Dinheiro é `Money`, um `long` de centavos. Nunca passa por `double`. | `Money.cs` |
| Cada banco escreve valor de um jeito: `1.234,56`, `1234.56`, `(10,00)`, `12,30-`. | Um leitor único decide o separador decimal pela última vírgula ou ponto com 1 ou 2 dígitos depois. | `Money.TryParse` |
| OFX 1.x (o que quase todo banco exporta) é SGML: a tag folha não fecha. Nenhum parser XML lê. | Tokenizador próprio que entende SGML e o OFX 2.x em XML com o mesmo código, tolerante a agregado mal fechado. | `Importing/OfxParser.cs` |
| Arquivo em Windows-1252, entidade `&amp;`, data `20260905120000[-3:BRT]`. | Codificação lida do cabeçalho do próprio arquivo; entidades decodificadas; data cortada nos 8 primeiros dígitos. | `OfxParser` |
| CSV não tem padrão: `;` ou `,`, "Histórico" ou "Lançamento", valor numa coluna ou em crédito e débito. | Detecta separador, acha coluna por sinônimo, segue o RFC 4180 (aspas, aspas escapadas, quebra de linha dentro do campo). Linha ruim vira erro com número e o resto entra. | `Importing/CsvParser.cs` |
| Importar o mesmo extrato duas vezes duplicaria tudo. | Cada lançamento tem uma chave estável. OFX usa o `FITID` do banco. | `Importing/ImportKeys.cs` |
| CSV não tem id, e dois cafés de R$ 5,00 no mesmo dia são duas compras reais com dados idênticos. | A chave numera a ocorrência dentro do arquivo (`#0`, `#1`). Os dois cafés entram, e reimportar gera as mesmas chaves. | `ImportKeys` |
| Categorizar na mão todo mês cansa. | Corrigiu um lançamento? O app tira o nome do estabelecimento da descrição ("COMPRA CARTAO 1234 IFOOD *RESTAURANTE 05/09" vira `ifood restaurante`), cria uma regra e aplica nos outros. | `Categorization/RuleEngine.cs` |
| Regra automática não pode desfazer escolha da pessoa. | Cada lançamento guarda de onde veio a categoria. O que foi escolhido na mão nunca muda sozinho. Prioridade: regra da pessoa, depois aprendida, depois a que vem pronta; empate, ganha o padrão mais longo (`uber eats` vence `uber`). | `RuleEngine`, `Ledger.cs` |
| "Estourou o limite" só no fim do mês é tarde. | Projeção pelo ritmo do mês: dia 10 com R$ 300 gastos fecha em R$ 900. Avisa antes de estourar. | `Reports/Reports.cs` |
| Meta de R$ 1.000 em 3 meses dá R$ 333,33, e 3 x 333,33 não fecha. | A parcela mensal arredonda pra cima no centavo. | `ReportBuilder.Goal` |
| Biblioteca de gráfico pesa e esconde a matemática. | Gráficos em SVG desenhados à mão. Escala do eixo com o algoritmo de "nice numbers" (Heckbert); arcos da rosca calculados e testados, inclusive a fatia de 100%, que em SVG precisa virar dois arcos. | `Charts/ChartMath.cs` |
| O Blazor publicado corta código não usado, e serialização por reflexão quebra em silêncio. | JSON gerado em tempo de compilação (`JsonSerializerContext`). | `Persistence/LedgerJson.cs` |
| O Blazor publicado só baixa os dados de idioma do navegador de quem abre. Com `CultureInfo("pt-BR")`, quem usa navegador em inglês via "BRL6,300.00". | Real e mês formatados à mão, iguais em qualquer navegador. Pego num teste de ponta a ponta, não no teste de unidade. | `PtBr.cs` |

## Estrutura

```
src/
  Fluxo.Core/        regra de negócio, sem nada de tela (é o que os testes cobrem)
    Importing/       OFX, CSV e chave de importação
    Categorization/  regras, aprendizado e regras prontas
    Reports/         resumo do mês, categorias, limites e metas
    Charts/          geometria dos gráficos
    Persistence/     JSON do estado
  Fluxo.Web/         Blazor WebAssembly: telas e armazenamento no navegador (localStorage)
tests/
  Fluxo.Tests/       74 testes xUnit, rodando no GitHub Actions a cada envio
```

## Telas

- **Painel:** entrou, saiu e sobrou no mês com variação contra o mês anterior, gráfico dos
  últimos 6 meses, gastos por categoria, limites e metas.
- **Lançamentos:** busca, filtro de "sem categoria" e troca de categoria na própria linha. Ao
  trocar, o app avisa o que aprendeu e quantos outros lançamentos atualizou.
- **Importar:** OFX ou CSV, vários arquivos de uma vez, com resumo do que entrou, do que já
  existia e do que não deu pra ler.
- **Metas:** limite mensal por categoria e metas de economia com prazo.
- **Regras:** as suas, as aprendidas e as prontas, separadas.

Tem um botão de **dados de exemplo** com quatro meses de extrato fictício, marcado como exemplo
na tela, pra ver o app funcionando sem importar nada.

## Rodar

Precisa do .NET 8 SDK.

```bash
dotnet test                       # 74 testes
dotnet run --project src/Fluxo.Web
```

Publicar:

- **Vercel:** importe o repositório e pronto. O `vercel.json` da raiz instala o .NET 8 no build,
  publica o Blazor e manda as rotas do app pro `index.html`.
- **Qualquer hospedagem estática:** `dotnet publish src/Fluxo.Web -c Release -o publish` e suba a
  pasta `publish/wwwroot`, com as rotas desconhecidas apontando pro `index.html`.

## Decisões assumidas

- **Dados só no navegador (`localStorage`).** É privado e não custa servidor, mas os dados ficam
  presos àquele navegador e somem se a pessoa limpar os dados do site. O caminho natural pra
  evoluir é exportar e importar um arquivo de backup, ou trocar o `LedgerService` por um que
  sincronize com uma API. O `Ledger` não muda em nenhum dos dois casos.
- **Uma conta só.** O OFX traz o número da conta e ela entra na chave de importação, mas a tela
  ainda não separa por conta.
- **Transferência entre contas próprias conta como gasto.** Separar exigiria casar a saída de uma
  conta com a entrada da outra, o que depende de ter as duas importadas.

## Licença

MIT
