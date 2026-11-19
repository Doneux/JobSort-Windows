using JobSort.Core;

namespace JobSort.Tests;

public class JobQueryTests
{
    static readonly List<Job> Jobs =
    [
        new() { Title = "Tools Programmer", Company = "Riot Games", Status = JobStatus.Applied, Pay = 120000, Skills = ["C++", "Python"] },
        new() { Title = "Software Engineer", Company = "General Atomics", Status = JobStatus.Interview, Pay = 55, PayPeriod = PayPeriod.Hourly, Skills = ["C#"] },
        new() { Title = "Game Designer", Company = "Indie Studio", Status = JobStatus.Rejected, Notes = "Asked about Roblox experience" },
    ];

    [Fact]
    public void Filter_SearchIsCaseInsensitive_AndCoversSkillsAndNotes()
    {
        Assert.Single(JobQuery.Filter(Jobs, "riot", null));
        Assert.Single(JobQuery.Filter(Jobs, "c#", null));
        Assert.Single(JobQuery.Filter(Jobs, "ROBLOX", null));
    }

    [Fact]
    public void Filter_EveryWordMustMatch()
    {
        Assert.Single(JobQuery.Filter(Jobs, "engineer atomics", null));
        Assert.Empty(JobQuery.Filter(Jobs, "engineer riot", null));
    }

    [Fact]
    public void Filter_ByStatus()
    {
        var result = JobQuery.Filter(Jobs, "", JobStatus.Interview).ToList();
        Assert.Single(result);
        Assert.Equal("General Atomics", result[0].Company);
    }

    [Fact]
    public void Filter_EmptySearch_ReturnsEverything()
    {
        Assert.Equal(3, JobQuery.Filter(Jobs, "  ", null).Count());
    }

    // Regression test for the v1 bug where deleting while a filter was active
    // saved only the filtered jobs, wiping the rest.
    [Fact]
    public void FilterAndSort_NeverModifyTheSourceList()
    {
        var source = Jobs.ToList();
        _ = JobQuery.Filter(source, "riot", JobStatus.Applied).ToList();
        _ = JobQuery.Sort(source, JobColumn.Pay, descending: true);
        Assert.Equal(Jobs, source);
    }

    [Fact]
    public void Sort_Pay_ComparesHourlyAsYearly()
    {
        // $55/hr = $114,400/yr, which is less than $120,000/yr
        var sorted = JobQuery.Sort(Jobs, JobColumn.Pay, descending: true);
        Assert.Equal("Riot Games", sorted[0].Company);
        Assert.Equal("General Atomics", sorted[1].Company);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Sort_EmptyValuesGoLast_InBothDirections(bool descending)
    {
        var sorted = JobQuery.Sort(Jobs, JobColumn.Pay, descending);
        Assert.Null(sorted[^1].Pay);
    }

    [Fact]
    public void Sort_Status_FollowsPipelineOrder()
    {
        var sorted = JobQuery.Sort(Jobs, JobColumn.Status, descending: false);
        Assert.Equal(new[] { JobStatus.Applied, JobStatus.Interview, JobStatus.Rejected }, sorted.Select(j => j.Status));
    }

    [Fact]
    public void Sort_IsStable()
    {
        var same = Enumerable.Range(0, 20).Select(i => new Job { Title = "Same", Notes = i.ToString() }).ToList();
        var sorted = JobQuery.Sort(same, JobColumn.Title, descending: false);
        Assert.Equal(same.Select(j => j.Notes), sorted.Select(j => j.Notes));
    }

    [Fact]
    public void IsSameListing_IgnoresCaseAndWhitespace()
    {
        var a = new Job { Title = "Game Designer", Company = "Indie Studio" };
        var b = new Job { Title = " game designer ", Company = "INDIE STUDIO" };
        Assert.True(JobQuery.IsSameListing(a, b));

        b.AppliedOn = new DateOnly(2026, 1, 5);
        Assert.False(JobQuery.IsSameListing(a, b));
    }

    [Fact]
    public void Clone_IsIndependentOfOriginal()
    {
        var original = new Job { Title = "A", Skills = ["x"] };
        var copy = original.Clone();
        copy.Title = "B";
        copy.Skills.Add("y");

        Assert.Equal("A", original.Title);
        Assert.Single(original.Skills);
        Assert.Equal(original.Id, copy.Id);
    }
}
