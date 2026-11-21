# Ahr.Assurance

**Know what your code does, in words everyone can read.** AHR Assurance turns the .NET tests you already have into business-readable scenarios: named Given, When and Then steps around the code each test contains anyway, with evidence and an offline HTML report for every run.

This is the core package: scenarios, business context, evidence and the results model, with the analyzers. Most projects install the adapter for their test framework, which brings this package with it:

| Framework | Package |
| --- | --- |
| xUnit v3 | `Ahr.Assurance.Xunit.v3` |
| xUnit v2 | `Ahr.Assurance.Xunit.v2` |
| NUnit 3.14, 4 or 5 | `Ahr.Assurance.NUnit` |
| MSTest 4 | `Ahr.Assurance.MSTest` |
| MSTest 3 | `Ahr.Assurance.MSTest.v3` |
| TUnit | `Ahr.Assurance.TUnit` |

```bash
dotnet add package Ahr.Assurance.Xunit.v3 --version 0.1.0
```

It needs .NET 10 or later, and has no test-framework dependency.

## Business context

Link scenarios to the capabilities, outcomes, rules and risks they cover, and the report groups journeys into areas, with what is at stake in each:

<!-- snippet: business-context -->
```csharp
private static readonly BusinessContext _offlineDocuments = BusinessContext
    .Define("offline-document-access", "Offline document access", "Keep documents available when connectivity changes")
    .Capability("read-saved-documents", "Read a saved document without a network")
    .Outcome("saved-content-available", "Saved content is returned to the reader")
    .Rule("offline-does-not-fetch", "Offline reads never contact the web source")
    .Risk("offline-content-missing", "A returning reader cannot access a saved document")
    .Reference("jira", "DOC-42", new Uri("https://example.atlassian.net/browse/DOC-42"))
    .Build();
```

## Evidence

- Each verification writes versioned JSON into the run's folder, with its attachments beside it. Tools in any language can read it with the [JSON schema](https://github.com/AHRConsulting/Ahr.Assurance/blob/main/schemas/assurance-evidence.v1.json).
- `Attach.File`, `Attach.Bytes` and `Attach.Text` add screenshots, files and text to the running step.
- `AssuranceResults.LoadAsync` reads a run folder for your own summaries or checks, and finds each attachment, also after the folder has moved.
- A sink receives each verification's evidence as it is verified. Implement `IAssuranceEvidenceSink` and list it in `ReportOptions.Sinks`.

## Reports

The CLI, `Ahr.Assurance.Cli`, builds one offline HTML report from a run, a project or a solution. Install it once per repository:

```bash
dotnet tool install Ahr.Assurance.Cli
```

In each test project's folder, or once in a solution's folder, create the config file:

```bash
dotnet assurance init
```

Then run the tests and report them:

```bash
dotnet test
dotnet assurance report
```

## Analyzers

| Rule | Severity | Reports |
| --- | --- | --- |
| `AHRA101` | Warning | A step lambda returns the task it assigns |
| `AHRA102` | Warning | A scenario is not verified |
| `AHRA103` | Warning | A resource is disposed before the scenario runs |
| `AHRA105` | Warning | A scenario's steps are out of order or incomplete |
| `AHRA106` | Warning | Scenarios share a name and have no ID |
| `AHRA107` | Warning | An inline scenario is stored for reuse |
| `AHRA108` | Info | A step property can be static |

## Learn more

- [Documentation and guide](https://ahrconsulting.github.io/assurance/)
- [Getting started](https://ahrconsulting.github.io/assurance/docs/getting-started.html)
- [Evidence](https://ahrconsulting.github.io/assurance/docs/evidence.html)
- [API reference](https://ahrconsulting.github.io/assurance/api/Ahr.Assurance.Execution.html)
- [Changelog](https://github.com/AHRConsulting/Ahr.Assurance/blob/main/CHANGELOG.md)
- [Source and issues](https://github.com/AHRConsulting/Ahr.Assurance)
- [Agent skill for AI coding assistants](https://github.com/AHRConsulting/agent-skills)

Licensed under the Apache License, Version 2.0.
