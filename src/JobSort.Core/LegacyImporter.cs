using System.Globalization;
using System.Text.Json;

namespace JobSort.Core;

/// <summary>Reads the jobs.json file written by JobSort v1 (the Electron version).</summary>
public static class LegacyImporter
{
    /// <summary>Where v1 stored its data: %AppData%\JobSort\jobs.json</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JobSort", "jobs.json");

    public static List<Job> Import(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new FormatException("This doesn't look like a JobSort v1 file (expected a list of jobs).");

        var jobs = new List<Job>();
        foreach (JsonElement e in doc.RootElement.EnumerateArray())
        {
            if (e.ValueKind != JsonValueKind.Object) continue;
            jobs.Add(new Job
            {
                Title = Str(e, "title"),
                Company = Str(e, "company"),
                Location = Str(e, "location"),
                Pay = Number(e, "pay"),
                PayPeriod = PayPeriod.Yearly,
                Status = ParseStatus(Str(e, "status")),
                ListedOn = Date(e, "listingDate"),
                AppliedOn = Date(e, "appliedOn"),
                InterviewOn = Date(e, "interviewDate"),
                Skills = Skills(e),
            });
        }
        return jobs;
    }

    static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "").Trim() : "";

    // v1 stored pay as either a number or a string (from the form), sometimes empty.
    static decimal? Number(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out decimal fromNumber)) return fromNumber;
        if (v.ValueKind == JsonValueKind.String &&
            decimal.TryParse(v.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal fromString))
            return fromString;
        return null;
    }

    // v1 stored dates as ISO timestamps in UTC, e.g. "2025-09-15T00:00:00.000Z".
    static DateOnly? Date(JsonElement e, string name) =>
        DateTimeOffset.TryParse(Str(e, name), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d)
            ? DateOnly.FromDateTime(d.UtcDateTime)
            : null;

    static JobStatus ParseStatus(string status) =>
        Enum.TryParse(status, ignoreCase: true, out JobStatus parsed) ? parsed : JobStatus.Interested;

    // v1 split the skills box on commas, so an empty box became [""].
    static List<string> Skills(JsonElement e)
    {
        if (!e.TryGetProperty("skills", out var v) || v.ValueKind != JsonValueKind.Array) return new();
        return v.EnumerateArray()
            .Where(s => s.ValueKind == JsonValueKind.String)
            .Select(s => (s.GetString() ?? "").Trim())
            .Where(s => s.Length > 0)
            .ToList();
    }
}
