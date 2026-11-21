#!/usr/bin/env bash
# Packs the libraries and tool, then runs copies of the samples against the packages, outside the repository.
# Usage: scripts/verify-package.sh [work-directory]
set -euo pipefail

REPO="$(cd "$(dirname "$0")/.." && pwd -P)"
# A folder of your own is kept. A temporary one is removed when every check passes, and kept to inspect when one fails.
KEEP_WORK="${1:+yes}"
WORK="$(work="${1:-$(mktemp -d)}" && mkdir -p "$work" && cd "$work" && pwd -P)"
VERSION="$(sed -n 's:.*<AhrAssuranceVersion [^>]*>\(.*\)</AhrAssuranceVersion>.*:\1:p' "$REPO/Directory.Build.props")"
central_version() {
  sed -n "s:.*Include=\"$1\" Version=\"\([^\"]*\)\".*:\1:p" "$REPO/Directory.Packages.props"
}
FEED="$WORK/feed"
BASE="$WORK/base"
PACKAGES=(Ahr.Assurance Ahr.Assurance.Xunit.v3 Ahr.Assurance.Xunit.v2 Ahr.Assurance.NUnit Ahr.Assurance.MSTest Ahr.Assurance.MSTest.v3 Ahr.Assurance.TUnit Ahr.Assurance.Playwright Ahr.Assurance.Cli)
FRAMEWORKS=(Xunit.v3 Xunit.v2 NUnit MSTest MSTest.v3 TUnit)
export DOTNET_NOLOGO=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
PASSED=0
FAILED=0

check() {
  if [ "$2" = "$3" ]; then
    echo "  ok    $1"
    PASSED=$((PASSED + 1))
  else
    echo "  FAIL  $1 (expected '$3', got '$2')"
    FAILED=$((FAILED + 1))
  fi
}

check_contains() {
  if grep -qF -- "$3" "$2"; then
    echo "  ok    $1"
    PASSED=$((PASSED + 1))
  else
    echo "  FAIL  $1 (missing '$3' in $2)"
    FAILED=$((FAILED + 1))
  fi
}

check_excludes() {
  if grep -qF -- "$3" "$2"; then
    echo "  FAIL  $1 (found '$3' in $2)"
    FAILED=$((FAILED + 1))
  else
    echo "  ok    $1"
    PASSED=$((PASSED + 1))
  fi
}

run_status() {
  set +e
  "$@" > "$WORK/last-output.txt" 2>&1
  local status=$?
  set -e
  echo "$status"
}

count() {
  find "$@" | wc -l | tr -d ' '
}

exists() {
  if [ -e "$1" ]; then echo yes; else echo no; fi
}

# A copy of the samples in its own folder, set up as a repository of your own would be.
copy_samples() {
  rm -rf "$1"
  mkdir -p "$1"
  (cd "$REPO" && git ls-files -co --exclude-standard samples | grep -v 'packages.lock.json$' | tar -cf - -T -) | tar -C "$1" -xf -
  cp -R "$BASE/." "$1/"
}

sample() {
  echo "samples/Ahr.Assurance.Sample.$1"
}

# Runs a sample's tests: xUnit v2 on VSTest, the others on Microsoft.Testing.Platform.
test_sample() {
  local name="$1"
  shift
  if [ "$name" = Xunit.v2 ]; then
    run_status dotnet vstest "$(sample Xunit.v2)/bin/Release/net10.0/Ahr.Assurance.Sample.Xunit.v2.dll" "$@"
    return
  fi
  run_status dotnet test --project "$(sample "$name")" -c Release --no-build "$@"
}

# Each run's scenarios, steps, verdicts, expected and actual values, and attachment counts, one line each, sorted.
evidence_lines() {
  python3 -I -c '
import json, pathlib, sys
lines = []
for path in pathlib.Path(sys.argv[1]).glob("*/*.json"):
    document = json.loads(path.read_text())
    for scenario in document["evidence"]["scenarios"]:
        steps = [(step["name"], step["verdict"], step.get("expected"), step.get("actual")) for step in scenario["steps"]]
        lines.append(repr((scenario["name"], steps, len(document["attachments"]))))
print("\n".join(sorted(lines)))' "$1"
}

echo "Version $VERSION, working in $WORK"
rm -rf "$FEED" "$BASE"
mkdir -p "$FEED" "$BASE"

echo "Packing"
dotnet pack "$REPO/Ahr.Assurance.slnx" -c Release -o "$FEED" -nr:false -p:UseSharedCompilation=false -v q > "$WORK/pack.txt" 2>&1
for package in "${PACKAGES[@]}"; do
  check "package $package exists" "$(exists "$FEED/$package.$VERSION.nupkg")" yes
done
check "only expected packages" "$(count "$FEED" -name '*.nupkg')" "${#PACKAGES[@]}"
for package in "${PACKAGES[@]}"; do
  NUSPEC="$(unzip -p "$FEED/$package.$VERSION.nupkg" '*.nuspec')"
  check "$package has a readme" "$(unzip -l "$FEED/$package.$VERSION.nupkg" | grep -c ' README.md$')" 1
  check "$package has the icon" "$(unzip -l "$FEED/$package.$VERSION.nupkg" | grep -c ' icon.png$')" 1
  check "$package names its repository and commit" "$(echo "$NUSPEC" | grep -c "commit=\"$(git -C "$REPO" rev-parse HEAD)\"")" 1
  check "$package has symbols" "$(exists "$FEED/$package.$VERSION.snupkg")" yes
done
CORE_NUSPEC="$(unzip -p "$FEED/Ahr.Assurance.$VERSION.nupkg" '*.nuspec')"
FOUNDATION_VERSION="$(sed -n 's:.*<AhrFoundationVersion[^>]*>\([^<]*\)<.*:\1:p' "$REPO/Directory.Packages.props")"
check "Ahr.Assurance depends on Ahr.Foundation" "$(echo "$CORE_NUSPEC" | grep -c "id=\"Ahr.Foundation\" version=\"$FOUNDATION_VERSION\"")" 1
check "Ahr.Foundation's analyzers stay out of consumers" "$(echo "$CORE_NUSPEC" | grep 'id="Ahr.Foundation"' | grep -c 'exclude="[^"]*Analyzers')" 1
check "the core package carries the analyzers" "$(unzip -l "$FEED/Ahr.Assurance.$VERSION.nupkg" | grep -c ' analyzers/dotnet/cs/Ahr.Assurance.Analyzers.dll$')" 1

# The repository packs from its own restore. The consumer copies below restore into their own package and tool folders.
export NUGET_PACKAGES="$WORK/packages"
export DOTNET_CLI_HOME="$WORK/cli-home"

cat > "$BASE/nuget.config" <<XML
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$FEED" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
XML
cat > "$BASE/global.json" <<JSON
{ "test": { "runner": "Microsoft.Testing.Platform" } }
JSON
cat > "$BASE/Directory.Build.props" <<XML
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
XML
{
  echo '<Project>'
  echo '  <PropertyGroup>'
  echo '    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>'
  echo '  </PropertyGroup>'
  echo '  <ItemGroup>'
  for package in xunit.v3 Microsoft.Testing.Extensions.TrxReport NUnit NUnit3TestAdapter MSTest TUnit Microsoft.Playwright; do
    echo "    <PackageVersion Include=\"$package\" Version=\"$(central_version "$package")\" />"
  done
  for package in "${PACKAGES[@]}"; do
    echo "    <PackageVersion Include=\"$package\" Version=\"$VERSION\" />"
  done
  echo '  </ItemGroup>'
  echo '</Project>'
} > "$BASE/Directory.Packages.props"
(cd "$BASE" && dotnet new tool-manifest > /dev/null)

echo "Installing the tool"
SAMPLES="$WORK/samples"
copy_samples "$SAMPLES"
cd "$SAMPLES"
check "tool installs" "$(run_status dotnet tool install Ahr.Assurance.Cli --version "$VERSION" --local)" 0
check "tool is this build" "$(dotnet assurance --version 2>&1 | grep -c "$(git -C "$REPO" rev-parse HEAD)")" 1
if [ -d .config ]; then cp -R .config "$BASE/"; else cp dotnet-tools.json "$BASE/"; fi

echo "Every sample, from the packages"
for framework in "${FRAMEWORKS[@]}" Playwright; do
  project="$(sample "$framework")"
  check "the $framework sample builds from the packages" "$(run_status dotnet build "$project" -c Release)" 0
  check "the $framework sample uses the packages" "$(grep -q "\"Ahr.Assurance.$framework/$VERSION\"" "$project/obj/project.assets.json" && echo yes || echo no)" yes
  check "the $framework sample passes" "$(test_sample "$framework")" 0
  check "the $framework sample's report" "$(run_status dotnet assurance report "$project")" 0
done
check "the analyzers come from the package" "$(dotnet msbuild "$(sample Xunit.v3)" -t:ResolveLockFileAnalyzers -getItem:Analyzer | grep '"Identity"' | grep -c "ahr.assurance/$VERSION/analyzers/dotnet/cs/Ahr.Assurance.Analyzers.dll")" 1
evidence_lines "$(sample Xunit.v3)/artifacts/assurance" > "$WORK/xunit-evidence.txt"
check "the xUnit sample records six scenarios" "$(wc -l < "$WORK/xunit-evidence.txt" | tr -d ' ')" 6
for framework in Xunit.v2 NUnit MSTest MSTest.v3 TUnit; do
  check "the $framework sample records what the xUnit sample records" "$(evidence_lines "$(sample "$framework")/artifacts/assurance" | cmp -s - "$WORK/xunit-evidence.txt" && echo same || echo different)" same
done
check_contains "a shared check shows what was expected" "$WORK/xunit-evidence.txt" "Content = Offline-ready field handbook"
for framework in Xunit.v2 MSTest.v3; do
  check_contains "the $framework sample names the test" "$(ls "$(sample "$framework")"/artifacts/assurance/*/*.json | head -1)" 'OfflineDocumentTests.'
done
for framework in "${FRAMEWORKS[@]}"; do
  check_contains "the $framework sample's rows are one scenario with examples" "$(ls -d "$(sample "$framework")"/artifacts/assurance/*/report | head -1)/report.html" "2 of 2 examples passed"
done
check "the Playwright sample has a screenshot after each step" "$(count "$(sample Playwright)/artifacts/assurance" -mindepth 3 -maxdepth 3 -path '*/attachments/*.png')" 10
sed -i.bak 's|"Offline-ready field handbook", await|"A different handbook", await|' "$(sample Playwright)/Tests/DocumentPageTests.cs"
check "a failed Playwright expectation fails the test" "$(run_status dotnet test --project "$(sample Playwright)" -c Release)" 2
check_contains "a failed Playwright expectation is a failed check" "$WORK/last-output.txt" "The handbook is on the page: Unsatisfied."
mv "$(sample Playwright)/Tests/DocumentPageTests.cs.bak" "$(sample Playwright)/Tests/DocumentPageTests.cs"

echo "Global usings"
check "tests need no Assurance using lines" "$(cat "$SAMPLES"/samples/Ahr.Assurance.Sample.{Xunit.v3,Xunit.v2,NUnit,MSTest,MSTest.v3,TUnit}/Tests/*.cs | grep -c '^using Ahr\.Assurance\.\(Xunit\|NUnit\|MSTest\|TUnit\|Execution\|Core\|Reporting\);')" 0
check "opting out removes the usings" "$(run_status dotnet build "$(sample Xunit.v3)" -c Release -p:AhrAssuranceImplicitUsings=false)" 1
check_contains "opting out leaves the Assurance types unknown" "$WORK/last-output.txt" "could not be found"
check "no usings without implicit usings" "$(run_status dotnet build "$(sample Xunit.v3)" -c Release -p:ImplicitUsings=disable)" 1
check "builds again with the usings" "$(run_status dotnet build "$(sample Xunit.v3)" -c Release)" 0

echo "Reporting evidence without a config file"
copy_samples "$WORK/without-config"
cd "$WORK/without-config/$(sample Xunit.v3)"
rm ahr-assurance.json
EVIDENCE="bin/Release/net10.0/artifacts/assurance"
check "the sample builds" "$(run_status dotnet build -c Release)" 0
check "tests pass" "$(ASSURANCE_RUN_ID=pass-run run_status dotnet test -c Release --no-build)" 0
check "evidence written" "$(count "$EVIDENCE" -maxdepth 1 -name '*.json')" 6
check "the test run writes no report unless asked" "$(exists "$EVIDENCE/report")" no
check "the test run wrote its summary" "$(count "$EVIDENCE/summary" -name 'tests-*.json')" 1
check "the log is written to attachments" "$(count "$EVIDENCE/attachments" -name '*.log')" 1
check "the screen file is not copied by the test run" "$(count "$EVIDENCE/attachments" -name '*.png')" 0
check "report exit code" "$(run_status dotnet assurance report --input "$EVIDENCE" --output out/pass --title 'Release 0.1')" 0
check "report plus two attachments" "$(count out/pass -type f)" 3
check "screen copied beside report" "$(count out/pass/attachments -name '*.png')" 1
check "log copied beside report" "$(cat out/pass/attachments/*.log)" "Opened from the local cache."
check_contains "title" out/pass/report.html "<h1>Release 0.1</h1>"
check_contains "headline" out/pass/report.html "All 9 risks, rules and outcomes are passing."
check "image linked inside its step" "$(python3 -I -c "import re,sys; h=open('out/pass/report.html').read(); print(any('<img src=\"attachments/' in x for x in re.findall(r'<li>.*?</li>', h, re.S)))")" True
check_excludes "nothing embedded" out/pass/report.html "data:"
cp -R "$EVIDENCE" moved-results
check "moved results exit code" "$(run_status dotnet assurance report --input moved-results --output out/moved)" 0
check "moved results log" "$(cat out/moved/attachments/*.log)" "Opened from the local cache."
check_contains "a file left behind is not available" out/moved/report.html "Document screen: is not available."
mkdir -p split/evidence
cp "$EVIDENCE"/*.json split/evidence/
cp -R "$EVIDENCE/attachments" split/uploaded
check "separate attachments exit code" "$(run_status dotnet assurance report --input split/evidence --attachments split/uploaded --output out/split)" 0
check "separate attachments log" "$(count out/split/attachments -name '*.log')" 1
check_excludes "no correlation ID" out/pass/report.html "pass-run"
check_excludes "no test names" out/pass/report.html "OfflineDocumentTests"
check "context exit code" "$(run_status dotnet assurance report --input "$EVIDENCE" --output out/context --context offline-document-access)" 0
check_contains "context title" out/context/report.html "<h1>Offline document access</h1>"
check "unknown context exit code" "$(run_status dotnet assurance report --input "$EVIDENCE" --output out/unknown --context nope)" 1

echo "A failing run"
READER="../Ahr.Assurance.Sample.Core/Application/DocumentReader.cs"
cp "$READER" "$WORK/DocumentReader.cs"
sed -i.bak '/cache.SaveAsync(downloaded/d' "$READER"
check "a reader that stops saving downloads fails the run" "$(ASSURANCE_RUN_ID=fail-run run_status dotnet test -c Release)" 2
check "several runs need --run" "$(run_status dotnet assurance report --input "$EVIDENCE" --output out/ambiguous)" 1
check "failure exit code" "$(run_status dotnet assurance report --input "$EVIDENCE" --run fail-run --output out/fail)" 2
check_contains "failure headline" out/fail/report.html "4 of 9 risks, rules and outcomes are not passing."
check_excludes "no raw assertion text" out/fail/report.html "Assert.Equal"
check_excludes "no raw failure message" out/fail/report.html "Values differ"
check_excludes "no raw actual label" out/fail/report.html "Actual:"
check "filtered failure keeps exit code" "$(run_status dotnet assurance report --input "$EVIDENCE" --run fail-run --output out/fail-context --context offline-document-access)" 2
check "developer exit code" "$(run_status dotnet assurance report --input "$EVIDENCE" --run fail-run --output out/developer --developer)" 2
check_contains "developer section" out/developer/report.html "Developer details"
check_contains "developer shows failure detail" out/developer/report.html "Values differ"
check_contains "developer shows actual value" out/developer/report.html "Actual:   2"
check "developer bundles" "$(exists out/developer/verifications)" yes
check "developer pages reuse the report attachments" "$(count out/developer -name '*.png')" 1
check "developer page links the report attachments" "$(grep -l 'src="../../attachments/' out/developer/verifications/*/*.html | wc -l | tr -d ' ')" 1
cp "$WORK/DocumentReader.cs" "$READER"

echo "Config file on a fresh project"
copy_samples "$WORK/fresh"
cd "$WORK/fresh/$(sample Xunit.v3)"
rm ahr-assurance.json
check "the sample builds" "$(run_status dotnet build -c Release)" 0
check "report without input or config fails" "$(run_status dotnet assurance report)" 1
check_contains "report says how to select evidence" "$WORK/last-output.txt" "dotnet assurance init"
check "init exit code" "$(run_status dotnet assurance init)" 0
check "config created" "$(exists ahr-assurance.json)" yes
CONFIG_HASH="$(shasum ahr-assurance.json)"
check "second init exit code" "$(run_status dotnet assurance init)" 0
check "second init changes nothing" "$(shasum ahr-assurance.json)" "$CONFIG_HASH"
check "init outside a project fails" "$(mkdir -p empty && cd empty && run_status dotnet assurance init)" 1
check "fresh tests pass" "$(run_status dotnet test -c Release)" 0
check "one run folder" "$(count artifacts/assurance -mindepth 1 -maxdepth 1 -type d)" 1
FIRST_RUN="$(basename "$(find artifacts/assurance -mindepth 1 -maxdepth 1 -type d)")"
check "evidence inside the run folder" "$(count "artifacts/assurance/$FIRST_RUN" -maxdepth 1 -name '*.json')" 6
check "log inside the run folder" "$(count "artifacts/assurance/$FIRST_RUN/attachments" -name '*.log')" 1
check "the test run writes no report unless asked" "$(exists "artifacts/assurance/$FIRST_RUN/report")" no
check "nothing written under bin" "$(exists bin/Release/net10.0/artifacts/assurance)" no
check "second fresh run passes" "$(run_status dotnet test -c Release --no-build)" 0
check "two run folders" "$(count artifacts/assurance -mindepth 1 -maxdepth 1 -type d)" 2
SECOND_RUN="$(find artifacts/assurance -mindepth 1 -maxdepth 1 -type d ! -name "$FIRST_RUN" -exec basename {} \;)"
check "report with no flags" "$(run_status dotnet assurance report)" 0
check_contains "report names the config" "$WORK/last-output.txt" "Config: $(pwd -P)/ahr-assurance.json"
check_contains "report picks the newest run" "$WORK/last-output.txt" "Selected run: $SECOND_RUN"
check_contains "report says which run it chose" "$WORK/last-output.txt" "newest of 2 runs"
check_contains "report counts every test" "$WORK/last-output.txt" "Tests: 6 tests ran: 6 passed, 0 failed, 0 skipped. 6 recorded scenario evidence."
check "report inside the run folder" "$(exists "artifacts/assurance/$SECOND_RUN/report/report.html")" yes
check "report image" "$(count "artifacts/assurance/$SECOND_RUN/report/attachments" -name '*.png')" 1
check "second report replaces the first" "$(run_status dotnet assurance report)" 0
check "earlier run by --run" "$(run_status dotnet assurance report --run "$FIRST_RUN")" 0
check "earlier run report" "$(exists "artifacts/assurance/$FIRST_RUN/report/report.html")" yes
check "report from a child folder" "$(mkdir -p tests-folder && cd tests-folder && run_status dotnet assurance report)" 0
check "--output still goes anywhere" "$(run_status dotnet assurance report --output "$WORK/fresh-out")" 0
check "--output report" "$(exists "$WORK/fresh-out/report.html")" yes
check "named run" "$(ASSURANCE_RUN_ID=release-4.2 run_status dotnet test -c Release --no-build)" 0
check "named run folder" "$(exists artifacts/assurance/release-4.2)" yes
sed -i.bak 's|// "keep": 20,|"keep": 2,|' ahr-assurance.json
check "run with keep" "$(run_status dotnet test -c Release --no-build)" 0
check "keep leaves two runs" "$(count artifacts/assurance -mindepth 1 -maxdepth 1 -type d)" 2
check "keep removed the oldest" "$(test -d "artifacts/assurance/$FIRST_RUN" && echo kept || echo removed)" removed
check "a run with the report on passes" "$(ASSURANCE_RUN_ID=report-on run_status dotnet test -c Release --no-build --assurance-report on)" 0
check_contains "the test run lists its report" "$WORK/last-output.txt" "$(pwd -P)/artifacts/assurance/report-on/report/report.html"
check "the report on writes its report" "$(exists artifacts/assurance/report-on/report/report.html)" yes
check_contains "the report counts every test" artifacts/assurance/report-on/report/report.html "6 tests ran: 6 passed, 0 failed, 0 skipped. 6 recorded scenario evidence."
python3 -I -c "p='ahr-assurance.json'; s=open(p).read(); open(p, 'w').write(s.replace('// \"version\": \"4.2\",', '\"version\": \"4.2\",'))"
check "a run with a version passes" "$(ASSURANCE_RUN_ID=versioned run_status dotnet test -c Release --no-build --assurance-report on)" 0
check_contains "the run's report carries the version after the project's name" artifacts/assurance/versioned/report/report.html '<p class="kind">Delivery report</p><h1>Ahr.Assurance.Sample.Xunit.v3 4.2</h1>'

echo "Solution with several test projects"
SOLUTION="$WORK/solution"
copy_samples "$SOLUTION"
cd "$SOLUTION"
rm samples/*/ahr-assurance.json
SOLVED=(Xunit.v3 NUnit MSTest TUnit)
{
  echo '<Solution>'
  for framework in Core MSTest MSTest.v3 NUnit TUnit Xunit.v3; do
    echo "  <Project Path=\"$(sample "$framework")/Ahr.Assurance.Sample.$framework.csproj\" />"
  done
  echo '</Solution>'
} > All.slnx
check "solution with no configured project fails" "$(run_status dotnet assurance report)" 1
check_contains "solution says how to add a project" "$WORK/last-output.txt" "dotnet assurance init"
for framework in "${SOLVED[@]}"; do
  check "init in the $framework sample" "$(cd "$(sample "$framework")" && run_status dotnet assurance init)" 0
done
check "solution report before tests fails" "$(run_status dotnet assurance report)" 1
check_contains "solution says run the tests" "$WORK/last-output.txt" "Run the tests first."
check "solution tests pass" "$(run_status dotnet test -c Release)" 0
for framework in "${SOLVED[@]}"; do
  check "the $framework sample's run folder" "$(count "$(sample "$framework")/artifacts/assurance" -mindepth 1 -maxdepth 1 -type d)" 1
done
check "no project report unless asked" "$(count samples -path '*/artifacts/assurance/*/report/report.html')" 0
check "solution report" "$(run_status dotnet assurance report)" 0
check_contains "solution named" "$WORK/last-output.txt" "Solution: $(pwd -P)/All.slnx"
for framework in "${SOLVED[@]}"; do
  check_contains "the $framework sample's run named" "$WORK/last-output.txt" "Project Ahr.Assurance.Sample.$framework: $(pwd -P)/$(sample "$framework")/artifacts/assurance/"
  check_contains "the $framework sample has its own section" artifacts/assurance/report/report.html "<h2>Ahr.Assurance.Sample.$framework</h2>"
done
check_excludes "project without a config left out" "$WORK/last-output.txt" "Ahr.Assurance.Sample.MSTest.v3"
check "solution report beside the solution" "$(exists artifacts/assurance/report/report.html)" yes
check "solution report has every project's journeys" "$(grep -o 'A connected reader downloads and saves a document' artifacts/assurance/report/report.html | wc -l | tr -d ' ')" 4
check "rows are one scenario with examples in every project" "$(grep -o '2 of 2 examples passed' artifacts/assurance/report/report.html | wc -l | tr -d ' ')" 4
check "solution report images" "$(count artifacts/assurance/report/attachments -name '*.png')" 4
check_contains "solution report titled by the solution" artifacts/assurance/report/report.html "<h1>All</h1>"
check_contains "solution report generated time" artifacts/assurance/report/report.html "4 projects. Report generated "
check_contains "one section per project" artifacts/assurance/report/report.html '<div class="project" id="project-0"><h2>Ahr.Assurance.Sample.MSTest</h2><p class="meta">Tested '
check "second solution report replaces the first" "$(run_status dotnet assurance report)" 0

echo "Report formats and single projects"
check "unknown format fails" "$(run_status dotnet assurance report --format nope)" 1
check_contains "unknown format names the ones available" "$WORK/last-output.txt" "This CLI writes html."
check "one project by --project" "$(run_status dotnet assurance report --project Ahr.Assurance.Sample.NUnit --output "$WORK/solution-nunit")" 0
check "one project's journeys" "$(grep -o 'A connected reader downloads and saves a document' "$WORK/solution-nunit/report.html" | wc -l | tr -d ' ')" 1
check "unknown --project fails" "$(run_status dotnet assurance report --project Ahr.Assurance.Sample.MSTest.v3)" 1
check_contains "unknown --project lists the projects" "$WORK/last-output.txt" "Available: Ahr.Assurance.Sample.MSTest, Ahr.Assurance.Sample.NUnit, Ahr.Assurance.Sample.TUnit, Ahr.Assurance.Sample.Xunit.v3."
check "report in a project folder covers that project" "$(cd "$(sample Xunit.v3)" && run_status dotnet assurance report)" 0
check_contains "project folder report names its run folder" "$WORK/last-output.txt" "Run folder: $(pwd -P)/$(sample Xunit.v3)/artifacts/assurance/"
check "shared run ID" "$(ASSURANCE_RUN_ID=ci-9 run_status dotnet test -c Release --no-build)" 0
check "solution report by --run" "$(run_status dotnet assurance report --run ci-9)" 0
check_contains "--run picks it in each project" "$WORK/last-output.txt" "Project Ahr.Assurance.Sample.NUnit: $(pwd -P)/$(sample NUnit)/artifacts/assurance/ci-9"
rm -rf "$(sample NUnit)/artifacts"
check "project with no runs is left out" "$(run_status dotnet assurance report)" 0
check_contains "project with no runs named in the output" "$WORK/last-output.txt" "Project Ahr.Assurance.Sample.NUnit: no runs in"
check_excludes "project with no runs not in the report" artifacts/assurance/report/report.html "Ahr.Assurance.Sample.NUnit"

echo "Init adds the adapter"
# The name init --framework takes for each adapter.
framework_name() {
  case "$1" in
    Xunit.v3) echo xunit-v3 ;;
    Xunit.v2) echo xunit-v2 ;;
    NUnit) echo nunit ;;
    MSTest) echo mstest ;;
    MSTest.v3) echo mstest-v3 ;;
    TUnit) echo tunit ;;
  esac
}
# Copies of the samples without their Assurance packages or config, and without those packages' central versions, as a project starts.
without_adapters() {
  copy_samples "$1"
  sed -i.bak -E '/Include="Ahr\.Assurance\.(Xunit|NUnit|MSTest|TUnit|Playwright)/d' "$1/Directory.Packages.props"
  for framework in "${FRAMEWORKS[@]}" Playwright; do
    sed -i.bak -E '/Include="Ahr\.Assurance\.(Xunit|NUnit|MSTest|TUnit|Playwright)/d' "$1/$(sample "$framework")/Ahr.Assurance.Sample.$framework.csproj"
    rm -f "$1/$(sample "$framework")/ahr-assurance.json"
  done
}
without_adapters "$WORK/init"
cd "$WORK/init"
PROJECT_HASH="$(shasum "$(sample NUnit)/Ahr.Assurance.Sample.NUnit.csproj")"
check "init without --framework" "$(cd "$(sample NUnit)" && run_status dotnet assurance init)" 0
check_contains "init names the command for the adapter" "$WORK/last-output.txt" "uses NUnit. To add its adapter: dotnet assurance init --framework nunit"
check "init without --framework adds no package" "$(shasum "$(sample NUnit)/Ahr.Assurance.Sample.NUnit.csproj")" "$PROJECT_HASH"
check "init without --framework writes the config" "$(exists "$(sample NUnit)/ahr-assurance.json")" yes
for framework in "${FRAMEWORKS[@]}"; do
  project="$(sample "$framework")"
  name="$(framework_name "$framework")"
  check "the $framework sample doesn't build without its adapter" "$(run_status dotnet build "$project" -c Release)" 1
  check "init --framework $name in the $framework sample" "$(cd "$project" && run_status dotnet assurance init --framework "$name")" 0
  check_contains "init names the $framework adapter it added" "$WORK/last-output.txt" "Added Ahr.Assurance.$framework $VERSION to Ahr.Assurance.Sample.$framework.csproj"
  check "the $framework sample has its config" "$(exists "$project/ahr-assurance.json")" yes
  check_contains "the $framework adapter's version is central" Directory.Packages.props "Include=\"Ahr.Assurance.$framework\" Version=\"$VERSION\""
  check "the $framework sample builds with the adapter init added" "$(run_status dotnet build "$project" -c Release)" 0
  check "the $framework sample uses the adapter package" "$(grep -q "\"Ahr.Assurance.$framework/$VERSION\"" "$project/obj/project.assets.json" && echo yes || echo no)" yes
  check "the $framework sample passes after init" "$(test_sample "$framework")" 0
done
check "init without --framework on the Playwright sample" "$(cd "$(sample Playwright)" && run_status dotnet assurance init)" 0
check_contains "init names Playwright in the command" "$WORK/last-output.txt" "dotnet assurance init --framework xunit-v3,playwright"
check "init --framework xunit-v3,playwright" "$(cd "$(sample Playwright)" && run_status dotnet assurance init --framework xunit-v3,playwright)" 0
check_contains "init added Playwright" "$WORK/last-output.txt" "Added Ahr.Assurance.Playwright $VERSION to Ahr.Assurance.Sample.Playwright.csproj"
check "the Playwright sample builds with the packages init added" "$(run_status dotnet build "$(sample Playwright)" -c Release)" 0
check "the Playwright sample passes after init" "$(test_sample Playwright)" 0
check "the Playwright sample has a screenshot after each step, after init" "$(count "$(sample Playwright)/artifacts/assurance" -mindepth 3 -maxdepth 3 -path '*/attachments/*.png')" 10
PROJECT_HASH="$(shasum "$(sample NUnit)/Ahr.Assurance.Sample.NUnit.csproj")"
check "init --framework again" "$(cd "$(sample NUnit)" && run_status dotnet assurance init --framework nunit)" 0
check_contains "init again leaves the adapter" "$WORK/last-output.txt" "already references Ahr.Assurance.NUnit."
check "init again changes nothing" "$(shasum "$(sample NUnit)/Ahr.Assurance.Sample.NUnit.csproj")" "$PROJECT_HASH"
check "a framework that contradicts the project fails" "$(cd "$(sample NUnit)" && run_status dotnet assurance init --framework xunit-v3)" 1
check_contains "the contradiction is named" "$WORK/last-output.txt" "already references Ahr.Assurance.NUnit, not Ahr.Assurance.Xunit.v3."

without_adapters "$WORK/init-solution"
cd "$WORK/init-solution"
{
  echo '<Solution>'
  for framework in Core Xunit.v2 MSTest.v3 TUnit; do
    echo "  <Project Path=\"$(sample "$framework")/Ahr.Assurance.Sample.$framework.csproj\" />"
  done
  echo '</Solution>'
} > All.slnx
check "init in a solution's folder" "$(run_status dotnet assurance init)" 0
check_contains "the project without tests is skipped" "$WORK/last-output.txt" "Skipped Ahr.Assurance.Sample.Core.csproj: no test framework."
for framework in Xunit.v2 MSTest.v3 TUnit; do
  check_contains "the solution names the $framework project's adapter" "$WORK/last-output.txt" "Ahr.Assurance.Sample.$framework.csproj uses"
  check "the solution's $framework project has its config" "$(exists "$(sample "$framework")/ahr-assurance.json")" yes
done
check "init --framework tunit in a solution's folder" "$(run_status dotnet assurance init --framework tunit)" 0
check_contains "only the TUnit project gets its adapter" "$WORK/last-output.txt" "Added Ahr.Assurance.TUnit $VERSION to Ahr.Assurance.Sample.TUnit.csproj"
check_contains "the others are skipped" "$WORK/last-output.txt" "Skipped Ahr.Assurance.Sample.Xunit.v2.csproj: uses xUnit v2 (xunit-v2)."
check "the solution's TUnit project builds after init" "$(run_status dotnet build "$(sample TUnit)" -c Release)" 0

without_adapters "$WORK/init-offline"
cd "$WORK/init-offline/$(sample TUnit)"
mkdir -p "$WORK/empty-feed"
cat > nuget.config <<XML
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="empty" value="$WORK/empty-feed" />
  </packageSources>
</configuration>
XML
PROJECT_HASH="$(shasum Ahr.Assurance.Sample.TUnit.csproj)"
check "init fails when the adapter can't be found" "$(NUGET_PACKAGES="$WORK/empty-packages" run_status dotnet assurance init --framework tunit)" 1
check_contains "init says the package step failed" "$WORK/last-output.txt" "dotnet add package Ahr.Assurance.TUnit --version $VERSION' failed"
check "init writes no config when the package step fails" "$(exists ahr-assurance.json)" no
check "the project is unchanged when the package step fails" "$(shasum Ahr.Assurance.Sample.TUnit.csproj)" "$PROJECT_HASH"

echo "Other framework versions"
# Runs one sample with other framework versions: its name, the sample, then package and version pairs.
# PROJECT_EDIT, when set, is a sed script for the sample's project file.
older_versions() {
  local name="$1" framework="$2" folder="$WORK/older/${1// /-}"
  shift 2
  copy_samples "$folder"
  while [ "$#" -gt 0 ]; do
    sed -i.bak "s|Include=\"$1\" Version=\"[^\"]*\"|Include=\"$1\" Version=\"$2\"|" "$folder/Directory.Packages.props"
    shift 2
  done
  if [ -n "${PROJECT_EDIT:-}" ]; then
    sed -i.bak "$PROJECT_EDIT" "$folder/$(sample "$framework")/Ahr.Assurance.Sample.$framework.csproj"
  fi
  cd "$folder"
  check "$name builds" "$(run_status dotnet build "$(sample "$framework")" -c Release)" 0
  check "$name passes" "$(ASSURANCE_RUN_ID=older run_status dotnet test --project "$(sample "$framework")" -c Release --no-build --assurance-report on)" 0
  check "$name wrote its report as the run ended" "$(exists "$(sample "$framework")/artifacts/assurance/older/report/report.html")" yes
  check_contains "$name's report has the rows as examples" "$(sample "$framework")/artifacts/assurance/older/report/report.html" "2 of 2 examples passed"
}
older_versions "NUnit 3" NUnit NUnit 3.14.0 NUnit3TestAdapter 5.2.0 Microsoft.Testing.Extensions.TrxReport 1.9.1
check "NUnit3TestAdapter 5 runs on platform 1.x" "$(grep -q '"Microsoft.Testing.Platform/1\.' "$(sample NUnit)/obj/project.assets.json" && echo yes || echo no)" yes
older_versions "NUnit 5" NUnit NUnit 5.0.0
PROJECT_EDIT='s|"MSTest" VersionOverride="3.11.1"|"MSTest" VersionOverride="4.0.0"|; s|"Ahr.Assurance.MSTest.v3"|"Ahr.Assurance.MSTest"|' older_versions "MSTest 4.0 with TestContext" MSTest.v3
older_versions "xUnit v3 3.2" Xunit.v3 xunit.v3 3.2.2 Microsoft.Testing.Extensions.TrxReport 1.9.1

restore_outputs() {
  find "$REPO" -path "$WORK" -prune -o \( -name project.assets.json -o -name '*.nuget.g.props' \) -print
}
restores_in_work() {
  { find "$REPO" -path "$WORK" -prune -o \( -name project.assets.json -o -name '*.nuget.g.props' \) -exec grep -lF -- "$WORK" {} + || true; } | wc -l | tr -d ' '
}
check "the repository has restore outputs" "$([ "$(restore_outputs | wc -l)" -gt 0 ] && echo yes || echo no)" yes
check "the repository's restore stays out of the work folder" "$(restores_in_work)" 0

echo
if [ "$FAILED" -eq 0 ] && [ -z "$KEEP_WORK" ]; then
  cd "$REPO"
  rm -rf "$WORK"
  echo "Passed $PASSED, failed $FAILED. Removed the temporary files."
else
  echo "Passed $PASSED, failed $FAILED. Files are in $WORK"
fi
[ "$FAILED" -eq 0 ]
