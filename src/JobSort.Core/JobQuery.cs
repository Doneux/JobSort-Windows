namespace JobSort.Core;

public enum JobColumn
{
    Title,
    Company,
    Location,
    Pay,
    Status,
    AppliedOn,
    InterviewOn,
    Skills,
}

/// <summary>
/// Filtering and sorting. Every method returns a new sequence and never modifies
/// the list it was given, so the full job list can't be lost by viewing a subset of it.
/// </summary>
public static class JobQuery
{
    /// <summary>
    /// Keeps jobs matching every word of <paramref name="search"/> (in title, company, location,
    /// skills or notes, case-insensitive) and, if given, the status.
    /// </summary>
    public static IEnumerable<Job> Filter(IEnumerable<Job> jobs, string? search, JobStatus? status)
    {
        string[] terms = (search ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return jobs.Where(j => (status is null || j.Status == status) && terms.All(t => Matches(j, t)));
    }

    /// <summary>
    /// Sorts by a column. Empty values (no pay, no date) always go last, in either direction.
    /// The sort is stable: jobs that tie keep their existing order.
    /// </summary>
    public static List<Job> Sort(IEnumerable<Job> jobs, JobColumn column, bool descending)
    {
        int sign = descending ? -1 : 1;
        var comparer = Comparer<Job>.Create((a, b) => Compare(a, b, column, sign));
        return jobs.OrderBy(j => j, comparer).ToList();
    }

    /// <summary>
    /// True if two jobs look like the same application (same title, company and applied date).
    /// Used so importing the same file twice doesn't create duplicates.
    /// </summary>
    public static bool IsSameListing(Job a, Job b) =>
        string.Equals(a.Title.Trim(), b.Title.Trim(), StringComparison.OrdinalIgnoreCase) &&
        string.Equals(a.Company.Trim(), b.Company.Trim(), StringComparison.OrdinalIgnoreCase) &&
        a.AppliedOn == b.AppliedOn;

    static bool Matches(Job job, string term) =>
        Has(job.Title, term) || Has(job.Company, term) || Has(job.Location, term) ||
        Has(job.Notes, term) || job.Skills.Any(s => Has(s, term));

    static bool Has(string text, string term) => text.Contains(term, StringComparison.OrdinalIgnoreCase);

    static int Compare(Job a, Job b, JobColumn column, int sign) => column switch
    {
        JobColumn.Title => sign * Text(a.Title, b.Title),
        JobColumn.Company => sign * Text(a.Company, b.Company),
        JobColumn.Location => sign * Text(a.Location, b.Location),
        JobColumn.Pay => NullsLast(a.YearlyPay, b.YearlyPay, sign),
        JobColumn.Status => sign * a.Status.CompareTo(b.Status),
        JobColumn.AppliedOn => NullsLast(a.AppliedOn, b.AppliedOn, sign),
        JobColumn.InterviewOn => NullsLast(a.InterviewOn, b.InterviewOn, sign),
        JobColumn.Skills => sign * Text(string.Join(", ", a.Skills), string.Join(", ", b.Skills)),
        _ => 0,
    };

    static int Text(string a, string b) => string.Compare(a, b, StringComparison.CurrentCultureIgnoreCase);

    static int NullsLast<T>(T? a, T? b, int sign) where T : struct, IComparable<T>
    {
        if (a is null) return b is null ? 0 : 1;
        if (b is null) return -1;
        return sign * a.Value.CompareTo(b.Value);
    }
}
