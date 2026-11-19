using JobSort.Core;

namespace JobSort.Tests;

public class JobStoreTests : IDisposable
{
    readonly string dir = Path.Combine(Path.GetTempPath(), "JobSortTests-" + Guid.NewGuid());
    string FilePath => Path.Combine(dir, "jobs.json");

    public void Dispose()
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void Load_MissingFile_ReturnsEmptyList()
    {
        Assert.Empty(new JobStore(FilePath).Load());
    }

    [Fact]
    public void SaveThenLoad_RoundTripsEveryField()
    {
        var job = new Job
        {
            Title = "Tools Programmer",
            Company = "Riot Games",
            Location = "Los Angeles, CA",
            Pay = 32.50m,
            PayPeriod = PayPeriod.Hourly,
            PostingUrl = "https://example.com/job/1",
            Status = JobStatus.Offer,
            ListedOn = new DateOnly(2026, 11, 1),
            AppliedOn = new DateOnly(2026, 11, 3),
            InterviewOn = new DateOnly(2026, 11, 20),
            Skills = ["C#", "WinForms"],
            Notes = "Line one\r\nLine two, with \"quotes\"",
        };

        var store = new JobStore(FilePath);
        store.Save([job]);
        var loaded = Assert.Single(store.Load());

        Assert.Equal(job.Id, loaded.Id);
        Assert.Equal(job.Title, loaded.Title);
        Assert.Equal(job.Company, loaded.Company);
        Assert.Equal(job.Location, loaded.Location);
        Assert.Equal(job.Pay, loaded.Pay);
        Assert.Equal(job.PayPeriod, loaded.PayPeriod);
        Assert.Equal(job.PostingUrl, loaded.PostingUrl);
        Assert.Equal(job.Status, loaded.Status);
        Assert.Equal(job.ListedOn, loaded.ListedOn);
        Assert.Equal(job.AppliedOn, loaded.AppliedOn);
        Assert.Equal(job.InterviewOn, loaded.InterviewOn);
        Assert.Equal(job.Skills, loaded.Skills);
        Assert.Equal(job.Notes, loaded.Notes);
    }

    [Fact]
    public void Save_WritesStatusAsReadableText()
    {
        new JobStore(FilePath).Save([new Job { Status = JobStatus.Interview }]);
        Assert.Contains("\"Interview\"", File.ReadAllText(FilePath));
    }

    [Fact]
    public void Save_LeavesNoTempFileBehind()
    {
        new JobStore(FilePath).Save([new Job()]);
        Assert.False(File.Exists(FilePath + ".tmp"));
    }
}

public class LegacyImporterTests
{
    // Same shape as the files JobSort v1 (Electron) wrote, including its quirks:
    // pay as a number or a string, empty skill entries, ISO dates in UTC.
    const string V1Json = """
        [
          {
            "title": "Job A", "company": "Company A", "location": "Somewhere", "pay": 1000,
            "listingDate": null, "skills": ["skill1", "skill2"], "status": "applied",
            "interviewDate": null, "appliedOn": "2025-09-15T00:00:00.000Z"
          },
          {
            "title": "Job B", "company": "Company B", "location": "Somewhere else", "pay": "2000",
            "listingDate": "2025-09-01T00:00:00.000Z", "skills": [""], "status": "accepted",
            "interviewDate": null, "appliedOn": null
          },
          { "title": "Job C", "pay": "", "status": "", "skills": [] }
        ]
        """;

    [Fact]
    public void Import_ReadsV1Files()
    {
        var jobs = LegacyImporter.Import(V1Json);
        Assert.Equal(3, jobs.Count);

        Assert.Equal("Job A", jobs[0].Title);
        Assert.Equal(1000m, jobs[0].Pay);
        Assert.Equal(JobStatus.Applied, jobs[0].Status);
        Assert.Equal(new DateOnly(2025, 9, 15), jobs[0].AppliedOn);
        Assert.Equal(new[] { "skill1", "skill2" }, jobs[0].Skills);

        Assert.Equal(2000m, jobs[1].Pay);
        Assert.Equal(JobStatus.Accepted, jobs[1].Status);
        Assert.Equal(new DateOnly(2025, 9, 1), jobs[1].ListedOn);
        Assert.Empty(jobs[1].Skills);

        Assert.Null(jobs[2].Pay);
        Assert.Equal(JobStatus.Interested, jobs[2].Status);
        Assert.Equal("", jobs[2].Company);
    }

    [Fact]
    public void Import_GivesEachJobAUniqueId()
    {
        var jobs = LegacyImporter.Import(V1Json);
        Assert.Equal(jobs.Count, jobs.Select(j => j.Id).Distinct().Count());
    }

    [Fact]
    public void Import_RejectsNonListJson()
    {
        Assert.Throws<FormatException>(() => LegacyImporter.Import("""{ "title": "not a list" }"""));
    }
}

public class CsvExporterTests
{
    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("San Diego, CA", "\"San Diego, CA\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("line1\nline2", "\"line1\nline2\"")]
    [InlineData("=SUM(A1)", "'=SUM(A1)")]
    public void Escape_HandlesSpecialCharacters(string input, string expected)
    {
        Assert.Equal(expected, CsvExporter.Escape(input));
    }

    [Fact]
    public void ToCsv_WritesHeaderAndOneRowPerJob()
    {
        string csv = CsvExporter.ToCsv([
            new Job { Title = "A", Pay = 50000, Skills = ["C#", "SQL"], AppliedOn = new DateOnly(2026, 1, 2) },
            new Job { Title = "B" },
        ]);
        string[] lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(3, lines.Length);
        Assert.Equal(CsvExporter.Header, lines[0]);
        Assert.StartsWith("A,,,50000,Yearly,Interested,,2026-01-02,,C#; SQL,,", lines[1]);
    }
}
