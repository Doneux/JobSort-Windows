using System.Text.Json;
using System.Text.Json.Serialization;

namespace JobSort.Core;

/// <summary>Loads and saves the job list as a JSON file.</summary>
public sealed class JobStore
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }, // "Applied" instead of 1 in the file
    };

    /// <summary>%AppData%\JobSort-Windows\jobs.json</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JobSort-Windows", "jobs.json");

    public string FilePath { get; }

    public JobStore(string filePath) => FilePath = filePath;

    public List<Job> Load()
    {
        if (!File.Exists(FilePath)) return new();
        string json = File.ReadAllText(FilePath);
        if (string.IsNullOrWhiteSpace(json)) return new();
        return JsonSerializer.Deserialize<List<Job>>(json, JsonOptions) ?? new();
    }

    /// <summary>
    /// Writes to a temporary file and then swaps it in, so a crash or power loss
    /// mid-save can never leave a half-written (corrupt) job list.
    /// </summary>
    public void Save(IEnumerable<Job> jobs)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(jobs, JsonOptions));
        File.Move(temp, FilePath, overwrite: true);
    }
}
