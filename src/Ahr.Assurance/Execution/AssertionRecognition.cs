namespace Ahr.Assurance.Execution;

internal enum RecognisedKind
{
    Failure,
    Skip,
}

internal readonly record struct RecognisedException(RecognisedKind Kind, string Library, string Message);

internal static class AssertionRecognition
{
    private const string XunitDynamicSkip = "$XunitDynamicSkip$";
    private const string MSTestInconclusive = "Assert.Inconclusive. ";
    private const string EarlierMSTestInconclusive = "Assert.Inconclusive failed. ";

    private static readonly Dictionary<string, (RecognisedKind Kind, string Library)> _types = new(StringComparer.Ordinal)
    {
        ["Xunit.Sdk.SkipException"] = (RecognisedKind.Skip, "xUnit"),
        ["Xunit.SkipException"] = (RecognisedKind.Skip, "Xunit.SkippableFact"),
        ["NUnit.Framework.IgnoreException"] = (RecognisedKind.Skip, "NUnit"),
        ["NUnit.Framework.InconclusiveException"] = (RecognisedKind.Skip, "NUnit"),
        ["Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException"] = (RecognisedKind.Skip, "MSTest"),
        ["TUnit.Core.Exceptions.SkipTestException"] = (RecognisedKind.Skip, "TUnit"),
        ["NUnit.Framework.AssertionException"] = (RecognisedKind.Failure, "NUnit"),
        ["NUnit.Framework.MultipleAssertException"] = (RecognisedKind.Failure, "NUnit"),
        ["Microsoft.VisualStudio.TestTools.UnitTesting.AssertFailedException"] = (RecognisedKind.Failure, "MSTest"),
        ["TUnit.Assertions.Exceptions.BaseAssertionException"] = (RecognisedKind.Failure, "TUnit"),
        ["Shouldly.ShouldAssertException"] = (RecognisedKind.Failure, "Shouldly"),
        ["FluentAssertions.Execution.AssertionFailedException"] = (RecognisedKind.Failure, "FluentAssertions"),
        ["AwesomeAssertions.Execution.AssertionFailedException"] = (RecognisedKind.Failure, "AwesomeAssertions"),
        ["Ahr.Assurance.Execution.ExpectedExceptionNotThrownException"] = (RecognisedKind.Failure, "Assurance"),
    };

    internal static RecognisedException? Classify(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        for (Type? type = exception.GetType(); type is not null; type = type.BaseType)
        {
            if (type.FullName is { } name && _types.TryGetValue(name, out (RecognisedKind Kind, string Library) known))
            {
                return new(known.Kind, known.Library, Message(exception));
            }
        }

        return exception.GetType().GetInterfaces().Any(type => type.FullName == "Xunit.Sdk.IAssertionException")
            ? new(RecognisedKind.Failure, "xUnit", exception.Message)
            : IsPlaywrightExpectation(exception)
            ? new(RecognisedKind.Failure, "Playwright", exception.Message)
            : null;
    }

    private static bool IsPlaywrightExpectation(Exception exception)
    {
        if (exception.GetType().FullName != "Microsoft.Playwright.PlaywrightException")
        {
            return false;
        }

        ReadOnlySpan<char> message = exception.Message;
        var end = message.IndexOf('\n');
        ReadOnlySpan<char> first = end < 0 ? message : message[..end];
        return (first.StartsWith("Locator ", StringComparison.Ordinal) || first.StartsWith("Page ", StringComparison.Ordinal)
                || first.StartsWith("Response ", StringComparison.Ordinal))
            && first.Contains(" expected ", StringComparison.Ordinal);
    }

    private static string Message(Exception exception) =>
        exception.Message.StartsWith(XunitDynamicSkip, StringComparison.Ordinal) ? exception.Message[XunitDynamicSkip.Length..]
        : exception.Message.StartsWith(MSTestInconclusive, StringComparison.Ordinal) ? exception.Message[MSTestInconclusive.Length..]
        : exception.Message.StartsWith(EarlierMSTestInconclusive, StringComparison.Ordinal) ? exception.Message[EarlierMSTestInconclusive.Length..]
        : exception.Message;
}
