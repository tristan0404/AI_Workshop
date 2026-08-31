using System.Globalization;
using System.Text;
using ClosedXML.Excel;

namespace AI_Workshop.Services;

public interface IAttendanceSpreadsheetReader
{
    Task<SpreadsheetTable> ReadAsync(Stream stream, string extension, int maximumRows, CancellationToken cancellationToken);
}

public sealed record SpreadsheetTable(IReadOnlyList<string> Headers, IReadOnlyList<SpreadsheetRow> Rows);
public sealed record SpreadsheetRow(int Number, IReadOnlyList<string> Cells);

public sealed class AttendanceSpreadsheetReader : IAttendanceSpreadsheetReader
{
    public async Task<SpreadsheetTable> ReadAsync(Stream stream, string extension, int maximumRows, CancellationToken cancellationToken)
    {
        return extension.ToLowerInvariant() switch
        {
            ".csv" => await ReadCsvAsync(stream, maximumRows, cancellationToken),
            ".xlsx" => ReadExcel(stream, maximumRows),
            _ => throw new AttendanceImportException("Only .csv and .xlsx files are supported.")
        };
    }

    private static async Task<SpreadsheetTable> ReadCsvAsync(Stream stream, int maximumRows, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, true, leaveOpen: true);
        var content = await reader.ReadToEndAsync(cancellationToken);
        var records = ParseCsv(content, DetectDelimiter(content));
        return BuildTable(records, maximumRows);
    }

    private static SpreadsheetTable ReadExcel(Stream stream, int maximumRows)
    {
        try
        {
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.FirstOrDefault()
                ?? throw new AttendanceImportException("The workbook has no worksheets.");
            var range = worksheet.RangeUsed()
                ?? throw new AttendanceImportException("The first worksheet is empty.");
            var columnCount = range.ColumnCount();
            var records = range.Rows().Select(row =>
                Enumerable.Range(1, columnCount).Select(column =>
                {
                    var cell = row.Cell(column);
                    return cell.DataType == XLDataType.DateTime
                        ? cell.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                        : cell.GetFormattedString().Trim();
                }).ToList()).ToList();
            return BuildTable(records, maximumRows);
        }
        catch (AttendanceImportException) { throw; }
        catch (Exception exception)
        {
            throw new AttendanceImportException("The Excel workbook could not be read. Check that it is a valid .xlsx file.", exception);
        }
    }

    private static SpreadsheetTable BuildTable(IReadOnlyList<List<string>> records, int maximumRows)
    {
        if (records.Count == 0) throw new AttendanceImportException("The file is empty.");
        if (records.Count - 1 > maximumRows) throw new AttendanceImportException($"The file exceeds the {maximumRows:N0}-row limit.");
        var headers = records[0].Select(value => value.Trim()).ToList();
        var rows = records.Skip(1)
            .Where(record => record.Any(value => !string.IsNullOrWhiteSpace(value)))
            .Select((record, index) => new SpreadsheetRow(index + 2,
                Enumerable.Range(0, headers.Count).Select(column => column < record.Count ? record[column].Trim() : string.Empty).ToList()))
            .ToList();
        return new SpreadsheetTable(headers, rows);
    }

    private static char DetectDelimiter(string content)
    {
        var commaCount = 0;
        var semicolonCount = 0;
        var quoted = false;
        foreach (var character in content)
        {
            if (character == '"') quoted = !quoted;
            else if (!quoted && character is '\r' or '\n') break;
            else if (!quoted && character == ',') commaCount++;
            else if (!quoted && character == ';') semicolonCount++;
        }

        if (commaCount == 0 && semicolonCount == 0)
            throw new AttendanceImportException("The CSV header must use commas or semicolons between columns.");
        return semicolonCount > commaCount ? ';' : ',';
    }

    private static List<List<string>> ParseCsv(string content, char delimiter)
    {
        var records = new List<List<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var index = 0; index < content.Length; index++)
        {
            var character = content[index];
            if (character == '"')
            {
                if (quoted && index + 1 < content.Length && content[index + 1] == '"') { field.Append('"'); index++; }
                else quoted = !quoted;
            }
            else if (character == delimiter && !quoted) { record.Add(field.ToString()); field.Clear(); }
            else if ((character == '\r' || character == '\n') && !quoted)
            {
                if (character == '\r' && index + 1 < content.Length && content[index + 1] == '\n') index++;
                record.Add(field.ToString()); field.Clear(); records.Add(record); record = [];
            }
            else field.Append(character);
        }
        if (quoted) throw new AttendanceImportException("The CSV file contains an unclosed quoted value.");
        if (field.Length > 0 || record.Count > 0) { record.Add(field.ToString()); records.Add(record); }
        return records;
    }
}

public sealed class AttendanceImportException(string message, Exception? innerException = null) : Exception(message, innerException);
