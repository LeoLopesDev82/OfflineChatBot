using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using OfflineChatBot.Models;

namespace OfflineChatBot.Services.Documents
{
    public sealed class WorkbookReader
    {
        private static readonly uint[] BuiltInDateFormats = { 14, 15, 16, 17, 22, 45, 46, 47 };

        public List<SheetGrid> Read(string filePath)
        {
            using var document = SpreadsheetDocument.Open(filePath, false);

            var workbookPart = document.WorkbookPart ?? throw new InvalidOperationException("The workbook has no content.");
            var sharedStrings = ReadSharedStrings(workbookPart);
            var dateStyles = ReadDateStyles(workbookPart);

            return workbookPart.Workbook?.Sheets?
                .OfType<Sheet>()
                .Select(sheet => ReadSheet(workbookPart, sheet, sharedStrings, dateStyles))
                .Where(grid => grid.LastRow > 0)
                .ToList() ?? new List<SheetGrid>();
        }

        #region Private Methods

        private static SheetGrid ReadSheet(WorkbookPart workbookPart, Sheet sheet, List<string> sharedStrings, HashSet<int> dateStyles)
        {
            var grid = new SheetGrid { Name = sheet.Name?.Value ?? "Sheet" };
            var part = (WorksheetPart)workbookPart.GetPartById(sheet.Id!.Value!);
            var worksheet = part.Worksheet ?? throw new InvalidOperationException($"The sheet {grid.Name} has no content.");

            foreach (var cell in worksheet.Descendants<Cell>())
            {
                var reference = cell.CellReference?.Value;

                if (reference == null)
                    continue;

                var (row, column) = Locate(reference);

                grid.Set(row, column, ValueOf(cell, sharedStrings, dateStyles));
            }

            foreach (var merge in worksheet.Descendants<MergeCell>())
                AddMerge(grid, merge.Reference?.Value);

            return grid;
        }

        private static void AddMerge(SheetGrid grid, string? reference)
        {
            if (reference == null || !reference.Contains(':'))
                return;

            var parts = reference.Split(':');
            var first = Locate(parts[0]);
            var last = Locate(parts[1]);

            grid.Merges.Add(new CellRange(first.Row, first.Column, last.Row, last.Column));
        }

        private static List<string> ReadSharedStrings(WorkbookPart workbookPart)
        {
            return workbookPart.SharedStringTablePart?.SharedStringTable?
                .Elements<SharedStringItem>()
                .Select(item => item.InnerText)
                .ToList() ?? new List<string>();
        }

        private static HashSet<int> ReadDateStyles(WorkbookPart workbookPart)
        {
            var styles = new HashSet<int>();
            var stylesheet = workbookPart.WorkbookStylesPart?.Stylesheet;

            if (stylesheet?.CellFormats == null)
                return styles;

            var custom = CustomDateFormats(stylesheet);
            var index = 0;

            foreach (var format in stylesheet.CellFormats.OfType<CellFormat>())
            {
                var numberFormat = format.NumberFormatId?.Value ?? 0;

                if (BuiltInDateFormats.Contains(numberFormat) || custom.Contains(numberFormat))
                    styles.Add(index);

                index++;
            }

            return styles;
        }

        private static HashSet<uint> CustomDateFormats(Stylesheet stylesheet)
        {
            return stylesheet.NumberingFormats?
                .OfType<NumberingFormat>()
                .Where(format => LooksLikeDate(format.FormatCode?.Value))
                .Select(format => format.NumberFormatId?.Value ?? 0)
                .ToHashSet() ?? new HashSet<uint>();
        }

        private static bool LooksLikeDate(string? formatCode)
        {
            if (string.IsNullOrEmpty(formatCode))
                return false;

            return formatCode.Contains('y') || formatCode.Contains("dd") || formatCode.Contains("mmm");
        }

        private static string ValueOf(Cell cell, List<string> sharedStrings, HashSet<int> dateStyles)
        {
            var raw = cell.CellValue?.InnerText ?? string.Empty;

            if (cell.DataType?.Value == CellValues.SharedString)
                return int.TryParse(raw, out var index) && index < sharedStrings.Count ? sharedStrings[index] : string.Empty;

            if (cell.DataType?.Value == CellValues.InlineString)
                return cell.InlineString?.InnerText ?? string.Empty;

            if (cell.DataType?.Value == CellValues.Boolean)
                return raw == "1" ? "TRUE" : "FALSE";

            return IsDate(cell, dateStyles) ? AsDate(raw) : raw;
        }

        private static bool IsDate(Cell cell, HashSet<int> dateStyles)
        {
            return cell.StyleIndex?.Value is { } style && dateStyles.Contains((int)style);
        }

        private static string AsDate(string raw)
        {
            if (!double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var serial))
                return raw;

            return DateTime.FromOADate(serial).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private static (int Row, int Column) Locate(string reference)
        {
            var column = 0;
            var position = 0;

            while (position < reference.Length && char.IsLetter(reference[position]))
            {
                column = column * 26 + (char.ToUpperInvariant(reference[position]) - 'A' + 1);
                position++;
            }

            return (int.Parse(reference[position..]), column);
        }

        #endregion
    }
}
