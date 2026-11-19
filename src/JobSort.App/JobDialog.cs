using System.ComponentModel;
using System.Globalization;
using JobSort.Core;

namespace JobSort.App;

/// <summary>
/// Add/Edit dialog. It works on the Job it's given (the caller passes a copy when editing),
/// so pressing Cancel can never change or lose the original.
/// </summary>
[DesignerCategory("")]
public sealed class JobDialog : Form
{
    readonly Job job;

    readonly TextBox title = new();
    readonly TextBox company = new();
    readonly TextBox location = new();
    readonly TextBox pay = new() { PlaceholderText = "e.g. 85000 or 32.50" };
    readonly ComboBox payPeriod = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly ComboBox status = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    readonly TextBox postingUrl = new() { PlaceholderText = "https://..." };
    readonly DateTimePicker listedOn = DatePicker();
    readonly DateTimePicker appliedOn = DatePicker();
    readonly DateTimePicker interviewOn = DatePicker();
    readonly TextBox skills = new() { PlaceholderText = "Comma-separated, e.g. C#, SQL, Git" };
    readonly TextBox notes = new() { Multiline = true, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical };

    public Job Result => job;

    public JobDialog(Job job, bool isNew)
    {
        this.job = job;

        Text = isNew ? "Add Job" : "Edit Job";
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        ShowIcon = false;
        ClientSize = new Size(LogicalToDeviceUnits(540), LogicalToDeviceUnits(600));
        MinimumSize = new Size(LogicalToDeviceUnits(420), LogicalToDeviceUnits(480));

        foreach (PayPeriod p in Enum.GetValues<PayPeriod>()) payPeriod.Items.Add(p);
        foreach (JobStatus s in Enum.GetValues<JobStatus>()) status.Items.Add(s);

        // ---------- Form fields: labels on the left, inputs on the right ----------
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12, 12, 12, 0) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var payRow = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = true, Margin = Padding.Empty };
        payRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        payRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pay.Dock = DockStyle.Fill;
        payRow.Controls.Add(pay, 0, 0);
        payRow.Controls.Add(payPeriod, 1, 0);

        AddRow(grid, "&Title *", title);
        AddRow(grid, "&Company", company);
        AddRow(grid, "&Location", location);
        AddRow(grid, "&Pay", payRow);
        AddRow(grid, "&Status", status, stretch: false);
        AddRow(grid, "Posting li&nk", postingUrl);
        AddRow(grid, "Listed &on", listedOn, stretch: false);
        AddRow(grid, "&Applied on", appliedOn, stretch: false);
        AddRow(grid, "&Interview on", interviewOn, stretch: false);
        AddRow(grid, "S&kills", skills);
        AddRow(grid, "N&otes", notes, fillHeight: true);

        // ---------- Buttons ----------
        var saveButton = new Button { Text = isNew ? "Add" : "Save", AutoSize = true, MinimumSize = new Size(LogicalToDeviceUnits(88), 0) };
        var cancelButton = new Button { Text = "Cancel", AutoSize = true, MinimumSize = new Size(LogicalToDeviceUnits(88), 0), DialogResult = DialogResult.Cancel };
        saveButton.Click += (_, _) => TrySave();
        AcceptButton = saveButton;   // Enter
        CancelButton = cancelButton; // Esc

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(12, 8, 12, 12),
        };
        buttons.Controls.Add(cancelButton);
        buttons.Controls.Add(saveButton);

        Controls.Add(grid);
        Controls.Add(buttons);

        LoadFields();
    }

    void LoadFields()
    {
        title.Text = job.Title;
        company.Text = job.Company;
        location.Text = job.Location;
        pay.Text = job.Pay?.ToString("#,0.##", CultureInfo.CurrentCulture) ?? "";
        payPeriod.SelectedItem = job.PayPeriod;
        status.SelectedItem = job.Status;
        postingUrl.Text = job.PostingUrl;
        SetDate(listedOn, job.ListedOn);
        SetDate(appliedOn, job.AppliedOn);
        SetDate(interviewOn, job.InterviewOn);
        skills.Text = string.Join(", ", job.Skills);
        notes.Text = job.Notes;
    }

    void TrySave()
    {
        if (string.IsNullOrWhiteSpace(title.Text))
        {
            Warn("Please enter a job title.", title);
            return;
        }

        decimal? payValue = null;
        if (pay.Text.Trim().Length > 0)
        {
            // Currency style accepts "$85,000", "85000" and "32.50"
            if (!decimal.TryParse(pay.Text.Trim(), NumberStyles.Currency, CultureInfo.CurrentCulture, out decimal parsed) || parsed < 0)
            {
                Warn("Pay should be a number, like 85000 or 32.50.", pay);
                return;
            }
            payValue = parsed;
        }

        job.Title = title.Text.Trim();
        job.Company = company.Text.Trim();
        job.Location = location.Text.Trim();
        job.Pay = payValue;
        job.PayPeriod = (PayPeriod)payPeriod.SelectedItem!;
        job.Status = (JobStatus)status.SelectedItem!;
        job.PostingUrl = postingUrl.Text.Trim();
        job.ListedOn = GetDate(listedOn);
        job.AppliedOn = GetDate(appliedOn);
        job.InterviewOn = GetDate(interviewOn);
        job.Skills = skills.Text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        job.Notes = notes.Text.Trim();

        DialogResult = DialogResult.OK;
    }

    void Warn(string message, Control field)
    {
        MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        field.Focus();
    }

    void AddRow(TableLayoutPanel grid, string label, Control input, bool stretch = true, bool fillHeight = false)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(fillHeight ? new RowStyle(SizeType.Percent, 100) : new RowStyle(SizeType.AutoSize));

        grid.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = fillHeight ? AnchorStyles.Top | AnchorStyles.Left : AnchorStyles.Left,
            Margin = new Padding(0, fillHeight ? 6 : 3, LogicalToDeviceUnits(10), 3),
        }, 0, row);

        if (stretch) input.Dock = DockStyle.Fill;
        else input.Anchor = AnchorStyles.Left;
        input.Margin = new Padding(0, 3, 0, 3);
        grid.Controls.Add(input, 1, row);
    }

    // The checkbox inside the picker means "has a date"; unchecked = no date.
    static DateTimePicker DatePicker() => new()
    {
        Format = DateTimePickerFormat.Short,
        ShowCheckBox = true,
        Checked = false,
    };

    static void SetDate(DateTimePicker picker, DateOnly? date)
    {
        picker.Value = date?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today;
        picker.Checked = date is not null; // set after Value, since setting Value ticks the box
    }

    static DateOnly? GetDate(DateTimePicker picker) =>
        picker.Checked ? DateOnly.FromDateTime(picker.Value) : null;
}
