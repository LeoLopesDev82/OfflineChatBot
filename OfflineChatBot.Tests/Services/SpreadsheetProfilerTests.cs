using OfflineChatBot.Models;
using OfflineChatBot.Services.Documents;
using Xunit;

namespace OfflineChatBot.Tests.Services
{
    public class SpreadsheetProfilerTests
    {
        [Fact]
        public void ANumericColumn_IsSummedExactly()
        {
            var profile = Profile(["Preco"], ["10"], ["20"], ["30.5"]);

            var column = profile.Columns.Single();

            Assert.Equal(ValueKind.Number, column.Kind);
            Assert.Equal(60.5, column.Sum);
            Assert.Equal(10, column.Minimum);
            Assert.Equal(30.5, column.Maximum);
        }

        [Fact]
        public void TextMixedIntoANumericColumn_IsLeftOutOfTheFigures()
        {
            var profile = Profile(["Metragem"], ["155"], ["158"], ["300 de terreno"]);

            var column = profile.Columns.Single();

            Assert.Equal(ValueKind.Number, column.Kind);
            Assert.Equal(3, column.FilledRows);
            Assert.Equal(2, column.NumericRows);
            Assert.Equal(313, column.Sum);
        }

        [Fact]
        public void AColumnWithMoreTextThanNumbers_IsText()
        {
            var profile = Profile(["Pago"], ["presente"], ["presente"], ["presente"], ["40"]);

            Assert.Equal(ValueKind.Text, profile.Columns.Single().Kind);
        }

        [Fact]
        public void CurrencyAndThousandSeparators_AreUnderstood()
        {
            var profile = Profile(["Valor"], ["R$ 1.234,56"], ["R$ 765,44"]);

            Assert.Equal(2000, profile.Columns.Single().Sum);
        }

        [Fact]
        public void ADateColumn_ReportsItsRange()
        {
            var profile = Profile(["Data"], ["2025-03-10"], ["2024-01-05"], ["2025-12-31"]);

            var column = profile.Columns.Single();

            Assert.Equal(ValueKind.Date, column.Kind);
            Assert.Equal("2024-01-05", column.Earliest);
            Assert.Equal("2025-12-31", column.Latest);
        }

        [Fact]
        public void ATextColumnWithFewValues_ListsThem()
        {
            var profile = Profile(["Regiao"], ["Sul"], ["Norte"], ["Sul"]);

            var column = profile.Columns.Single();

            Assert.Equal(2, column.DistinctCount);
            Assert.Equal(["Sul", "Norte"], column.DistinctValues);
        }

        [Fact]
        public void AnEmptyColumn_IsReportedAsEmpty()
        {
            var profile = Profile(["Produto", "Local"], ["Camisa", ""], ["Body", ""]);

            Assert.Equal(ValueKind.Empty, profile.Columns[1].Kind);
            Assert.Equal(0, profile.Columns[1].FilledRows);
        }

        [Fact]
        public void ATotalsRow_IsReportedApartFromTheData()
        {
            var block = Block(["Produto", "Valor"], ["Camisa", "10"], ["Body", "20"]);

            block.TotalsRow.AddRange(["", "30"]);

            var profile = new SpreadsheetProfiler().Profile(block);

            Assert.Equal(2, profile.RowCount);
            Assert.Equal(30, profile.Columns[1].Sum);
            Assert.Equal(["30"], profile.Totals);
        }

        private static BlockProfile Profile(string[] headers, params string[][] rows)
        {
            return new SpreadsheetProfiler().Profile(Block(headers, rows));
        }

        private static SheetBlock Block(string[] headers, params string[][] rows)
        {
            var block = new SheetBlock { SheetName = "Sheet1", HeaderRow = 1, Range = new CellRange(1, 1, rows.Length + 1, headers.Length) };

            block.Headers.AddRange(headers);

            foreach (var row in rows)
                block.Rows.Add(row.ToList());

            return block;
        }
    }
}
