using System.Globalization;
using System.Text;

namespace JobSort.Core;

/// <summary>Turns jobs into CSV that opens cleanly in Excel or Google Sheets.</summary>
public static class CsvExporter
{
    public const string Header =
        "Title,Company,Location,Pay,Pay Period,Status,Listed On,Applied On,Interview On,Skills,Posting URL,Notes";

    public static string ToCsv(IEnumerable<Job> jobs)
    {
        var sb = new StringBuilder();
        sb.Append(Header).Append("\r\n");
        foreach (Job j in jobs)
        {
            string[] fields =
            {
                j.Title, j.Company, j.Location,
                j.Pay?.ToString(CultureInfo.InvariantCulture) ?? "",
                j.PayPeriod.ToString(), j.Status.ToString(),
                Date(j.ListedOn), Date(j.AppliedOn), Date(j.InterviewOn),
                string.Join("; ", j.Skills), j.PostingUrl, j.Notes,
            };
            sb.Append(string.Join(",", fields.Select(Escape))).Append("\r\n");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Quotes a field if it contains a comma, quote or line break (doubling any quotes).
    /// Fields starting with = + - or @ get a leading apostrophe so spreadsheet apps
    /// don't run them as formulas.
    /// </summary>
    public static string Escape(string field)
    {
        if (field.Length > 0 && "=+-@".Contains(field[0])) field = "'" + field;
        return field.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
            ? "\"" + field.Replace("\"", "\"\"") + "\""
            : field;
    }

    static string Date(DateOnly? d) => d?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "";
}
