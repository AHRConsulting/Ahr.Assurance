# Ahr.Assurance.Cli

**One offline report from your tests, for people who don't read the code.** The `dotnet assurance` command builds an HTML report from the evidence AHR Assurance scenarios record: for a test project, a whole solution, an earlier run, or some business areas only.

Install it once per repository:

```bash
dotnet tool install Ahr.Assurance.Cli
```

In each test project's folder, or once in a solution's folder, create the config file. Add `--framework` to add the adapter for the test framework too, such as `--framework xunit-v3`:

```bash
dotnet assurance init
```

Then run the tests and report them:

```bash
dotnet test
dotnet assurance report
```

It's published with Native AOT, so it starts at once.

## Commands

- **`init`** writes `ahr-assurance.json`, with every default and a comment on each setting, and prints the command that adds the adapter for the project's framework. `--framework xunit-v3,playwright` adds those packages at the CLI's version with `dotnet add package`. An adapter or config file already there is left as it is.
- **`report`** runs like `dotnet test`: on the project or solution in the current folder, or the one you name. In a test project's folder it reports the newest run; in a solution's folder, the newest run of each project, in one report.

| Option | What it does |
| --- | --- |
| `--run <id>` | Reports that run instead of the newest |
| `--project <name>` | With a solution, reports only that project. Can be repeated. |
| `--exclude-project <name>` | With a solution, leaves that project out. Can be repeated. |
| `--context <id>` | Shows only that business context. Can be repeated. |
| `--exclude-context <id>` | Leaves that business context out. Can be repeated. |
| `--title <text>` | Names the report |
| `--output <folder>` | Writes the report there |
| `--developer` | Adds failure details, expected and actual values and test names |

`dotnet assurance report --help` lists every option.

## In CI

Run the report step even when `dotnet test` fails, so the report shows what failed. Set `ASSURANCE_RUN_ID` to one value across shards, so they write to the same run. Upload `report.html` and the `attachments` folder beside it as one artifact: the report has no scripts or external resources, and works offline and on a phone.

The exit code is 0 when every check passed, 2 when the report shows failures, and 1 when the command couldn't run, so a pipeline can tell a broken step from failing checks.

## Learn more

- [The CLI](https://ahrconsulting.github.io/assurance/docs/cli.html)
- [Running in CI](https://ahrconsulting.github.io/assurance/docs/ci.html)
- [Documentation and guide](https://ahrconsulting.github.io/assurance/)
- [Changelog](https://github.com/AHRConsulting/Ahr.Assurance/blob/main/CHANGELOG.md)
- [Source and issues](https://github.com/AHRConsulting/Ahr.Assurance)

Licensed under the Apache License, Version 2.0.
