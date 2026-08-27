using OfflineChatBot.Models;
using OfflineChatBot.Services.Documents;
using Xunit;

namespace OfflineChatBot.Tests.Services
{
    public class QueryRunnerTests
    {
        [Fact]
        public void PlainJson_IsParsed()
        {
            var query = QueryRunner.Parse("{\"table\":\"Vendas\",\"operation\":\"sum\",\"column\":\"Valor\"}");

            Assert.Equal("sum", query!.Operation);
            Assert.Equal("Valor", query.Column);
        }

        [Fact]
        public void JsonWrappedInChatter_IsStillParsed()
        {
            var query = QueryRunner.Parse("Here you go:\n{\"operation\":\"sum\",\"column\":\"Valor\"}\nHope it helps.");

            Assert.Equal("sum", query!.Operation);
        }

        [Fact]
        public void ACountWithoutFilters_IsRejectedBecauseTheSummaryAlreadySaysIt()
        {
            Assert.Null(QueryRunner.Parse("{\"operation\":\"count\",\"column\":\"Produto\",\"filters\":[]}"));
        }

        [Fact]
        public void ANullLimit_DoesNotBreakParsing()
        {
            var query = QueryRunner.Parse("{\"operation\":\"sum\",\"column\":\"Valor\",\"limit\":null}");

            Assert.NotNull(query);
            Assert.Null(query!.Limit);
        }

        [Fact]
        public void UnbalancedClosingBraces_AreRepaired()
        {
            var query = QueryRunner.Parse("{\"operation\":\"count\",\"column\":\"P\",\"filters\":[{\"column\":\"MARCA\",\"equals\":\"Carters\"}}}");

            Assert.Equal("count", query!.Operation);
            Assert.Equal("MARCA", query.Filters.Single().Column);
        }

        [Fact]
        public void AnUnclosedObject_IsRepaired()
        {
            var query = QueryRunner.Parse("{\"operation\":\"sum\",\"column\":\"Valor\"");

            Assert.Equal("sum", query!.Operation);
        }

        [Fact]
        public void BracesInsideStrings_DoNotConfuseTheRepair()
        {
            var query = QueryRunner.Parse("{\"operation\":\"sum\",\"column\":\"a}b\"}");

            Assert.Equal("a}b", query!.Column);
        }

        [Fact]
        public void TextWithoutJson_IsRejected()
        {
            Assert.Null(QueryRunner.Parse("I cannot answer that with a query."));
        }

        [Fact]
        public void AQueryWithoutAnOperation_IsRejected()
        {
            Assert.Null(QueryRunner.Parse("{\"column\":\"Valor\"}"));
        }

        [Fact]
        public void AListWithoutFilters_IsRejectedBecauseItOnlyRepeatsTheContext()
        {
            Assert.Null(QueryRunner.Parse("{\"operation\":\"list\",\"column\":\"\",\"filters\":[]}"));
        }

        [Fact]
        public void AListWithFilters_IsAccepted()
        {
            var query = QueryRunner.Parse("{\"operation\":\"list\",\"filters\":[{\"column\":\"Marca\",\"equals\":\"Poim\"}]}");

            Assert.Equal("list", query!.Operation);
        }

        [Fact]
        public void SumAddsOnlyTheNumericValues()
        {
            var result = Run("{\"operation\":\"sum\",\"column\":\"Pago\"}");

            Assert.True(result.Answered);
            Assert.Contains("sum = 60", result.Text);
            Assert.Contains("over 2 rows", result.Text);
        }

        [Fact]
        public void AFilterNarrowsTheRows()
        {
            var result = Run("{\"operation\":\"count\",\"filters\":[{\"column\":\"Marca\",\"equals\":\"Poim\"}]}");

            Assert.Contains("**1** rows match", result.Text);
            Assert.Contains("Camisa", result.Text);
        }

        [Fact]
        public void DistinctListsTheValues()
        {
            var result = Run("{\"operation\":\"distinct\",\"column\":\"Marca\"}");

            Assert.Contains("Poim", result.Text);
            Assert.Contains("Carters", result.Text);
        }

        [Fact]
        public void AnUnknownColumn_IsReportedInsteadOfGuessed()
        {
            var result = Run("{\"operation\":\"sum\",\"column\":\"Lucro\"}");

            Assert.False(result.Answered);
            Assert.Contains("no column called \"Lucro\"", result.Text);
        }

        [Fact]
        public void AColumnWithNoNumbers_SaysSoInsteadOfReturningZero()
        {
            var result = Run("{\"operation\":\"sum\",\"column\":\"Marca\"}");

            Assert.False(result.Answered);
            Assert.Contains("holds no numeric values", result.Text);
        }

        [Fact]
        public void AFilterOnAColumnThatDoesNotExist_IsRefusedInsteadOfAnswered()
        {
            var result = Run("{\"operation\":\"count\",\"filters\":[{\"column\":\"Regiao\",\"equals\":\"Sul\"}]}");

            Assert.False(result.Answered);
            Assert.Contains("no column called \"Regiao\"", result.Text);
        }

        [Fact]
        public void AFilterOnAValueNoRowHolds_IsRefusedInsteadOfAnsweringZero()
        {
            var result = Run("{\"operation\":\"count\",\"filters\":[{\"column\":\"Marca\",\"equals\":\"Nike\"}]}");

            Assert.False(result.Answered);
            Assert.Contains("matches nothing", result.Text);
        }

        [Fact]
        public void AColumnNameThatMatchesTwoHeaders_IsRefused()
        {
            var block = new SheetBlock { SheetName = "Sheet1" };

            block.Headers.AddRange(["escritorio (E)", "escritorio (I)", "valor"]);
            block.Rows.Add(["sim", "não", "10"]);

            var result = new QueryRunner().Run([block], QueryRunner.Parse("{\"operation\":\"distinct\",\"column\":\"escritorio\"}")!);

            Assert.False(result.Answered);
            Assert.Contains("ambiguous", result.Text);
            Assert.Contains("escritorio (E)", result.Text);
        }

        private static QueryOutcome Run(string json)
        {
            var block = new SheetBlock { SheetName = "Sheet1", Title = "Desapego" };

            block.Headers.AddRange(["Produto", "Marca", "Pago"]);
            block.Rows.Add(["Camisa", "Poim", "48"]);
            block.Rows.Add(["Body", "Carters", "12"]);
            block.Rows.Add(["Malha", "Carters", "presente"]);

            return new QueryRunner().Run([block], QueryRunner.Parse(json)!);
        }
    }
}
