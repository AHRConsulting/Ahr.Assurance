# Samples

One small application, a document reader that keeps working offline, tested the same way under each test framework.

| Project | What it shows |
|---|---|
| `Ahr.Assurance.Sample.Core` | The application, its fakes, the business context, reusable steps and the scenarios. No test framework. |
| `Ahr.Assurance.Sample.Xunit.v3` | The shared scenarios under xUnit v3, and one scenario written in the test with xUnit's `Assert`, run once for each data row. |
| `Ahr.Assurance.Sample.Xunit.v2` | The same under xUnit v2 on VSTest. Each test passes its `ITestOutputHelper`. |
| `Ahr.Assurance.Sample.NUnit` | The same under NUnit, with NUnit's `Assert`. |
| `Ahr.Assurance.Sample.MSTest` | The same under MSTest, with MSTest's `Assert`. |
| `Ahr.Assurance.Sample.MSTest.v3` | The same under MSTest 3. Each test passes its `TestContext`. |
| `Ahr.Assurance.Sample.TUnit` | The same under TUnit, with TUnit's `Assert`. |
| `Ahr.Assurance.Sample.Playwright` | The scenarios through a page in a headless Chromium, with a screenshot after each step. |

The shared steps check with `ExpectationResult`, so they work under any framework, and the report shows what was expected beside what happened. Every framework's sample records the same scenarios with the same steps, so their reports match.

## Running a sample

```bash
dotnet test --project samples/Ahr.Assurance.Sample.Xunit.v3
dotnet assurance report samples/Ahr.Assurance.Sample.Xunit.v3
```

The xUnit v2 sample runs on VSTest. In this repository, run it with `dotnet vstest`:

```bash
dotnet build samples/Ahr.Assurance.Sample.Xunit.v2 -c Release
dotnet vstest samples/Ahr.Assurance.Sample.Xunit.v2/bin/Release/net10.0/Ahr.Assurance.Sample.Xunit.v2.dll
```

The Playwright sample needs Chromium, installed once after a build:

```bash
pwsh samples/Ahr.Assurance.Sample.Playwright/bin/Debug/net10.0/playwright.ps1 install chromium
```

Without it, its tests are skipped.

## Packages

Each sample references the `Ahr.Assurance` packages, as a project of yours would. In this repository, `Directory.Build.targets` builds them against the libraries' source instead. `scripts/verify-package.sh` copies the samples out of the repository and runs them against the packed packages, including with older versions of NUnit, MSTest and xUnit v3.
