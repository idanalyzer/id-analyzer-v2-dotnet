# ID Analyzer .NET SDK — Identity Verification, KYC, Document & Biometric API

[![NuGet version](https://img.shields.io/nuget/v/IDAnalyzerV2.svg)](https://www.nuget.org/packages/IDAnalyzerV2)
[![NuGet downloads](https://img.shields.io/nuget/dt/IDAnalyzerV2.svg)](https://www.nuget.org/packages/IDAnalyzerV2)
[![license](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Official .NET / C# client library for the **[ID Analyzer](https://www.idanalyzer.com) API v2** — automate identity document verification, KYC onboarding and biometric checks in minutes.

Scan and authenticate **passports, driver's licenses, ID cards, visas and residence permits from 190+ countries**, run **1:1 face match and liveness detection**, screen against **AML / PEP / sanctions** watchlists, and onboard users remotely with **DocuPass** hosted verification & e-signature.

- 🌐 **Website:** [www.idanalyzer.com](https://www.idanalyzer.com)
- 📚 **Developer docs & API reference:** [developer.idanalyzer.com](https://developer.idanalyzer.com/help)
- 🔑 **Get your API key:** [portal2.idanalyzer.com](https://portal2.idanalyzer.com)
- 💬 **Support:** support@idanalyzer.com

## Features

- **Document OCR & authentication** — passport, driver's license, ID card, visa & residence-permit recognition from 190+ countries, including MRZ and PDF417 / AAMVA barcode parsing.
- **Biometric verification** — 1:1 face match and liveness / presentation-attack detection.
- **AML screening** — PEP, sanctions, watchlist and adverse-media checks.
- **DocuPass** — hosted, no-code remote identity verification, KYC/AML onboarding and legally-binding e-signature.
- **KYC profiles, transaction vault, contract generation and webhooks.**
- **US & EU data-residency regions.**

> ⚠️ Never embed your API key in client-side apps (mobile, browser JS). Call the API from your server.

## Installation

The v2 SDK ships as **`IDAnalyzerV2`** (the legacy `IDAnalyzer` package id remains the API v1 SDK):

```bash
dotnet add package IDAnalyzerV2
```

Targets .NET Standard 2.1 (works with .NET Core 3.x, .NET 5/6/8+).

## Authentication & region

Pass your API key to each client, or set the `IDANALYZER_KEY` environment variable. The SDK targets the US endpoint (`https://api2.idanalyzer.com`) by default; set `IDANALYZER_REGION=eu` for the EU endpoint (`https://api2-eu.idanalyzer.com`). An unrecognized region throws `InvalidArgumentException`.

## Quick start

```csharp
using IDAnalyzer;

var scanner = new Scanner("YOUR_API_KEY");
scanner.throwApiException(true);
scanner.setProfile(new Profile(Profile.SECURITY_MEDIUM));

// Scan a document + selfie for biometric verification
var result = scanner.scan("id_front.jpg", "", "selfie.jpg");
Console.WriteLine(result["decision"]);   // accept / review / reject
```

## Examples

```csharp
using IDAnalyzer;

// AML / PEP / sanctions screening
var aml = new AML("YOUR_API_KEY");
aml.search("John Smith", "", 0, "US");        // POST /aml
aml.searchV3("John Smith", "", 10, 1);        // POST /amlv3

// DocuPass — hosted remote verification link
var docupass = new Docupass("YOUR_API_KEY");
var link = docupass.createDocupass("YOUR_PROFILE_ID");
Console.WriteLine(link["url"]);
```

## API coverage

The SDK wraps the complete ID Analyzer API v2 surface:

| Class | Methods |
|---|---|
| `Scanner` | `scan`, `quickScan`, `veryQuickScan` |
| `Biometric` | `verifyFace`, `verifyLiveness` |
| `AML` | `search` (`/aml`), `searchV3` (`/amlv3`) |
| `Contract` | `generate` + template CRUD |
| `Transaction` | `getTransaction`, `listTransaction`, `updateTransaction`, `deleteTransaction`, `exportTransaction`, `saveImage`, `saveFile` |
| `Docupass` | `createDocupass`, `listDocupass`, `getDocupass`, `deleteDocupass` |
| `ProfileAPI` | KYC profile create / list / get / update / delete / export |
| `Webhook` | `listWebhook`, `resendWebhook`, `deleteWebhook` |
| `Account` | `getAccount` |
| `Profile` | client-side KYC profile-override builder |

## Resources

- [ID Analyzer website](https://www.idanalyzer.com)
- [Developer documentation & API reference](https://developer.idanalyzer.com/help)
- [.NET SDK guide](https://developer.idanalyzer.com/help/net)
- [Dashboard — get your API key](https://portal2.idanalyzer.com)

## Other ID Analyzer SDKs

[PHP](https://github.com/idanalyzer/id-analyzer-v2-php) · [Python](https://github.com/idanalyzer/id-analyzer-v2-python) · [Node.js](https://github.com/idanalyzer/id-analyzer-v2-nodejs) · [.NET](https://github.com/idanalyzer/id-analyzer-v2-dotnet) · [Java](https://github.com/idanalyzer/id-analyzer-v2-java) · [Go](https://github.com/idanalyzer/id-analyzer-v2-go)

## License

MIT © [ID Analyzer](https://www.idanalyzer.com) — see [LICENSE](LICENSE).
