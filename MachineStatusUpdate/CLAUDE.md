# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

ASP.NET Core MVC (.NET 8) app for tracking factory equipment status and downtime ("Sigma"). Single controller-heavy app backed by SQL Server via EF Core. No test project exists — verification is done by running the app and hitting endpoints manually / with Playwright.

## Commands

- Build: `dotnet build`
- Run (dev, no rebuild): `dotnet run --urls "http://localhost:5299" --no-build` (pick any free port; `launchSettings.json` defaults to `5041`/`7218`)
- Restore Playwright dev dependency (only used by the ad-hoc script below): `npm install`
- Manual UI smoke test: `node test-run-modal.js` — a Playwright script that drives `/Status/CreateDownTime`, exercising the Run/Stop confirmation modal. It expects the app running at `localhost:5000`; update the URL in the script if you ran on a different port.
- No automated test suite (`dotnet test`) exists in this repo — validate changes by building, running the app, and curling/loading the relevant `Status` actions.

## Architecture

### Two generations of the same idea, one currently active

The codebase contains two parallel pipelines for recording equipment state. Only the **Downtime** pipeline is wired into the nav (`Views/Shared/_Layout.cshtml` — the older links are commented out) and is the one being actively developed (see recent commits about downtime charts).

1. **Downtime pipeline (active)** — direct entry point, no barcode scanning:
   - `SVN_Downtime_Info` — one row per state change (`Run`/`Stop`) for a `SVNCode` + `Operation`, optionally tagged with an `ISS_Code` (a "reason code" for stops, joined against `SVN_Downtime_Reason` for the human-readable name).
   - `StatusController.CreateDownTime` (GET/POST) — entry form. Operation options come from `SVN_target` (a keyless view/table filtered to the current day, excluding `Sakura`/`(SM)` operations).
   - `StatusController.DowntimeList` / `DowntimeDetailReport` — history browsing with pagination + Excel export (`ExportDowntimeListToExcel`).
   - `StatusController.ReportDowntime` + a cluster of private `Prepare*ChartData` helpers — builds Chart.js-ready aggregates (by operation, by ISS code, daily trend, and "running time by project" which pairs consecutive `Stop`→`Run` rows per `SVNCode`+`Operation` to compute downtime/uptime minutes). `RunningTimeByProject` is a separate JSON endpoint (`/Status/ReportDowntime/runningTimeByProject`) used for async chart refresh by date.
   - `Services/DowntimeAutoRunService` (hosted `BackgroundService`, polls every 30s) — auto-inserts a `Run` row once a `Stop` row's `EstimateTime` (an HH:mm offset) elapses, when `AutoRunEnabled` was set on the stop. Flags `IsAutoRunExecuted` so it only fires once per stop.

2. **Machine Status Update pipeline (legacy, still routable but unlinked)** — barcode/QR-driven:
   - `SVN_Equipment_Info_History_Test` (raw scans) → `SVN_Equipment_Status_Update_Detail` (per-transition duration) → `SVN_Equipment_Status_Update` (daily rollup per machine). `Services/StatusUpdateService` does the fan-out between these three tables; `StatusController.Create` (QR scan entry) and `.ProcessAllHistoryToDetail` / `.ProcessToStatusUpdate` drive it.
   - `StatusController.DecodeQR` / `ValidateCode` support scanning a machine code (`SVN_Equipment_Machine_Info`) via ZXing.Net + SkiaSharp.
   - Don't extend this pipeline unless explicitly asked — treat the Downtime pipeline as canonical for new work.

### Controller structure

`Controllers/StatusController.cs` is a large (~2000+ line) single controller holding both pipelines plus all Excel export logic (via ClosedXML) and chart-data preparation. When editing, use the section comment banners (`/*===...===*/`) inside the file to navigate rather than reading it top to bottom. `HomeController` is just the default MVC landing/error pages and unrelated to the domain logic.

### Data access

- `Models/ApplicationDbContext.cs` is the only `DbContext`. `SVN_target` is mapped `HasNoKey()` (it's read-only reference data, not owned by this app).
- Column name mismatches are handled via `[Column("...")]` attributes (e.g. `ISS_Code` maps to DB column `ISS-Code`).
- One raw ADO.NET call exists (`StatusController.Create`) that executes a stored procedure (`SVN_InsertMachineStatus_Test`) directly via `_context.Database.GetDbConnection()` — everything else goes through EF Core LINQ.
- Connection string (including credentials) lives in `appsettings.json` / `appsettings.Development.json` pointing at a shared SQL Server instance (`10.10.99.10`, catalog `svn_pentaho`) — this is a real, shared database, not a local/disposable one. Be careful with destructive queries (see `command.sql` for examples of raw delete/reseed scripts that were used ad hoc — do not run these against the shared DB without explicit confirmation).

### Uploads & exports

- User-uploaded images (Run/Stop evidence photos) are saved under `wwwroot/uploads/status-images/` and referenced by relative path (`/uploads/status-images/...`) in the DB.
- Excel exports (ClosedXML) embed these images directly into the workbook when present; missing files degrade gracefully to a "No image" cell rather than failing the export.

### Views

Razor views live under `Views/Status/*.cshtml` and `Views/Home/*.cshtml`, sharing `Views/Shared/_Layout.cshtml`. Charts are rendered client-side (Chart.js, loaded from `wwwroot/lib`) using JSON serialized from the `Prepare*ChartData` controller helpers into `ViewBag`.
