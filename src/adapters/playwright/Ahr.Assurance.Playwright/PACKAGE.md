# Ahr.Assurance.Playwright

**See what the browser saw at each step.** Playwright integration for AHR Assurance: screenshots and traces of a page, attached to the evidence of each scenario step and shown in the report. It works under every Assurance adapter.

```bash
dotnet add package Ahr.Assurance.Playwright --version 0.1.0
```

Add it beside the adapter for your test framework, such as `Ahr.Assurance.Xunit.v3`.

## Capture a page

<!-- snippet: playwright -->
```csharp
public static async Task AnEmptyBasketCostsNothing(IPage page)
{
    using IDisposable capture = AssurancePlaywright.Capture(page);
    _ = await Scenario.Define("An empty basket costs nothing")
        .Given("An empty basket", () => page.GotoAsync("/basket"))
        .When("The total is worked out", () => page.ClickAsync("#update-total"))
        .Then("It is zero", async () => Assert.Equal("£0.00", await page.InnerTextAsync("#total")))
        .VerifyAsync();
}
```

- By default, a step that fails gets a screenshot of the visible page, shown under that step in the report. A screenshot taken inside the step itself can't do this, because the step stops at the failed assertion.
- Capture covers every scenario verified in the current test until it is disposed. A screenshot that can't be taken never fails the scenario.
- For a screenshot at a point you choose, no capture is needed: `Attach.Bytes("Basket", await page.ScreenshotAsync(), "image/png")` inside the step.

## Options

| Option | What it does |
| --- | --- |
| `Screenshots = ScreenshotCapture.AfterEachStep` | Takes a screenshot after every step |
| `FullPage = true` | Captures the whole scrollable page |
| `Trace = TraceCapture.OnFailure` or `TraceCapture.Always` | Records the scenario's Playwright trace and attaches it, to open with `playwright show-trace`. Recording a trace slows the scenario. |

Pass them as `AssurancePlaywright.Capture(page, new PlaywrightCaptureOptions { ... })`.

## Learn more

- [Playwright](https://ahrconsulting.github.io/assurance/docs/adapters.html#playwright)
- [Documentation and guide](https://ahrconsulting.github.io/assurance/)
- [Changelog](https://github.com/AHRConsulting/Ahr.Assurance/blob/main/CHANGELOG.md)
- [Source and issues](https://github.com/AHRConsulting/Ahr.Assurance)

Licensed under the Apache License, Version 2.0.
