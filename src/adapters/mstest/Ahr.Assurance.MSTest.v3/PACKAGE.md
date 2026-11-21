# Ahr.Assurance.MSTest.v3

**Know what your code does, in words everyone can read.** The MSTest 3 adapter for AHR Assurance turns the tests you already have into business-readable scenarios: named Given, When and Then steps around the code each test contains anyway, with evidence and an offline HTML report for every run. Tests keep MSTest, its runner and its assertions.

## Install

Install the CLI, which builds the report, once per repository:

```bash
dotnet tool install Ahr.Assurance.Cli
```

Add this adapter to each test project:

```bash
dotnet add package Ahr.Assurance.MSTest.v3 --version 0.1.0
```

For MSTest 4, use `Ahr.Assurance.MSTest`. The adapter brings `Ahr.Assurance` and its analyzers, and needs .NET 10 or later. For browser tests, add `Ahr.Assurance.Playwright` too: you write Playwright as usual, and the report gets a screenshot of each step. Then create the config file in the test project's folder:

```bash
dotnet assurance init
```

## Write a scenario

<!-- snippet: mstest-v3-readme -->
```csharp
[TestClass]
public sealed class BasketTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public Task AnEmptyBasketCostsNothing()
    {
        List<decimal> basket = [];
        decimal total = -1;

        return Scenario.Define("An empty basket costs nothing")
            .Given("An empty basket", basket.Clear)
            .When("The total is worked out", () => total = basket.Sum())
            .Then("It is zero", () => Assert.AreEqual(0m, total))
            .VerifyAsync(TestContext);
    }
}
```

- `Define` names the guarantee. `Given` sets up, `When` acts, and `Then` and `And` check, with ordinary assertions.
- MSTest 3 has no current-test context, so pass the test's: `VerifyAsync(TestContext)`. It runs the steps, writes evidence, then passes or fails the test the way MSTest expects.
- Assertion failures become failed expectations, and `Assert.Inconclusive` inside a step reports the test as inconclusive. Other exceptions are reported as execution errors.
- Scenarios are cancelled with the test, including by `[Timeout]`.
- `Attach.File`, `Attach.Bytes` and `Attach.Text` add screenshots, files and text to the running step.
- The namespace is `Ahr.Assurance.MSTest`, as in `Ahr.Assurance.MSTest`, so moving to MSTest 4 changes only the package.

## Run it and report it

```bash
dotnet test
dotnet assurance report
```

The test output shows each scenario's steps and verdicts. `dotnet assurance report` then builds the report: for the project in its folder, or for every test project in one report in a solution's folder. It leads with what passed and what needs attention, works offline, and is written for people outside the team. Add `--developer` for failure details, expected and actual values and test names.

## Parameterised tests

Keep `[DataRow]` or `[DynamicData]`, on MSTest 3.7 and later, which give a test its row, as they are. Give the scenario the same name for every row, and the report shows the rows as one journey, with a table of examples.

## Reusable steps

When tests share steps, write each step once in a steps class over a context, `ScenarioSteps<TContext>`, and use it by name, as in `.Given(ReadingSteps.ASavedDocument("handbook"))`. Steps written for an interface are shared by every scenario whose context implements it. See [reusable steps](https://ahrconsulting.github.io/assurance/docs/scenarios.html#reusable-steps).

## Business context

`BusinessContext.Define(...)` links scenarios to the capabilities, outcomes, rules and risks they cover. The report then groups journeys into areas, with what is at stake in each. See [business context](https://ahrconsulting.github.io/assurance/docs/business-context.html).

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
- [Running in CI](https://ahrconsulting.github.io/assurance/docs/ci.html)
- [Analyzer rules](https://ahrconsulting.github.io/assurance/docs/analyzers.html)
- [API reference](https://ahrconsulting.github.io/assurance/api/Ahr.Assurance.Execution.html)
- [Changelog](https://github.com/AHRConsulting/Ahr.Assurance/blob/main/CHANGELOG.md)
- [Source and issues](https://github.com/AHRConsulting/Ahr.Assurance)
- [Agent skill for AI coding assistants](https://github.com/AHRConsulting/agent-skills)

Licensed under the Apache License, Version 2.0.
