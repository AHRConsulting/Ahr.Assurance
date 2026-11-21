# Contributing

Contributions are accepted under the [Apache License 2.0](LICENSE).

## Before you open a PR

```bash
dotnet build Ahr.Assurance.slnx -c Release
dotnet test Ahr.Assurance.slnx -c Release
dotnet format Ahr.Assurance.slnx --verify-no-changes --severity warn
```

Update docs in the same change as the behaviour they describe.

## Public API rules

- Default to private, then internal. A type is public only for a demonstrated consumer or extension contract.
- `Ahr.Assurance` and the adapters depend only on .NET and their test framework.
- The CLI exposes no public types.

## Adapter contract

An adapter supplies only:

1. Assertion classification: whether an exception is a failed expectation or an execution error.
2. The current test's identity, cancellation token and captured output.
3. A native final failure, after evidence has been published.
4. Optional project-level defaults.

Everything else belongs in `Ahr.Assurance`. If an adapter needs more, the logic probably belongs in the core.

## CLI rules

The CLI never guesses. It never scans the checkout, never picks a "latest" run, and never merges correlations by itself. Any default must be one documented, deterministic location that is printed back, and explicit `--input` and `--run` always win.

## Performance

Keep per-verification work small: at most one JSON write in the test process, no locks (unique files only), bounded text, and no I/O when sinks are empty.

`AllocationBudgetTests` enforces these budgets on every test run:

| What | Budget |
|---|---|
| Allocations to run one scenario of 3 steps | 2,600 bytes |
| Allocations to verify one scenario, with no sinks or text | 4,350 bytes |

The benchmarks are in `benchmarks/Ahr.Assurance.Benchmarks`:

```bash
dotnet run -c Release --project benchmarks/Ahr.Assurance.Benchmarks -- --filter '*' --job short
```

## Adding an adapter

Adapters live in `src/adapters/<framework>/`, and their tests in `tests/adapters/<framework>/`. Create a test project there that implements `IAdapterConformanceDriver` from `tests/Ahr.Assurance.Adapters.Conformance` and runs every `AdapterConformance.CaseNames` entry under the new framework. See `XunitAdapterConformanceTests`. If a case cannot pass, fix the contract or the core rather than adding adapter-specific behaviour.

## Trying a local build of the CLI

```bash
dotnet pack src/Ahr.Assurance.Cli -c Release -o artifacts/tool-package -p:AhrAssuranceVersion=0.1.0-try.1
dotnet tool install Ahr.Assurance.Cli --version 0.1.0-try.1 --add-source artifacts/tool-package
```

Use a new `AhrAssuranceVersion` each time you repack, so a cached package isn't picked up.
