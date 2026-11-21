using Ahr.Assurance.Execution;
using Ahr.Assurance.Reporting;
using Ahr.Assurance.Xunit;

namespace Ahr.Assurance.Cli.Tests.Specifications;

public sealed class SettingUpAProject
{
    [Fact]
    public Task InitWritesEveryDefault() =>
        Scenario.Define<ToolUnderTest>("'dotnet assurance init' writes ahr-assurance.json with every default, and adds no package", CliBehaviour.Setup)
            .Given(InitSteps.AProject("xunit.v3/3.2.2"))
            .When(ToolSteps.InitRuns)
            .Then(ReportSteps.TheCliExitsWith(0))
            .And("ahr-assurance.json holds the template, with every default and what it does", tool =>
                Assert.Equal(AssuranceConfig.Template, tool.Read(AssuranceConfig.FileName)))
            .And(InitSteps.NoPackageIsAdded)
            .VerifyAsync();

    [Theory]
    [InlineData("Microsoft.NET.Sdk", "xunit.v3/3.2.2", "xUnit v3", "xunit-v3")]
    [InlineData("Microsoft.NET.Sdk", "xunit.v3.core/3.2.2", "xUnit v3", "xunit-v3")]
    [InlineData("Microsoft.NET.Sdk", "xunit/2.9.3", "xUnit v2", "xunit-v2")]
    [InlineData("Microsoft.NET.Sdk", "NUnit/4.4.0", "NUnit", "nunit")]
    [InlineData("Microsoft.NET.Sdk", "MSTest/4.0.1", "MSTest 4", "mstest")]
    [InlineData("Microsoft.NET.Sdk", "MSTest/3.11.1", "MSTest 3", "mstest-v3")]
    [InlineData("Microsoft.NET.Sdk", "MSTest.TestFramework/[3.6,)", "MSTest 3", "mstest-v3")]
    [InlineData("MSTest.Sdk/4.0.1", "", "MSTest 4", "mstest")]
    [InlineData("MSTest.Sdk/3.11.1", "", "MSTest 3", "mstest-v3")]
    [InlineData("Microsoft.NET.Sdk", "TUnit/0.90.0", "TUnit", "tunit")]
    public Task InitNamesTheAdapter(string sdk, string package, string framework, string name) =>
        Scenario.Define<ToolUnderTest>("'dotnet assurance init' names the command that adds the adapter for the project's test framework", CliBehaviour.Adapters,
                description: "xUnit v2 and v3, and MSTest 3 and 4, need different adapters, which their package names don't show.")
            .Given($"A test project on {sdk} referencing {Shown(package)}", tool => tool.ProjectFile("Checkout.Tests.csproj", sdk, Packages(package)))
            .When(ToolSteps.InitRuns)
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheCliSays($"Checkout.Tests.csproj uses {framework}. To add its adapter: dotnet assurance init --framework {name}"))
            .And(InitSteps.NoPackageIsAdded)
            .And(InitSteps.TheConfigIsWritten)
            .VerifyAsync();

    [Theory]
    [InlineData("MSTest", "3.11.1", "MSTest 3", "mstest-v3")]
    [InlineData("MSTest", "4.0.1", "MSTest 4", "mstest")]
    [InlineData("xunit", "2.9.3", "xUnit v2", "xunit-v2")]
    public Task InitReadsCentralVersions(string package, string version, string framework, string name) =>
        Scenario.Define<ToolUnderTest>("'dotnet assurance init' reads a version set in Directory.Packages.props", CliBehaviour.Adapters)
            .Given($"Directory.Packages.props sets {package} to {version}, and the project references {package} without a version", tool =>
            {
                File.WriteAllText(tool.PathOf("Directory.Packages.props"),
                    $"<Project><ItemGroup><PackageVersion Include=\"{package}\" Version=\"{version}\" /></ItemGroup></Project>");
                tool.ProjectFile("Checkout.Tests/Checkout.Tests.csproj", "Microsoft.NET.Sdk", package);
            })
            .When("'dotnet assurance init' runs in Checkout.Tests", tool => tool.Init("Checkout.Tests"))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheCliSays($"Checkout.Tests.csproj uses {framework}. To add its adapter: dotnet assurance init --framework {name}"))
            .VerifyAsync();

    [Fact]
    public Task InitReadsTheMSTestSdkFromGlobalJson() =>
        Scenario.Define<ToolUnderTest>("'dotnet assurance init' reads the MSTest.Sdk version from global.json", CliBehaviour.Adapters)
            .Given("global.json sets MSTest.Sdk to 3.11.1, and the project uses MSTest.Sdk without a version", tool =>
            {
                File.WriteAllText(tool.PathOf("global.json"), """{ "msbuild-sdks": { "MSTest.Sdk": "3.11.1" } }""");
                tool.ProjectFile("Checkout.Tests.csproj", "MSTest.Sdk");
            })
            .When(ToolSteps.InitRuns)
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheCliSays("Checkout.Tests.csproj uses MSTest 3. To add its adapter: dotnet assurance init --framework mstest-v3"))
            .VerifyAsync();

    [Fact]
    public Task InitNamesPlaywrightToo() =>
        Scenario.Define<ToolUnderTest>("'dotnet assurance init' names Playwright in the command when the project uses Microsoft.Playwright", CliBehaviour.Adapters)
            .Given(InitSteps.AProject("xunit.v3/3.2.2", "Microsoft.Playwright/1.55.0"))
            .When(ToolSteps.InitRuns)
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheCliSays("To add its adapter: dotnet assurance init --framework xunit-v3,playwright"))
            .VerifyAsync();

    [Fact]
    public Task InitNamesPlaywrightBesideAnAdapter() =>
        Scenario.Define<ToolUnderTest>("'dotnet assurance init' names the Playwright command when the project has its adapter but not Playwright's", CliBehaviour.Adapters)
            .Given(InitSteps.AProject("NUnit/4.4.0", "Ahr.Assurance.NUnit/0.1.0", "Microsoft.Playwright/1.55.0"))
            .When(ToolSteps.InitRuns)
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheCliSays("To add a screenshot of each step: dotnet assurance init --framework playwright"))
            .And(ReportSteps.TheCliSays("Run the tests, then 'dotnet assurance report'."))
            .VerifyAsync();

    [Fact]
    public Task InitNamesTheFrameworksWhenNoneIsFound() =>
        Scenario.Define<ToolUnderTest>("'dotnet assurance init' lists the framework names when the project references no test framework it knows", CliBehaviour.Adapters)
            .Given(InitSteps.AProject("Shouldly/4.3.0"))
            .When(ToolSteps.InitRuns)
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheCliSays("Checkout.Tests.csproj references no test framework init knows. To add the adapter for yours: dotnet assurance init " +
                "--framework <name>, with one of: xUnit v3 (xunit-v3), xUnit v2 (xunit-v2), NUnit (nunit), MSTest 4 (mstest), MSTest 3 (mstest-v3), TUnit (tunit)"))
            .And(InitSteps.NoPackageIsAdded)
            .And(InitSteps.TheConfigIsWritten)
            .VerifyAsync();

    [Theory]
    [InlineData("xunit.v3/3.2.2;NUnit/4.4.0", "xUnit v3 (xunit-v3), NUnit (nunit)")]
    [InlineData("MSTest/$(MSTestVersion)", "MSTest 4 (mstest), MSTest 3 (mstest-v3)")]
    public Task InitListsTheAdaptersWhenMoreThanOneFits(string packages, string listed) =>
        Scenario.Define<ToolUnderTest>("'dotnet assurance init' lists the adapters when more than one could fit", CliBehaviour.Adapters)
            .Given(InitSteps.AProject(packages.Split(';')))
            .When(ToolSteps.InitRuns)
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheCliSays($"Checkout.Tests.csproj could use more than one adapter: {listed}. To add the one it runs on: dotnet assurance init --framework <name>"))
            .And(InitSteps.NoPackageIsAdded)
            .VerifyAsync();

    [Theory]
    [InlineData("xunit.v3/3.2.2", "xunit-v3", "Ahr.Assurance.Xunit.v3")]
    [InlineData("xunit/2.9.3", "xunit-v2", "Ahr.Assurance.Xunit.v2")]
    [InlineData("NUnit/4.4.0", "nunit", "Ahr.Assurance.NUnit")]
    [InlineData("MSTest/4.0.1", "mstest", "Ahr.Assurance.MSTest")]
    [InlineData("MSTest/3.11.1", "mstest-v3", "Ahr.Assurance.MSTest.v3")]
    [InlineData("TUnit/0.90.0", "tunit", "Ahr.Assurance.TUnit")]
    public Task FrameworkAddsTheAdapter(string package, string name, string adapter) =>
        Scenario.Define<ToolUnderTest>("'dotnet assurance init --framework' adds the adapter at the CLI's version and writes ahr-assurance.json", CliBehaviour.Adapters)
            .Given(InitSteps.AProject(package))
            .When(InitSteps.InitRunsWith(name))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(InitSteps.TheProjectGot(adapter))
            .And(InitSteps.EachPackageIsAtTheCliVersion)
            .And(ReportSteps.TheCliSays($"Added {adapter} {AssuranceVersion.Current} to Checkout.Tests.csproj"))
            .And(ReportSteps.TheCliSays("Run the tests, then 'dotnet assurance report'."))
            .And(InitSteps.TheConfigIsWritten)
            .VerifyAsync();

    [Theory]
    [InlineData("xunit-v3,playwright")]
    [InlineData("xunit-v3", "playwright")]
    public Task FrameworkAddsPlaywrightBesideTheAdapter(params string[] frameworks) =>
        Scenario.Define<ToolUnderTest>("'dotnet assurance init --framework' adds Playwright beside the adapter, named in a list or once each", CliBehaviour.Adapters)
            .Given(InitSteps.AProject("xunit.v3/3.2.2"))
            .When(InitSteps.InitRunsWith(frameworks))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(InitSteps.TheProjectGot("Ahr.Assurance.Xunit.v3", "Ahr.Assurance.Playwright"))
            .And(InitSteps.EachPackageIsAtTheCliVersion)
            .And(InitSteps.TheConfigIsWritten)
            .VerifyAsync();

    [Fact]
    public Task PlaywrightAlreadyThereIsLeftAlone() =>
        Scenario.Define<ToolUnderTest>("'dotnet assurance init --framework' leaves Playwright's package when the project already references it", CliBehaviour.Adapters)
            .Given(InitSteps.AProject("xunit.v3/3.2.2", "Microsoft.Playwright/1.55.0", "Ahr.Assurance.Playwright/0.1.0"))
            .When(InitSteps.InitRunsWith("xunit-v3,playwright"))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(InitSteps.TheProjectGot("Ahr.Assurance.Xunit.v3"))
            .And(ReportSteps.TheCliSays("Checkout.Tests.csproj already references Ahr.Assurance.Playwright."))
            .And(InitSteps.TheConfigIsWritten)
            .VerifyAsync();

    [Fact]
    public Task PlaywrightJoinsAnAdapterAlreadyThere() =>
        Scenario.Define<ToolUnderTest>("'dotnet assurance init --framework playwright' adds Playwright to a project that has its adapter", CliBehaviour.Adapters)
            .Given(InitSteps.AProject("NUnit/4.4.0", "Ahr.Assurance.NUnit/0.1.0"))
            .When(InitSteps.InitRunsWith("playwright"))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(InitSteps.TheProjectGot("Ahr.Assurance.Playwright"))
            .And(InitSteps.EachPackageIsAtTheCliVersion)
            .And(ReportSteps.TheCliSays("Checkout.Tests.csproj already references Ahr.Assurance.NUnit."))
            .And(InitSteps.TheConfigIsWritten)
            .VerifyAsync();

    [Fact]
    public Task PlaywrightNeedsAnAdapter() =>
        Scenario.Define<ToolUnderTest>("'dotnet assurance init --framework playwright' stops when the project has no adapter for it to work beside", CliBehaviour.Adapters)
            .Given(InitSteps.AProject("xunit.v3/3.2.2"))
            .When(InitSteps.InitRunsWith("playwright"))
            .Then(ReportSteps.TheCliExitsWith(1))
            .And(ReportSteps.TheCliSays("Name its test framework too, such as 'dotnet assurance init --framework xunit-v3,playwright'. Nothing changed."))
            .And(InitSteps.NothingChanged)
            .VerifyAsync();

    [Fact]
    public Task FrameworkChoosesBetweenTwo() =>
        Scenario.Define<ToolUnderTest>("'dotnet assurance init --framework' picks the adapter when the project could use more than one", CliBehaviour.Adapters)
            .Given(InitSteps.AProject("xunit.v3/3.2.2", "NUnit/4.4.0"))
            .When(InitSteps.InitRunsWith("nunit"))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(InitSteps.TheProjectGot("Ahr.Assurance.NUnit"))
            .VerifyAsync();

    [Fact]
    public Task FrameworkNamesOneInitCantSee() =>
        Scenario.Define<ToolUnderTest>("'dotnet assurance init --framework' adds the named adapter when the project shows no framework",
                CliBehaviour.Adapters, description: "The framework can come from a shared props file init doesn't read.")
            .Given(InitSteps.AProject())
            .When(InitSteps.InitRunsWith("tunit"))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(InitSteps.TheProjectGot("Ahr.Assurance.TUnit"))
            .VerifyAsync();

    [Fact]
    public Task FrameworkThatContradictsTheProjectStops() =>
        Scenario.Define<ToolUnderTest>("'dotnet assurance init --framework' stops when the project uses another framework", CliBehaviour.Adapters)
            .Given(InitSteps.AProject("NUnit/4.4.0"))
            .When(InitSteps.InitRunsWith("xunit-v3"))
            .Then(ReportSteps.TheCliExitsWith(1))
            .And(ReportSteps.TheCliSays("Checkout.Tests.csproj uses NUnit (nunit), not xUnit v3. Nothing changed."))
            .And(InitSteps.NothingChanged)
            .VerifyAsync();

    [Theory]
    [InlineData("jasmine", "'jasmine' is not a framework init knows. Use one of: xunit-v3, xunit-v2, nunit, mstest, mstest-v3, tunit, playwright.")]
    [InlineData("nunit,tunit", "Name one test framework, not NUnit (nunit), TUnit (tunit). Playwright can be added beside it.")]
    [InlineData("nunit tunit", "Name one test framework, not NUnit (nunit), TUnit (tunit). Playwright can be added beside it.")]
    public Task FrameworkMustBeOneItKnows(string frameworks, string message) =>
        Scenario.Define<ToolUnderTest>("'dotnet assurance init --framework' takes one test framework it knows, and Playwright", CliBehaviour.Adapters)
            .Given(InitSteps.AProject("NUnit/4.4.0"))
            .When(InitSteps.InitRunsWith(frameworks.Split(' ')))
            .Then(ReportSteps.TheCliExitsWith(1))
            .And(ReportSteps.TheCliSays(message))
            .And(InitSteps.NothingChanged)
            .VerifyAsync();

    [Fact]
    public Task AnAdapterAlreadyThereIsLeftAlone() =>
        Scenario.Define<ToolUnderTest>("'dotnet assurance init --framework' leaves an adapter already referenced, and only writes ahr-assurance.json", CliBehaviour.Adapters)
            .Given(InitSteps.AProject("NUnit/4.4.0", "Ahr.Assurance.NUnit/0.1.0"))
            .When(InitSteps.InitRunsWith("nunit"))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheCliSays("Checkout.Tests.csproj already references Ahr.Assurance.NUnit."))
            .And(InitSteps.NoPackageIsAdded)
            .And(InitSteps.TheConfigIsWritten)
            .VerifyAsync();

    [Fact]
    public Task AnAdapterAlreadyThereContradictsTheFramework() =>
        Scenario.Define<ToolUnderTest>("'dotnet assurance init --framework' stops when the project already references another adapter", CliBehaviour.Adapters)
            .Given(InitSteps.AProject("Ahr.Assurance.NUnit/0.1.0"))
            .When(InitSteps.InitRunsWith("tunit"))
            .Then(ReportSteps.TheCliExitsWith(1))
            .And(ReportSteps.TheCliSays("Checkout.Tests.csproj already references Ahr.Assurance.NUnit, not Ahr.Assurance.TUnit. Nothing changed."))
            .And(InitSteps.NothingChanged)
            .VerifyAsync();

    [Fact]
    public Task AFailedPackageWritesNothing() =>
        Scenario.Define<ToolUnderTest>("'dotnet assurance init --framework' says so and writes no ahr-assurance.json when the package can't be added", CliBehaviour.Adapters,
                description: "Offline, or behind a feed without the package.")
            .Given(InitSteps.AProject("xunit.v3/3.2.2"))
            .And("'dotnet add package' fails", tool => tool.Packages.Failure = "'dotnet add package Ahr.Assurance.Xunit.v3' failed with exit code 1.")
            .When(InitSteps.InitRunsWith("xunit-v3"))
            .Then(ReportSteps.TheCliExitsWith(1))
            .And(ReportSteps.TheCliSays("Assurance init failed: 'dotnet add package Ahr.Assurance.Xunit.v3' failed with exit code 1. No ahr-assurance.json was written " +
                $"for Checkout.Tests.csproj. Check the package source, or add Ahr.Assurance.Xunit.v3 {AssuranceVersion.Current} yourself and run init again."))
            .And(ReportSteps.TheCliSays(FakePackages.FailureDetail))
            .And(InitSteps.NoConfigIsWritten)
            .VerifyAsync();

    [Fact]
    public Task AFailedPlaywrightPackageWritesNothing() =>
        Scenario.Define<ToolUnderTest>("'dotnet assurance init --framework' writes no ahr-assurance.json when the adapter is added but Playwright's package can't be",
                CliBehaviour.Adapters)
            .Given(InitSteps.AProject("xunit.v3/3.2.2", "Microsoft.Playwright/1.55.0"))
            .And("'dotnet add package' fails for Ahr.Assurance.Playwright", tool =>
            {
                tool.Packages.FailingPackage = "Ahr.Assurance.Playwright";
                tool.Packages.Failure = "'dotnet add package Ahr.Assurance.Playwright' failed with exit code 1.";
            })
            .When(InitSteps.InitRunsWith("xunit-v3,playwright"))
            .Then(ReportSteps.TheCliExitsWith(1))
            .And(ReportSteps.TheCliSays($"Added Ahr.Assurance.Xunit.v3 {AssuranceVersion.Current} to Checkout.Tests.csproj"))
            .And(ReportSteps.TheCliSays("Assurance init failed: 'dotnet add package Ahr.Assurance.Playwright' failed with exit code 1. No ahr-assurance.json was written " +
                $"for Checkout.Tests.csproj. Check the package source, or add Ahr.Assurance.Playwright {AssuranceVersion.Current} yourself and run init again."))
            .And(InitSteps.NoConfigIsWritten)
            .VerifyAsync();

    [Fact]
    public Task InitSetsUpEachTestProjectInASolution() =>
        Scenario.Define<ToolUnderTest>("'dotnet assurance init' in a solution's folder writes ahr-assurance.json in each test project, and names each one's adapter", CliBehaviour.Setup)
            .Given(InitSteps.ASolution)
            .When(ToolSteps.InitRuns)
            .Then(ReportSteps.TheCliExitsWith(0))
            .And("Each test project has its own ahr-assurance.json, and App has none", tool => Assert.Equal([true, true, false],
                new[] { "Checkout.Tests", "Billing.Tests", "App" }.Select(project => File.Exists(tool.PathOf($"tests/{project}/{AssuranceConfig.FileName}")))))
            .And(ReportSteps.TheCliSays("Checkout.Tests.csproj uses xUnit v3. To add its adapter: dotnet assurance init --framework xunit-v3"))
            .And(ReportSteps.TheCliSays("Billing.Tests.csproj uses NUnit. To add its adapter: dotnet assurance init --framework nunit"))
            .And(ReportSteps.TheCliSays("Skipped App.csproj: no test framework."))
            .And(InitSteps.NoPackageIsAdded)
            .VerifyAsync();

    [Fact]
    public Task FrameworkInASolutionSetsUpItsProjects() =>
        Scenario.Define<ToolUnderTest>("'dotnet assurance init --framework' in a solution's folder sets up only the projects on that framework", CliBehaviour.Adapters)
            .Given(InitSteps.ASolution)
            .When(InitSteps.InitRunsWith("nunit"))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And("Only Billing.Tests gets an adapter", tool => Assert.Equal(["Billing.Tests.csproj"], tool.Packages.Added.Select(added => added.Project)))
            .And("Only Billing.Tests gets ahr-assurance.json", tool => Assert.Equal([false, true],
                new[] { "Checkout.Tests", "Billing.Tests" }.Select(project => File.Exists(tool.PathOf($"tests/{project}/{AssuranceConfig.FileName}")))))
            .And(ReportSteps.TheCliSays("Skipped Checkout.Tests.csproj: uses xUnit v3 (xunit-v3)."))
            .And(ReportSteps.TheCliSays("Skipped App.csproj: no test framework."))
            .VerifyAsync();

    [Fact]
    public Task ASolutionWithoutTestProjectsStops() =>
        Scenario.Define<ToolUnderTest>("'dotnet assurance init' in a solution's folder stops when no project uses a test framework it knows", CliBehaviour.Setup)
            .Given("All.slnx lists App, which references no test framework", tool =>
            {
                tool.ProjectFile("tests/App/App.csproj", "Microsoft.NET.Sdk");
                File.WriteAllText(tool.PathOf("All.slnx"), "<Solution><Project Path=\"tests/App/App.csproj\" /></Solution>");
            })
            .When(ToolSteps.InitRuns)
            .Then(ReportSteps.TheCliExitsWith(1))
            .And(ReportSteps.TheCliSays("references a test framework init knows"))
            .And("No ahr-assurance.json is written", tool => Assert.Empty(Directory.GetFiles(tool.Root, AssuranceConfig.FileName, SearchOption.AllDirectories)))
            .VerifyAsync();

    [Fact]
    public Task InitNeedsAProjectFolder() =>
        Scenario.Define<ToolUnderTest>("'dotnet assurance init' refuses a folder without a project or solution file", CliBehaviour.Setup,
                description: "A config in the wrong folder would be found by every project below it.")
            .Given("A folder without a project file", _ => { })
            .When(ToolSteps.InitRuns)
            .Then(ReportSteps.TheCliExitsWith(1))
            .And(ReportSteps.TheCliSays("has no project file"))
            .And("No ahr-assurance.json is written", tool => Assert.False(File.Exists(tool.PathOf(AssuranceConfig.FileName))))
            .VerifyAsync();

    [Fact]
    public Task InitCanBeForced() =>
        Scenario.Define<ToolUnderTest>("'dotnet assurance init --force' writes ahr-assurance.json in any folder", CliBehaviour.Setup)
            .Given("A folder without a project file", _ => { })
            .When("'dotnet assurance init --force' runs in the folder", tool => tool.Init(".", force: true))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And("ahr-assurance.json holds the template", tool => Assert.Equal(AssuranceConfig.Template, tool.Read(AssuranceConfig.FileName)))
            .VerifyAsync();

    [Fact]
    public Task InitLeavesAnExistingFile() =>
        Scenario.Define<ToolUnderTest>("'dotnet assurance init' leaves an existing ahr-assurance.json and adapter as they are", CliBehaviour.Setup)
            .Given(InitSteps.AProject("NUnit/4.4.0", "Ahr.Assurance.NUnit/0.1.0"))
            .And("Its ahr-assurance.json holds { \"runs\": \"mine\" }", tool => File.WriteAllText(tool.PathOf(AssuranceConfig.FileName), """{ "runs": "mine" }"""))
            .When(ToolSteps.InitRuns)
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(ReportSteps.TheCliSays("already exists. Nothing changed."))
            .And(ToolSteps.TheConfigHolds("""{ "runs": "mine" }"""))
            .And(InitSteps.NoPackageIsAdded)
            .VerifyAsync();

    [Fact]
    public Task FrameworkAddsTheAdapterBesideAnExistingFile() =>
        Scenario.Define<ToolUnderTest>("'dotnet assurance init --framework' adds a missing adapter and keeps the existing ahr-assurance.json", CliBehaviour.Adapters)
            .Given(InitSteps.AProject("TUnit/0.90.0"))
            .And("Its ahr-assurance.json holds { \"runs\": \"mine\" }", tool => File.WriteAllText(tool.PathOf(AssuranceConfig.FileName), """{ "runs": "mine" }"""))
            .When(InitSteps.InitRunsWith("tunit"))
            .Then(ReportSteps.TheCliExitsWith(0))
            .And(InitSteps.TheProjectGot("Ahr.Assurance.TUnit"))
            .And(ToolSteps.TheConfigHolds("""{ "runs": "mine" }"""))
            .VerifyAsync();

    [Fact]
    public Task InitReportsAnInvalidFile() =>
        Scenario.Define<ToolUnderTest>("'dotnet assurance init --framework' says when an existing ahr-assurance.json isn't valid, and changes nothing", CliBehaviour.Setup)
            .Given(InitSteps.AProject("TUnit/0.90.0"))
            .And("Its ahr-assurance.json holds {", tool => File.WriteAllText(tool.PathOf(AssuranceConfig.FileName), "{"))
            .When(InitSteps.InitRunsWith("tunit"))
            .Then(ReportSteps.TheCliExitsWith(1))
            .And(ReportSteps.TheCliSays("is not valid"))
            .And(ToolSteps.TheConfigHolds("{"))
            .And(InitSteps.NoPackageIsAdded)
            .VerifyAsync();

    private static string[] Packages(string package) => package.Length == 0 ? [] : [package];

    private static string Shown(string package) => package.Length == 0 ? "no packages" : package;
}

public sealed class InitSteps : ScenarioSteps<ToolUnderTest>
{
    public static ScenarioAction<ToolUnderTest> AProject(params string[] packages) =>
        CreateActionStep($"Checkout.Tests.csproj referencing {CommandText.ListOrNone(packages)}",
            tool => tool.ProjectFile("Checkout.Tests.csproj", "Microsoft.NET.Sdk", packages));

    public static ScenarioAction<ToolUnderTest> ASolution { get; } =
        CreateActionStep("All.slnx lists Checkout.Tests on xUnit v3, Billing.Tests on NUnit, and App, which has no test framework", tool =>
        {
            tool.ProjectFile("tests/Checkout.Tests/Checkout.Tests.csproj", "Microsoft.NET.Sdk", "xunit.v3/3.2.2");
            tool.ProjectFile("tests/Billing.Tests/Billing.Tests.csproj", "Microsoft.NET.Sdk", "NUnit/4.4.0");
            tool.ProjectFile("tests/App/App.csproj", "Microsoft.NET.Sdk");
            File.WriteAllText(tool.PathOf("All.slnx"), "<Solution>" + string.Concat(new[] { "Checkout.Tests", "Billing.Tests", "App" }
                .Select(project => $"<Project Path=\"tests/{project}/{project}.csproj\" />")) + "</Solution>");
        });

    public static ScenarioAction<ToolUnderTest> InitRunsWith(params string[] frameworks) =>
        CreateActionStep($"'dotnet assurance init{string.Concat(frameworks.Select(framework => " --framework " + framework))}' runs in the folder",
            tool => tool.Init(".", false, frameworks));

    public static ScenarioExpectation<ToolUnderTest> TheProjectGot(params string[] adapters) =>
        CreateAssertionStep($"{string.Join(" and ", adapters)} added to the project, and nothing else", tool =>
            Assert.Equal(adapters, tool.Packages.Added.Select(added => added.Package)));

    public static ScenarioExpectation<ToolUnderTest> TheConfigIsWritten { get; } =
        CreateAssertionStep("ahr-assurance.json holds the template", tool => Assert.Equal(AssuranceConfig.Template, tool.Read(AssuranceConfig.FileName)));

    public static ScenarioExpectation<ToolUnderTest> EachPackageIsAtTheCliVersion { get; } =
        CreateAssertionStep("Each package is at the CLI's own version", tool => Assert.All(tool.Packages.Added, added => Assert.Equal(AssuranceVersion.Current, added.Version)));

    public static ScenarioExpectation<ToolUnderTest> NoConfigIsWritten { get; } =
        CreateAssertionStep("No ahr-assurance.json is written", tool => Assert.False(File.Exists(tool.PathOf(AssuranceConfig.FileName))));

    public static ScenarioExpectation<ToolUnderTest> NoPackageIsAdded { get; } =
        CreateAssertionStep("No package is added", tool => Assert.Empty(tool.Packages.Added));

    public static ScenarioExpectation<ToolUnderTest> NothingChanged { get; } =
        CreateAssertionStep("No package is added and no ahr-assurance.json is written", tool =>
        {
            Assert.Empty(tool.Packages.Added);
            Assert.False(File.Exists(tool.PathOf(AssuranceConfig.FileName)));
        });
}
