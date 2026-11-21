# Changelog

## 0.1.0

The first release.

### Packages

| Package | Contents |
|---|---|
| `Ahr.Assurance` | Scenarios, business metadata, evidence, sinks and the results model, with [analyzers](https://ahrconsulting.github.io/assurance/docs/analyzers.html) for common mistakes. No test-framework dependency. |
| `Ahr.Assurance.Xunit.v3` | The xUnit v3 adapter, for every xUnit v3 release. |
| `Ahr.Assurance.Xunit.v2` | The xUnit v2 adapter. Tests pass their `ITestOutputHelper`: `VerifyAsync(output)`. |
| `Ahr.Assurance.NUnit` | The NUnit adapter, for NUnit 3.14, 4 and 5. |
| `Ahr.Assurance.MSTest` | The MSTest 4 adapter. |
| `Ahr.Assurance.MSTest.v3` | The MSTest 3 adapter. Tests pass their `TestContext`: `VerifyAsync(TestContext)`. |
| `Ahr.Assurance.TUnit` | The TUnit adapter. |
| `Ahr.Assurance.Playwright` | Screenshots and traces of a Playwright page, attached to each step's evidence, under any adapter. |
| `Ahr.Assurance.Cli` | The `dotnet assurance` command: `init` and `report`. |

All packages target .NET 10. The libraries depend only on .NET and, for each adapter, its test framework; `Ahr.Assurance.Playwright` depends on Playwright.

### Writing tests

- `Scenario.Define(...)` with inline `Given`, `When`, `Then` and `And` steps over ordinary tests, keeping native assertions. `VerifyAsync()` passes or fails the test the way the framework expects.
- When a step throws an unexpected exception, the test failure names the exception's type and carries the exception, with its inner exceptions and stack trace.
- A failed test's stack trace starts at the line that failed in the test's own code, such as an assertion in a step, before the frames inside Assurance.
- Typed scenarios with a fresh context per run, and reusable steps for shared journeys. `ScenarioSteps<T>` is the same base class under every adapter.
- Steps written for an interface or base class serve every scenario whose context implements it. A step that takes values is a method that writes its own name, as the report shows it. `WithValue` records an input in the evidence, for tracing, a collection as its items, and the report's developer view shows it beside the step.
- `Scenario.Define<TContext>(name, business)` creates the context with its parameterless constructor.
- Business contexts with capabilities, outcomes, rules (`BusinessRule`), risks and objectives, and ticket references on contexts and scenarios.
- `Attach.File`, `Attach.Bytes` and `Attach.Text` add files, screenshots and text to the running step. With `AttachmentKeep.OnFailure`, an attachment is kept only when its scenario doesn't pass, and nothing is written when it does.
- Throws steps: `WhenThrows<T>` and `AndThrows<T>` pass only when their work throws exactly `T`, and `WhenThrowsAny<T>` and `AndThrowsAny<T>` also when it throws a type derived from `T`. `Then` and `And` steps whose lambda takes the exception get it typed. Their work can be a read that returns a value. Checks of one type pair in order with the throws steps of that type. A throws step checks what its work threw, so a scenario can end with it. Steps classes have `CreateThrowsStep<T>`, `CreateThrowsAnyStep<T>` and `CreateThrownStep<T>`.
- A `When`, or an `And` after it, that returns a value hands it to the `Then` and `And` steps after it whose lambda takes its type, `null` included. What a `Given` step returns is setup, and is not kept. A check reads a value returned in its own When and Then block; one whose block returns nothing of its type is refused before the scenario runs. When a step in that block returns the struct's nullable form, such as `int?` for a check of `int`, the refusal names that step. A step that returns a `ValueTask<T>` is awaited and gives its `T`; a plain `ValueTask` is awaited and gives nothing. In steps classes, a `CreateActionStep` callback that returns a value keeps it, and `CreateReturnedStep<T>` checks it.
- Asynchronous steps can take the run's cancellation token, inline, in typed scenarios and in steps classes, as can checks of a step's result or exception.
- Each step starts on the synchronization context the test was on when it called `VerifyAsync()`.
- Culture and async-local values set in a step are in effect in the steps after it, as in a plain test.
- A step's name is the text its author writes; Assurance adds no formatting to it. Row IDs write a type as C# does.
- Every `Define` takes the scenario's name first and its ID last; the ID is optional and defaults to the name.
- `AssurancePlaywright.Capture(page)` attaches a screenshot of the page after a failed step, or after every step, and optionally the scenario's Playwright trace.
- `Given` steps are optional: a call with nothing to set up starts at its `When`.
- A workflow can act again after a check: `When` then `Then`, repeated after the first `Given`. A failed check stops the scenario before its next `When`.
- Ordinary assertions work in every step. A failed assertion in `Then` is a failed expectation and later checks still run; in `Given` or `When` it fails that step and the steps after it are not run. Other exceptions are reported as execution errors.
- xUnit, NUnit, MSTest and TUnit asserts, Shouldly, FluentAssertions and Playwright's `Expect(...)` are recognised under every adapter.
- Skipping inside a step (`Assert.Skip`, `Assert.Ignore`, `Assert.Inconclusive`) records the step as skipped and reports the test as skipped by its framework.
- Each row of a parameterised test is recorded as an example of one scenario, with its arguments, a collection argument as its items, and its own ID.
- Analyzers AHRA101–AHRA103 and AHRA105–AHRA108 report steps that wait for an assigned task, scenarios that are never verified, resources disposed before a scenario runs, steps out of order, shared scenario names, stored inline scenarios and step properties that can be static. Four have code fixes.
- In projects with implicit usings, the packages add global usings for their namespaces. `AhrAssuranceImplicitUsings` set to `false` leaves them out.
- The xUnit, NUnit, MSTest and TUnit adapters pass the same conformance checks and produce the same evidence.

### Evidence

- Each verification writes versioned JSON (schema 1), with its attachments in a folder beside it. The schema is in [`schemas/assurance-evidence.v1.json`](schemas/assurance-evidence.v1.json).
- `dotnet assurance init` creates `ahr-assurance.json` in a test project's folder, and prints the command that adds the adapter for its framework. `--framework xunit-v3,playwright` adds those packages at the CLI's version. In a solution's folder it sets up each test project. Each test run writes to its own folder, named by its run ID. `ASSURANCE_RUN_ID` names the run, and `keep` removes older run folders.
- Custom sinks receive the same evidence as the built-in JSON, HTML and text output.
- `AssuranceResults.LoadAsync` reads a run folder after a run.

### Reports

- `dotnet assurance report` builds one offline HTML report, organised by business area and what is at stake, for people who don't read the code. It has no scripts or external resources and works on a phone.
- The report opens with a small "Delivery report" label above its title: the `--title` or `report.title` given, else the project, solution or single business area it covers, else "Delivery report" alone. `report.version` adds a version after the title, such as "AHR Assurance 0.1.0". The `report.title` and `report.version` of an `ahr-assurance.json` beside a solution name the solution's report; a project in a subfolder needs a config of its own to be in that report. `--title` replaces both. The AHR Assurance logo and name sit in the footer; the version is in the page's generator tag.
- In a test project's folder it reports the newest run, inside that run's folder. In a solution's folder it reports the newest run of each test project, with a section per project, beside the solution.
- `--run`, `--project`, `--exclude-project`, `--context`, `--title`, `--output`, `--input` and `--attachments` choose what to report and where. `--developer` adds developer detail.
- `--exclude-context` leaves out a business context, such as checks only developers read, and `report.excludeContexts` in `ahr-assurance.json` does the same for every report of the project, including the one written after a run. The report names the areas it leaves out.
- The exit code reflects the whole run, including when a report is filtered.
- A run cancelled before any scenario ran is still reported, and the report says its checks could not finish.
- A scenario with several examples counts as one journey in every count, and passes only when all its examples pass.
- In a solution report, projects can use the same business context ID for different areas. A context defined two ways in one project fails the command before anything is written, and the error names both definitions.
- `--overwrite` replaces the earlier report in full: attachments and developer pages it no longer shows are removed.
- On Microsoft.Testing.Platform, each adapter records every test's outcome, so reports say how many tests ran. `report.afterRun` or `--assurance-report on` also writes the run's report when `dotnet test` finishes; by default the report comes from `dotnet assurance report`.
- The rows of a parameterised test are shown as one journey with an examples table. Verifications are listed in the order they ran.
- The step that stopped execution is named, also when details are withheld.
- When every scenario passed but other tests in the run failed, the headline says how many failed, and the summary shows the run as failed.
- A warning when tests in one project record the same scenario ID.
- A warning when the evidence was recorded by a different Ahr.Assurance version than the CLI's.

### Known limits

- A report covers only the checks that ran. A test-run summary counts the tests each test process ran, but nothing establishes that every intended project or shard ran.
- The test-run summary and the optional report at the end of a run need Microsoft.Testing.Platform, 1.x or 2.x. Under VSTest, including every xUnit v2 project, reports have no test count.
- A solution report uses each project's newest run, which may come from different test runs. Each project's section shows when it was tested.
- With `keep: 1`, two test processes starting together, such as a project targeting two frameworks, can remove each other's run folder.
- There is no history, trend or comparison across runs.
- In the NUnit adapter, `Assert.Warn` warnings inside a step are not kept, and `Assert.Pass` inside a step is an execution error.
- On MSTest 4.5 and later, the MSTest adapter finds the running test through `TestContext.Current`, which MSTest marks as experimental. On MSTest 3 and 4.0 to 4.4, tests pass their `TestContext`. Rows of a data-driven test are examples on MSTest 3.7 and later.
- xUnit v2 has no per-test cancellation and no skip inside a running test; with `Xunit.SkippableFact`, `Skip.If(...)` in a step skips the test.
- On NUnit 3.14, scenarios aren't cancelled with the test, since NUnit 3 gives tests no cancellation token.
- Rows of a parameterised test are listed in the order they ran, not their order in the source.
