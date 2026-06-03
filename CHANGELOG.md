# Changelog

## 1.1.0 (2026-06-03)

Full API v2 surface parity, bug fixes, and a dedicated v2 NuGet package id.

### Packaging
- **Now published as `IDAnalyzer.V2`** on NuGet. The legacy `IDAnalyzer` package id
  remains the **API v1** SDK — this avoids silently overwriting v1 consumers.
- Bumped `Newtonsoft.Json` 12.0.3 → 13.0.3; removed the build-fragile `docfx.console`
  dependency and the missing `appicon.png` package-icon reference.
- Fixed `RepositoryUrl` (pointed at the v1 repo) and stale release notes/copyright.

### Fixed
- **Base URL** default `v2-us1.idanalyzer.com` (single node, no Cloudflare/LB/HA) →
  **`api2.idanalyzer.com`**. EU unchanged (`api2-eu` via `IDANALYZER_REGION=eu`).
- Unknown `IDANALYZER_REGION` now throws `InvalidArgumentException` instead of
  silently falling back to the US node.
- Fixed `Transaction.updateTransaction` decision validation (`decision == "reject"`
  → `!= "reject"`; valid `"reject"` was rejected and the guard never fired as intended).
- Fixed `Profile.webhook` protocol guard (`!= "http" || != "https"` was always true →
  every URL threw).
- Fixed the `verifyUserInformation` date format string (`yyyy/mm/dd` used minutes for
  the month component → `yyyy/MM/dd`, invariant culture).

### Added
- `Scanner.veryQuickScan` → `POST /veryquickscan`.
- `AML` class — `search` (`POST /aml`, incl. optional `birthYear`) and `searchV3`
  (`POST /amlv3`).
- `Docupass.getDocupass` → `GET /docupass/{reference}`.
- `ProfileAPI` class — server-side KYC profile CRUD + export.
- `Webhook` class — `listWebhook`/`resendWebhook`/`deleteWebhook`.
- `Account` class — `getAccount` (`GET /myaccount`).
