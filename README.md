
# ID Analyzer .NET SDK
This is a .Net SDK for [ID Analyzer Identity Verification APIs](https://www.idanalyzer.com), though all the APIs can be called with without the SDK using simple HTTP requests as outlined in the [documentation](https://developer.idanalyzer.com), you can use this SDK to accelerate server-side development.

We strongly discourage users to connect to ID Analyzer API endpoint directly  from client-side applications that will be distributed to end user, such as mobile app, or in-browser JavaScript. Your API key could be easily compromised, and if you are storing your customer's information inside Vault they could use your API key to fetch all your user details. Therefore, the best practice is always to implement a client side connection to your server, and call our APIs from the server-side.

## Installation (Unlisted Packages)
Install through nuget CLI

```shell

```
Install through Visual Studio Package Manager Console

```shell

```

Alternatively, download this package and add the project under`/IDAnalyzer` to your solution

## Scanner
This category supports all scanning-related functions specifically used to initiate a new identity document scan & ID face verification transaction by uploading based64-encoded images.
![Sample ID](https://www.idanalyzer.com/img/sampleid1.jpg)
```c#
using IDAnalyzer;

try {
    var p = new Profile(Profile.SECURITY_MEDIUM);
    var s = new Scanner("OlZBrUWs4F60McKKKpuLKNY01XX7sm6B");
    s.throwApiException(true);
    var result = s.quickScan("05.jpg", "", true);
    Console.WriteLine(result);
    s.setProfile(p);
    result = s.scan("05.jpg");
    Console.WriteLine(result);
} catch (ApiError err) {
    Console.WriteLine($"{err.Msg} : {err.Code}");
} catch (InvalidArgumentException err) {
    Console.WriteLine(err.Message);
} catch (Exception err) {
    Console.WriteLine(err.Message);
}
```

## Biometric
There are two primary functions within this class. The first one is verifyFace and the second is verifyLiveness.
```c#
using IDAnalyzer;

try
{
    var p = new Profile(Profile.SECURITY_MEDIUM);
    var b = new Biometric("OlZBrUWs4F60McKKKpuLKNY01XX7sm6B");
    b.throwApiException(true);
    b.setProfile(p);
    Console.WriteLine(b.verifyFace("1.jpg", "2.jpg"));
    Console.WriteLine(b.verifyLiveness("1.jpg"));
}
catch (ApiError err)
{
    Console.WriteLine($"{err.Msg} : {err.Code}");
}
catch (InvalidArgumentException err)
{
    Console.WriteLine(err.Message);
}
catch (Exception err)
{
    Console.WriteLine(err.Message);
}
```

## Contract
All contract-related feature sets are available in Contract class. There are three primary functions in this class.
```c#
using IDAnalyzer;

try {
    var c = new Contract("OlZBrUWs4F60McKKKpuLKNY01XX7sm6B");
    c.throwApiException(true);
    var temp = c.createTemplate("tempName", "<p>%{fullName}</p>");
    var tempId = temp["templateId"].ToString();
    Console.WriteLine(temp);
    Console.WriteLine(c.updateTemplate(tempId, "oldTemp", "<p>%{fullName}</p><p>Hello!!</p>"));
    Console.WriteLine(c.getTemplate(tempId));
    Console.WriteLine(c.listTemplate());
    Console.WriteLine(c.generate(tempId, "PDF", "", new Hashtable() {
        { "fullName", "Tian"},
    }));
    Console.WriteLine(c.deleteTemplate(tempId));
} catch (ApiError err) {
    Console.WriteLine($"{err.Msg} : {err.Code}");
} catch (InvalidArgumentException err) {
    Console.WriteLine(err.Message);
} catch (Exception err) {
    Console.WriteLine(err.Message);
}
```

## Docupass
This category supports all rapid user verification based on the ids and the face images provided.
![DocuPass Screen](https://www.idanalyzer.com/img/docupassliveflow.jpg)
```c#
using IDAnalyzer;

try {
    var d = new Docupass("OlZBrUWs4F60McKKKpuLKNY01XX7sm6B");
    d.throwApiException(true);
    Console.WriteLine(d.createDocupass("bbd8436953ef426e98d078953f258835"));
    Console.WriteLine(d.listDocupass());
    Console.WriteLine(d.deleteDocupass("4PDPN8DRYF17GTEGEUS1T1SN"));
} catch (ApiError err) {
    Console.WriteLine($"{err.Msg} : {err.Code}");
} catch (InvalidArgumentException err) {
    Console.WriteLine(err.Message);
} catch (Exception err) {
    Console.WriteLine(err.Message);
}
```

## Transaction
This function enables the developer to retrieve a single transaction record based on the provided transactionId.
```c#
using IDAnalyzer;

try {
    var t = new Transaction("OlZBrUWs4F60McKKKpuLKNY01XX7sm6B");
    t.throwApiException(true);
    Console.WriteLine(t.getTransaction("bcefdaf921ca4393a6b9174ba44d3e8f"));
    Console.WriteLine(t.listTransaction());
    Console.WriteLine(t.updateTransaction("bcefdaf921ca4393a6b9174ba44d3e8f", "review"));
    Console.WriteLine(t.deleteTransaction("bcefdaf921ca4393a6b9174ba44d3e8f"));
    t.saveImage("9dd555648a7d594eadda39ea32adbd0fc08a1c79a9e4690cdc8d213f188f5376", "test.jpg");
    t.saveFile("transaction-audit-report_iPYNUSKTD5HhpRb4tZpLdHMsiZKVZgWX.pdf", "test.pdf");
    t.exportTransaction("./test.zip", new List<string>() { "a714d58a41874326874c7ce0052717ee", "cb45b0898aeb4a3b8fd578f136f4fafa" }, "json");
} catch (ApiError err) {
    Console.WriteLine($"{err.Msg} : {err.Code}");
} catch (InvalidArgumentException err) {
    Console.WriteLine(err.Message);
} catch (Exception err) {
    Console.WriteLine(err.Message);
}
```

## Api Document
[ID Analyzer Document](https://id-analyzer-v2.readme.io/docs/net)

## Demo
Check out **/demo** folder for more .NET demos.

## SDK Reference
Check out [ID Analyzer .NET Reference](https://idanalyzer.github.io/id-analyzer-nodejs/)
