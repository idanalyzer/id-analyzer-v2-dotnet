# ID Analyzer .NET SDK (API v2)

.NET client library for the [ID Analyzer API v2](https://www.idanalyzer.com) —
worldwide passport, driver license and ID card scanning, biometric face/liveness
verification, AML/PEP screening, DocuPass remote verification & e-signature, KYC
profile management and contract generation.

> This is the **API v2** SDK, published as **`IDAnalyzer.V2`**. The legacy
> `IDAnalyzer` package id remains the API v1 SDK.

## Installation
```shell
dotnet add package IDAnalyzer.V2
```

## Base URL / Region
Defaults to the US endpoint (`https://api2.idanalyzer.com`). Set the environment
variable `IDANALYZER_REGION=eu` to use the EU endpoint (`https://api2-eu.idanalyzer.com`).
An unrecognized value throws `InvalidArgumentException`.

## Quick start
```c#
using IDAnalyzer;

var p = new Profile(Profile.SECURITY_MEDIUM);
var s = new Scanner("YOUR_API_KEY");
s.throwApiException(true);
s.setProfile(p);
var result = s.scan("id_front.jpg");
Console.WriteLine(result);
```

## Coverage
`Scanner` (scan / quickScan / veryQuickScan), `Biometric` (verifyFace / verifyLiveness),
`AML` (search / searchV3), `Contract` (generate + template CRUD), `Transaction`
(get / list / update / delete / export / saveImage / saveFile), `Docupass`
(create / list / get / delete), `ProfileAPI` (KYC profile CRUD + export), `Webhook`
(list / resend / delete), `Account` (getAccount).

Full examples and reference: https://github.com/idanalyzer/id-analyzer-v2-dotnet
