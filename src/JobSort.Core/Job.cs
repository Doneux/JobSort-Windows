using System.Text.Json.Serialization;

namespace JobSort.Core;

/// <summary>Where an application is in the hiring pipeline, in pipeline order.</summary>
public enum JobStatus
{
    Interested,
    Applied,
    Interview,
    Offer,
    Accepted,
    Rejected,
    Withdrawn,
}

public enum PayPeriod
{
    Yearly,
    Hourly,
}

public sealed class Job
{
    /// <summary>Used to turn hourly pay into a yearly figure (40 hours x 52 weeks).</summary>
    public const int HoursPerYear = 2080;

    /// <summary>
    /// Stable identity. Jobs are matched by Id, never by their position in a list,
    /// so sorting and filtering can't cause the wrong job to be edited or deleted.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Title { get; set; } = "";
    public string Company { get; set; } = "";
    public string Location { get; set; } = "";
    public decimal? Pay { get; set; }
    public PayPeriod PayPeriod { get; set; } = PayPeriod.Yearly;
    public string PostingUrl { get; set; } = "";
    public JobStatus Status { get; set; } = JobStatus.Interested;
    public DateOnly? ListedOn { get; set; }
    public DateOnly? AppliedOn { get; set; }
    public DateOnly? InterviewOn { get; set; }
    public List<string> Skills { get; set; } = new();
    public string Notes { get; set; } = "";

    /// <summary>Pay as a yearly amount, so hourly and salaried jobs can be compared.</summary>
    [JsonIgnore]
    public decimal? YearlyPay => PayPeriod == PayPeriod.Hourly ? Pay * HoursPerYear : Pay;

    /// <summary>A deep copy, so an edit dialog can change it freely and Cancel leaves the original untouched.</summary>
    public Job Clone() => new()
    {
        Id = Id,
        Title = Title,
        Company = Company,
        Location = Location,
        Pay = Pay,
        PayPeriod = PayPeriod,
        PostingUrl = PostingUrl,
        Status = Status,
        ListedOn = ListedOn,
        AppliedOn = AppliedOn,
        InterviewOn = InterviewOn,
        Skills = new List<string>(Skills),
        Notes = Notes,
    };
}
