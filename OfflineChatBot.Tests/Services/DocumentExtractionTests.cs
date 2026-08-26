using System.IO;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using OfflineChatBot.Services.Abstractions;
using OfflineChatBot.Services.Documents;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace OfflineChatBot.Tests.Services
{
    public class DocumentExtractionTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), $"offlinechatbot-tests-{Guid.NewGuid():N}");

        public DocumentExtractionTests()
        {
            Directory.CreateDirectory(_folder);
        }

        public void Dispose()
        {
            Directory.Delete(_folder, recursive: true);
        }

        [Fact]
        public async Task PlainTextExtractor_ReadsTheFileAsItIs()
        {
            var path = WriteText("notes.txt", "First line\nSecond line");

            var text = await new PlainTextExtractor().ExtractAsync(path);

            Assert.Equal("First line\nSecond line", text);
        }

        [Theory]
        [InlineData("notes.txt", true)]
        [InlineData("notes.md", true)]
        [InlineData("data.csv", true)]
        [InlineData("report.pdf", false)]
        [InlineData("letter.docx", false)]
        public void PlainTextExtractor_HandlesOnlyTextFormats(string fileName, bool expected)
        {
            Assert.Equal(expected, new PlainTextExtractor().CanHandle(fileName));
        }

        [Fact]
        public async Task PdfTextExtractor_ReadsTheTextOfEveryPage()
        {
            var path = WritePdf("report.pdf", "Revenue grew twelve percent", "Costs stayed flat");

            var text = await new PdfTextExtractor().ExtractAsync(path);

            Assert.Contains("Revenue grew twelve percent", text);
            Assert.Contains("Costs stayed flat", text);
        }

        [Fact]
        public async Task WordTextExtractor_ReadsEveryParagraph()
        {
            var path = WriteWord("letter.docx", "Dear customer", "Your order has shipped");

            var text = await new WordTextExtractor().ExtractAsync(path);

            Assert.Contains("Dear customer", text);
            Assert.Contains("Your order has shipped", text);
        }

        [Fact]
        public async Task CompositeTextExtractor_PicksTheReaderByExtension()
        {
            var composite = CreateComposite();

            Assert.Contains("Dear customer", await composite.ExtractAsync(WriteWord("letter.docx", "Dear customer")));
            Assert.Contains("Revenue grew", await composite.ExtractAsync(WritePdf("report.pdf", "Revenue grew")));
            Assert.Contains("plain", await composite.ExtractAsync(WriteText("notes.txt", "plain")));
        }

        [Fact]
        public void CompositeTextExtractor_RejectsUnsupportedFormats()
        {
            var composite = CreateComposite();

            Assert.False(composite.CanHandle("spreadsheet.xlsx"));
            Assert.False(composite.CanHandle("legacy.doc"));
        }

        [Fact]
        public async Task CompositeTextExtractor_ThrowsWhenNoReaderMatches()
        {
            var composite = CreateComposite();

            await Assert.ThrowsAsync<NotSupportedException>(() => composite.ExtractAsync("spreadsheet.xlsx"));
        }

        private static IDocumentTextExtractor CreateComposite()
        {
            return new CompositeTextExtractor([new PlainTextExtractor(), new PdfTextExtractor(), new WordTextExtractor()]);
        }

        private string WriteText(string fileName, string content)
        {
            var path = Path.Combine(_folder, fileName);

            File.WriteAllText(path, content);

            return path;
        }

        private string WritePdf(string fileName, params string[] pages)
        {
            var path = Path.Combine(_folder, fileName);
            var builder = new PdfDocumentBuilder();
            var font = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Helvetica);

            foreach (var content in pages)
                builder.AddPage(600, 800).AddText(content, 12, new UglyToad.PdfPig.Core.PdfPoint(30, 700), font);

            File.WriteAllBytes(path, builder.Build());

            return path;
        }

        private string WriteWord(string fileName, params string[] paragraphs)
        {
            var path = Path.Combine(_folder, fileName);

            using (var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
            {
                var body = document.AddMainDocumentPart().Document = new Document(new Body());

                foreach (var content in paragraphs)
                    body.Body!.AppendChild(new Paragraph(new Run(new Text(content))));
            }

            return path;
        }
    }
}
