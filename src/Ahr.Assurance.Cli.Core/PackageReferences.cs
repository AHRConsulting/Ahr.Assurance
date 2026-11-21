using System.Diagnostics;
using Ahr.Foundation;

namespace Ahr.Assurance.Cli;

internal interface IPackageReferences
{
    Result<string, CommandError> Add(string project, string package, string version);
}

internal sealed class DotnetPackageReferences : IPackageReferences
{
    private const int ShownLines = 20;

    public Result<string, CommandError> Add(string project, string package, string version)
    {
        var command = $"dotnet add package {package} --version {version}";
        ProcessStartInfo start = new(Dotnet())
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(project)
        };
        foreach (var argument in new[] { "add", project, "package", package, "--version", version })
        {
            start.ArgumentList.Add(argument);
        }
        return Result.Try(() => Run(start), exception => exception is InvalidOperationException or System.ComponentModel.Win32Exception or IOException,
                exception => new CommandError("package-error", $"'{command}' could not start: {exception.Message}"))
            .Bind(run => CommandResult.Success(command)
                .Ensure(_ => run.ExitCode == 0, new CommandError("package-error", $"'{command}' failed with exit code {run.ExitCode}.", Tail(run.Output))));
    }

    private static (int ExitCode, string Output) Run(ProcessStartInfo start)
    {
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("dotnet did not start.");
        Task<string> error = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output + error.GetAwaiter().GetResult());
    }

    private static string Dotnet() => Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host ? host : "dotnet";

    private static string Tail(string output)
    {
        var lines = output.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return string.Join(Environment.NewLine, lines.TakeLast(ShownLines));
    }
}
