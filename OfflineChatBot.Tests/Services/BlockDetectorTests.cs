using OfflineChatBot.Models;
using OfflineChatBot.Services.Documents;
using Xunit;

namespace OfflineChatBot.Tests.Services
{
    public class BlockDetectorTests
    {
        [Fact]
        public void APlainTable_TakesTheFirstRowAsTheHeader()
        {
            var grid = Grid(("A1", "Produto"), ("B1", "Preco"), ("A2", "Camisa"), ("B2", "48"));

            var block = Single(grid);

            Assert.Equal(1, block.HeaderRow);
            Assert.Equal(["Produto", "Preco"], block.Headers);
            Assert.Single(block.Rows);
        }

        [Fact]
        public void ATitleAboveTheTable_IsNotMistakenForTheHeader()
        {
            var grid = Grid(("A1", "DESAPEGO LUCAS"), ("A2", "Produto"), ("B2", "Preco"), ("A3", "Camisa"), ("B3", "48"));

            var block = Single(grid);

            Assert.Equal("DESAPEGO LUCAS", block.Title);
            Assert.Equal(2, block.HeaderRow);
            Assert.Equal(["Produto", "Preco"], block.Headers);
        }

        [Fact]
        public void AMergedBannerAndAMergedGroupHeader_AreBothSkipped()
        {
            var grid = Grid(("A1", "Casas"), ("C2", "Planejados"), ("A3", "Descricao"), ("B3", "Metragem"), ("C3", "Cozinha"), ("A4", "Bunker"), ("B4", "158"), ("C4", "sim"));

            grid.Merges.Add(new CellRange(1, 1, 1, 3));
            grid.Merges.Add(new CellRange(2, 3, 2, 3));

            var block = Single(grid);

            Assert.Equal("Casas / Planejados", block.Title);
            Assert.Equal(3, block.HeaderRow);
            Assert.Equal(["Descricao", "Metragem", "Cozinha"], block.Headers);
            Assert.Single(block.Rows);
        }

        [Fact]
        public void ABlankRow_SeparatesTwoTables()
        {
            var grid = Grid(
                ("A1", "Produto"), ("B1", "Preco"), ("A2", "Camisa"), ("B2", "48"),
                ("A4", "Regiao"), ("B4", "Meta"), ("A5", "Sul"), ("B5", "100"));

            var blocks = new BlockDetector().Detect(grid);

            Assert.Equal(2, blocks.Count);
            Assert.Equal(["Produto", "Preco"], blocks[0].Headers);
            Assert.Equal(["Regiao", "Meta"], blocks[1].Headers);
        }

        [Fact]
        public void ALooseTitleAboveABlankRow_BelongsToTheTableBelow()
        {
            var grid = Grid(("A1", "DESAPEGO LUCAS"), ("A3", "Produto"), ("B3", "Preco"), ("A4", "Camisa"), ("B4", "48"));

            var block = Single(grid);

            Assert.Equal("DESAPEGO LUCAS", block.Title);
            Assert.Equal(3, block.HeaderRow);
        }

        [Fact]
        public void ATrailingTotalsRow_IsKeptOutOfTheData()
        {
            var grid = Grid(
                ("A1", "Produto"), ("B1", "Pago"), ("C1", "Vendido"),
                ("A2", "Camisa"), ("B2", "48"), ("C2", "30"),
                ("A3", "Body"), ("B3", "8"), ("C3", "10"),
                ("B4", "56"), ("C4", "40"));

            var block = Single(grid);

            Assert.Equal(2, block.Rows.Count);
            Assert.True(block.HasTotals);
            Assert.Contains("56", block.TotalsRow);
        }

        [Fact]
        public void AFullWidthLastRow_IsData_NotTotals()
        {
            var grid = Grid(
                ("A1", "Produto"), ("B1", "Pago"), ("C1", "Vendido"),
                ("A2", "Camisa"), ("B2", "48"), ("C2", "30"),
                ("A3", "Body"), ("B3", "8"), ("C3", "10"));

            var block = Single(grid);

            Assert.Equal(2, block.Rows.Count);
            Assert.False(block.HasTotals);
        }

        [Fact]
        public void RowsWithHoles_KeepTheirColumnPositions()
        {
            var grid = Grid(
                ("A1", "Produto"), ("B1", "Tamanho"), ("C1", "Marca"),
                ("A2", "Camisa"), ("C2", "Poim"));

            var block = Single(grid);

            Assert.Equal(["Camisa", string.Empty, "Poim"], block.Rows.Single());
        }

        private static SheetBlock Single(SheetGrid grid)
        {
            return Assert.Single(new BlockDetector().Detect(grid));
        }

        private static SheetGrid Grid(params (string Reference, string Value)[] cells)
        {
            var grid = new SheetGrid { Name = "Sheet1" };

            foreach (var (reference, value) in cells)
            {
                var column = reference[0] - 'A' + 1;
                var row = int.Parse(reference[1..]);

                grid.Set(row, column, value);
            }

            return grid;
        }
    }
}
