using System.Reflection;
using ClosedXML.Excel;

namespace MotoCare.Api.Services;

public sealed class ExcelExportService
{
    public byte[] Export<T>(string sheetName, IReadOnlyList<T> rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(SanitizeSheetName(sheetName));
        var properties = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public);

        for (var column = 0; column < properties.Length; column++)
        {
            var cell = worksheet.Cell(1, column + 1);
            cell.Value = properties[column].Name;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#17324D");
            cell.Style.Font.FontColor = XLColor.White;
        }

        for (var row = 0; row < rows.Count; row++)
        {
            for (var column = 0; column < properties.Length; column++)
            {
                var value = properties[column].GetValue(rows[row]);
                worksheet.Cell(row + 2, column + 1).Value = XLCellValue.FromObject(value);
            }
        }

        var dataRange = worksheet.Range(1, 1, Math.Max(1, rows.Count + 1), properties.Length);
        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        worksheet.SheetView.FreezeRows(1);
        worksheet.Columns().AdjustToContents(8, 45);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string SanitizeSheetName(string value)
    {
        var invalid = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        var sanitized = string.Concat(value.Select(x => invalid.Contains(x) ? '-' : x));
        return sanitized.Length <= 31 ? sanitized : sanitized[..31];
    }
}
