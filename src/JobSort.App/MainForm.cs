using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using JobSort.Core;

namespace JobSort.App;

// The UI is built in code (no .Designer.cs). This attribute makes double-clicking
// the file in Visual Studio open the code instead of an empty designer.
[DesignerCategory("")]
public sealed class MainForm : Form
{
    static readonly (string Header, JobColumn Column, int Width)[] Columns =
    [
        ("Title", JobColumn.Title, 210),
        ("Company", JobColumn.Company, 160),
        ("Location", JobColumn.Location, 130),
        ("Pay", JobColumn.Pay, 110),
        ("Status", JobColumn.Status, 90),
        ("Applied", JobColumn.AppliedOn, 100),
        ("Interview", JobColumn.InterviewOn, 100),
        ("Skills", JobColumn.Skills, 220),
    ];

    readonly JobStore store = new(JobStore.DefaultPath);

    // The one and only list of jobs. Filtering and sorting produce new lists for display
    // and never modify this one, and everything is saved from here.
    readonly List<Job> allJobs;

    readonly ListView list = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        HideSelection = false,
        MultiSelect = false,
    };
    readonly ToolStripTextBox searchBox = new() { AutoSize = false };
    readonly ToolStripComboBox statusFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly ToolStripStatusLabel countLabel = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    readonly ToolStripMenuItem editItem, deleteItem;
    readonly Font boldFont;

    JobColumn sortColumn = JobColumn.Status;
    bool sortDescending;

    Job? SelectedJob => list.SelectedItems.Count > 0 ? list.SelectedItems[0].Tag as Job : null;

    public MainForm()
    {
        Text = "JobSort";
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { /* keep default icon */ }
        ClientSize = new Size(LogicalToDeviceUnits(1150), LogicalToDeviceUnits(650));
        MinimumSize = new Size(LogicalToDeviceUnits(700), LogicalToDeviceUnits(400));
        StartPosition = FormStartPosition.CenterScreen;
        boldFont = new Font(list.Font, FontStyle.Bold);

        allJobs = LoadJobs();

        // ---------- Menu ----------
        var menu = new MenuStrip();

        var fileMenu = new ToolStripMenuItem("&File");
        fileMenu.DropDownItems.AddRange(new ToolStripItem[]
        {
            new ToolStripMenuItem("&Import from JobSort v1...", null, (_, _) => ImportLegacy()),
            new ToolStripMenuItem("&Export to CSV...", null, (_, _) => ExportCsv()),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Open &Data Folder", null, (_, _) => OpenDataFolder()),
            new ToolStripSeparator(),
            new ToolStripMenuItem("E&xit", null, (_, _) => Close()),
        });

        editItem = new ToolStripMenuItem("&Edit Job...", null, (_, _) => EditSelected()) { ShortcutKeys = Keys.Control | Keys.E };
        // Delete is handled by the list's KeyDown (a real shortcut would steal Delete from the search box)
        deleteItem = new ToolStripMenuItem("&Delete Job", null, (_, _) => DeleteSelected()) { ShortcutKeyDisplayString = "Del" };
        var editMenu = new ToolStripMenuItem("&Edit");
        editMenu.DropDownItems.AddRange(new ToolStripItem[]
        {
            new ToolStripMenuItem("&Add Job...", null, (_, _) => AddJob()) { ShortcutKeys = Keys.Control | Keys.N },
            editItem,
            deleteItem,
            new ToolStripSeparator(),
            new ToolStripMenuItem("&Find", null, (_, _) => searchBox.Focus()) { ShortcutKeys = Keys.Control | Keys.F },
        });

        var helpMenu = new ToolStripMenuItem("&Help");
        helpMenu.DropDownItems.Add("&About JobSort", null, (_, _) => ShowAbout());

        menu.Items.AddRange(new ToolStripItem[] { fileMenu, editMenu, helpMenu });
        MainMenuStrip = menu;

        // ---------- Toolbar ----------
        var tools = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(6, 3, 6, 3) };
        searchBox.Width = LogicalToDeviceUnits(240);
        searchBox.TextBox.PlaceholderText = "Title, company, skill, notes...";
        statusFilter.Items.Add("All statuses");
        foreach (JobStatus s in Enum.GetValues<JobStatus>()) statusFilter.Items.Add(s.ToString());
        statusFilter.SelectedIndex = 0;
        tools.Items.AddRange(new ToolStripItem[]
        {
            new ToolStripButton("Add Job", null, (_, _) => AddJob()),
            new ToolStripSeparator(),
            new ToolStripLabel("Search:"),
            searchBox,
            new ToolStripLabel("  Status:"),
            statusFilter,
        });

        // ---------- Status bar ----------
        var status = new StatusStrip();
        status.Items.Add(countLabel);

        // ---------- Job list ----------
        foreach (var c in Columns) list.Columns.Add(c.Header, LogicalToDeviceUnits(c.Width));

        var rowMenu = new ContextMenuStrip();
        var rowEdit = new ToolStripMenuItem("&Edit...", null, (_, _) => EditSelected());
        var rowOpen = new ToolStripMenuItem("&Open Posting", null, (_, _) => OpenPosting());
        var rowStatus = new ToolStripMenuItem("Set &Status");
        foreach (JobStatus s in Enum.GetValues<JobStatus>())
            rowStatus.DropDownItems.Add(new ToolStripMenuItem(s.ToString(), null, (_, _) => SetStatus(s)) { Tag = s });
        rowMenu.Items.AddRange(new ToolStripItem[]
        {
            rowEdit, rowOpen, rowStatus, new ToolStripSeparator(),
            new ToolStripMenuItem("&Delete", null, (_, _) => DeleteSelected()),
        });
        rowMenu.Opening += (_, e) =>
        {
            if (SelectedJob is not { } job) { e.Cancel = true; return; }
            rowOpen.Enabled = job.PostingUrl.Length > 0;
            foreach (ToolStripMenuItem item in rowStatus.DropDownItems)
                item.Checked = (JobStatus)item.Tag! == job.Status;
        };
        list.ContextMenuStrip = rowMenu;

        // ---------- Layout (fill control first so it takes the space the bars leave) ----------
        Controls.Add(list);
        Controls.Add(status);
        Controls.Add(tools);
        Controls.Add(menu);

        // ---------- Events ----------
        searchBox.TextChanged += (_, _) => RefreshList();
        statusFilter.SelectedIndexChanged += (_, _) => RefreshList();
        list.DoubleClick += (_, _) => EditSelected();
        list.SelectedIndexChanged += (_, _) => UpdateCommandStates();
        list.ColumnClick += (_, e) =>
        {
            JobColumn clicked = Columns[e.Column].Column;
            if (clicked == sortColumn) sortDescending = !sortDescending;
            else
            {
                sortColumn = clicked;
                sortDescending = clicked == JobColumn.Pay; // highest pay first feels more natural
            }
            RefreshList();
        };
        list.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Delete) { DeleteSelected(); e.Handled = true; }
            else if (e.KeyCode == Keys.Enter) { EditSelected(); e.Handled = true; }
        };

        RefreshList();
    }

    // ================= Commands =================

    void AddJob()
    {
        var draft = new Job { Status = JobStatus.Applied, AppliedOn = DateOnly.FromDateTime(DateTime.Today) };
        using var dialog = new JobDialog(draft, isNew: true);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        allJobs.Add(dialog.Result);
        SaveJobs();
        RefreshList(dialog.Result.Id);

        // If the search or status filter hides the new job, clear them so it's visible.
        if (SelectedJob?.Id != dialog.Result.Id)
        {
            searchBox.Text = "";
            statusFilter.SelectedIndex = 0;
            RefreshList(dialog.Result.Id);
        }
    }

    void EditSelected()
    {
        if (SelectedJob is not { } job) return;

        // The dialog edits a copy; the real job is only replaced if the user saves.
        using var dialog = new JobDialog(job.Clone(), isNew: false);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        int index = allJobs.FindIndex(j => j.Id == job.Id);
        if (index >= 0) allJobs[index] = dialog.Result;
        SaveJobs();
        RefreshList(job.Id);
    }

    void DeleteSelected()
    {
        if (SelectedJob is not { } job) return;
        string name = job.Company.Length > 0 ? $"{job.Title} at {job.Company}" : job.Title;
        if (MessageBox.Show(this, $"Delete \"{name}\"?", "Delete Job",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
            return;

        allJobs.RemoveAll(j => j.Id == job.Id);
        SaveJobs();
        RefreshList();
    }

    void SetStatus(JobStatus status)
    {
        if (SelectedJob is not { } job) return;
        job.Status = status;
        if (status == JobStatus.Applied && job.AppliedOn is null)
            job.AppliedOn = DateOnly.FromDateTime(DateTime.Today);
        SaveJobs();
        RefreshList(job.Id);
    }

    void OpenPosting()
    {
        if (SelectedJob is not { } job || job.PostingUrl.Length == 0) return;

        string url = job.PostingUrl.Contains("://") ? job.PostingUrl : "https://" + job.PostingUrl;
        // Only ever open web links, never arbitrary files or programs.
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            MessageBox.Show(this, "The posting link isn't a valid web address.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    void ImportLegacy()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Import from JobSort v1",
            Filter = "JobSort v1 data (*.json)|*.json|All files|*.*",
        };
        string legacyFolder = Path.GetDirectoryName(LegacyImporter.DefaultPath)!;
        if (Directory.Exists(legacyFolder)) dialog.InitialDirectory = legacyFolder;
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        List<Job> imported;
        try { imported = LegacyImporter.Import(File.ReadAllText(dialog.FileName)); }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Couldn't import that file:\n{ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // Skip jobs that are already in the list, so importing twice is harmless.
        var fresh = imported.Where(n => !allJobs.Any(existing => JobQuery.IsSameListing(existing, n))).ToList();
        allJobs.AddRange(fresh);
        if (fresh.Count > 0) SaveJobs();
        RefreshList();

        int skipped = imported.Count - fresh.Count;
        string message = $"Imported {Plural(fresh.Count, "job")}.";
        if (skipped > 0) message += $"\n{Plural(skipped, "job")} skipped because they were already in your list.";
        MessageBox.Show(this, message, "Import", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    void ExportCsv()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Export to CSV",
            Filter = "CSV file (*.csv)|*.csv",
            DefaultExt = "csv",
            FileName = $"jobs-{DateTime.Today:yyyy-MM-dd}.csv",
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var sorted = JobQuery.Sort(allJobs, sortColumn, sortDescending);
            // UTF-8 with a byte-order mark so Excel reads accented characters correctly
            File.WriteAllText(dialog.FileName, CsvExporter.ToCsv(sorted), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Couldn't export:\n{ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    void OpenDataFolder()
    {
        string folder = Path.GetDirectoryName(store.FilePath)!;
        Directory.CreateDirectory(folder);
        if (File.Exists(store.FilePath))
            Process.Start("explorer.exe", $"/select,\"{store.FilePath}\"");
        else
            Process.Start("explorer.exe", $"\"{folder}\"");
    }

    void ShowAbout()
    {
        string version = Application.ProductVersion.Split('+')[0];
        MessageBox.Show(this,
            $"JobSort {version}\n\n" +
            "A job application tracker for Windows.\n" +
            "Your jobs are saved automatically after every change.\n\n" +
            "github.com/Doneux/JobSort-Windows",
            "About JobSort", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    // ================= Data =================

    List<Job> LoadJobs()
    {
        try { return store.Load(); }
        catch (Exception ex)
        {
            // Never silently overwrite a file we couldn't read: back it up first.
            string backup = $"{store.FilePath}.unreadable-{DateTime.Now:yyyyMMdd-HHmmss}";
            try { File.Copy(store.FilePath, backup); } catch { /* best effort */ }
            MessageBox.Show(
                "Your saved job list couldn't be read, so JobSort is starting with an empty list.\n\n" +
                $"The unreadable file was backed up to:\n{backup}\n\nDetails: {ex.Message}",
                "JobSort", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return new();
        }
    }

    void SaveJobs()
    {
        try { store.Save(allJobs); }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Couldn't save your changes:\n{ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // ================= Display =================

    /// <summary>Rebuilds the visible list from <see cref="allJobs"/>, keeping (or setting) the selection.</summary>
    void RefreshList(Guid? selectId = null)
    {
        selectId ??= SelectedJob?.Id;
        JobStatus? status = statusFilter.SelectedIndex > 0 ? (JobStatus)(statusFilter.SelectedIndex - 1) : null;
        List<Job> visible = JobQuery.Sort(JobQuery.Filter(allJobs, searchBox.Text, status), sortColumn, sortDescending);

        var today = DateOnly.FromDateTime(DateTime.Today);
        list.BeginUpdate();
        list.Items.Clear();
        foreach (Job job in visible)
        {
            var item = new ListViewItem(job.Title) { Tag = job, UseItemStyleForSubItems = false };
            item.SubItems.Add(job.Company);
            item.SubItems.Add(job.Location);
            item.SubItems.Add(FormatPay(job));
            item.SubItems.Add(job.Status.ToString()).ForeColor = StatusColor(job.Status);
            item.SubItems.Add(FormatDate(job.AppliedOn));
            var interview = item.SubItems.Add(FormatDate(job.InterviewOn));
            if (job.InterviewOn >= today) interview.Font = boldFont; // upcoming interviews stand out
            item.SubItems.Add(string.Join(", ", job.Skills));

            list.Items.Add(item);
            if (job.Id == selectId) { item.Selected = true; item.Focused = true; }
        }
        list.EndUpdate();
        if (list.SelectedItems.Count > 0) list.SelectedItems[0].EnsureVisible();

        // Arrow on the sorted column's header
        for (int i = 0; i < Columns.Length; i++)
            list.Columns[i].Text = Columns[i].Header + (Columns[i].Column == sortColumn ? (sortDescending ? "  ▼" : "  ▲") : "");

        UpdateStatusBar(visible.Count);
        UpdateCommandStates();
    }

    void UpdateStatusBar(int visibleCount)
    {
        if (allJobs.Count == 0)
        {
            countLabel.Text = "No jobs yet. Press Ctrl+N to add one, or use File → Import to bring in your JobSort v1 list.";
            return;
        }
        int active = allJobs.Count(j => j.Status is JobStatus.Applied or JobStatus.Interview or JobStatus.Offer);
        int interviewing = allJobs.Count(j => j.Status == JobStatus.Interview);
        string shown = visibleCount == allJobs.Count
            ? Plural(allJobs.Count, "job")
            : $"Showing {visibleCount} of {Plural(allJobs.Count, "job")}";
        countLabel.Text = $"{shown}     ·     {active} active     ·     {interviewing} interviewing";
    }

    void UpdateCommandStates()
    {
        bool hasSelection = SelectedJob is not null;
        editItem.Enabled = hasSelection;
        deleteItem.Enabled = hasSelection;
    }

    static string FormatPay(Job job) => job.Pay is not decimal pay ? ""
        : job.PayPeriod == PayPeriod.Hourly ? $"{pay:C2}/hr" : $"{pay:C0}/yr";

    static string FormatDate(DateOnly? date) => date?.ToString("MMM d, yyyy") ?? "";

    static Color StatusColor(JobStatus status) => status switch
    {
        JobStatus.Interested => Color.SlateGray,
        JobStatus.Interview => Color.DodgerBlue,
        JobStatus.Offer => Color.DarkOrange,
        JobStatus.Accepted => Color.MediumSeaGreen,
        JobStatus.Rejected => Color.IndianRed,
        JobStatus.Withdrawn => Color.Gray,
        _ => SystemColors.WindowText,
    };

    static string Plural(int count, string word) => $"{count} {word}{(count == 1 ? "" : "s")}";

    protected override void Dispose(bool disposing)
    {
        if (disposing) boldFont.Dispose();
        base.Dispose(disposing);
    }
}
