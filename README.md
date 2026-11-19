# JobSort for Windows

[![Build and test](https://github.com/Doneux/JobSort-Windows/actions/workflows/build.yml/badge.svg)](https://github.com/Doneux/JobSort-Windows/actions/workflows/build.yml)

A native Windows app for tracking job applications: where you've applied, what stage each one is at, and what's coming up.

![Screenshot](docs/screenshot.png)

## Features

- **One table for everything:** click any column header to sort; pay sorts fairly across hourly and salaried jobs
- **Instant search** across title, company, location, skills and notes, plus a status filter
- **Pipeline statuses:** Interested → Applied → Interview → Offer → Accepted / Rejected / Withdrawn, color-coded, with a right-click menu to change them
- **Upcoming interviews** shown in bold
- **Auto-save** after every change, written safely (temp file + swap) so a crash can't corrupt your data
- **Import** from JobSort v1, skipping anything already in your list
- **Export to CSV** for Excel or Google Sheets
- Keyboard-friendly (Ctrl+N add, Enter edit, Del delete, Ctrl+F search) and follows the Windows light/dark theme

Data lives in `%AppData%\JobSort-Windows\jobs.json` (**File → Open Data Folder**).

## Project structure

```
src/
  JobSort.Core/    Data model, search/sort, storage, CSV export, v1 import (no UI code)
  JobSort.App/     WinForms user interface
tests/
  JobSort.Tests/   xUnit tests for everything in JobSort.Core
```

Keeping the logic in a separate library with no UI dependencies means it can be tested on its own, and the tests run automatically on every push via GitHub Actions.

## Building

Requires Windows and the [.NET 10 SDK](https://dotnet.microsoft.com/download) (included with Visual Studio 2026's ".NET desktop development" workload).

```
dotnet build JobSort.slnx
dotnet test JobSort.slnx
dotnet run --project src/JobSort.App
```

Or open `JobSort.slnx` in Visual Studio and press F5.

## History

Version 1 was built in 2025 with Electron and React: [JobSort-Electron](https://github.com/Doneux/JobSort-Electron). Version 2 is a ground-up native rewrite. The biggest change is architectural: v1 kept the full job list and the filtered view in two separate arrays, which let edits made while filtering overwrite hidden data. v2 keeps a single source of truth, and its filtering and sorting can only *read* it (covered by a regression test).

## License

[MIT](LICENSE)
