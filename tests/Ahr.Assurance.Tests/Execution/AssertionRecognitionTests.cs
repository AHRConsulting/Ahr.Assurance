using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;

namespace Ahr.Assurance.Tests
{
    public sealed class AssertionRecognitionTests
    {
        public static TheoryData<Exception, string, string> Recognised => new()
        {
            { new NUnit.Framework.AssertionException("nunit"), "Failure", "NUnit" },
            { new NUnit.Framework.MultipleAssertException("several"), "Failure", "NUnit" },
            { new NUnit.Framework.IgnoreException("ignored"), "Skip", "NUnit" },
            { new NUnit.Framework.InconclusiveException("unknown"), "Skip", "NUnit" },
            { new Microsoft.VisualStudio.TestTools.UnitTesting.AssertFailedException("mstest"), "Failure", "MSTest" },
            { new Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException("mstest"), "Skip", "MSTest" },
            { new TUnit.Assertions.Exceptions.AssertionException("tunit"), "Failure", "TUnit" },
            { new TUnit.Core.Exceptions.SkipTestException("skipped"), "Skip", "TUnit" },
            { new Shouldly.ShouldAssertException("shouldly"), "Failure", "Shouldly" },
            { new FluentAssertions.Execution.AssertionFailedException("fluent"), "Failure", "FluentAssertions" },
            { new AwesomeAssertions.Execution.AssertionFailedException("awesome"), "Failure", "AwesomeAssertions" },
            { new DerivedAssertion("derived"), "Failure", "NUnit" },
            { new Microsoft.Playwright.PlaywrightException("Locator expected to have text '£9.00'\nCall log:\n  - waiting for Locator(\"#total\")"), "Failure", "Playwright" },
            { new Microsoft.Playwright.PlaywrightException("Locator expected not to be visible "), "Failure", "Playwright" },
            { new Microsoft.Playwright.PlaywrightException("Page title expected to be 'Basket'"), "Failure", "Playwright" },
            { new Microsoft.Playwright.PlaywrightException("Page URL expected to be 'https://example.com/'"), "Failure", "Playwright" },
        };

        [Theory]
        [MemberData(nameof(Recognised))]
        public void CommonLibrariesAreRecognisedByTypeName(Exception exception, string kind, string library)
        {
            RecognisedException? recognised = AssertionRecognition.Classify(exception);

            _ = Assert.NotNull(recognised);
            Assert.Equal(kind, recognised.Value.Kind.ToString());
            Assert.Equal(library, recognised.Value.Library);
            Assert.Equal(exception.Message, recognised.Value.Message);
        }

        [Fact]
        public void XunitFailuresAndSkipsAreRecognised()
        {
            RecognisedException? failure = AssertionRecognition.Classify(Record.Exception(() => Assert.Equal(1, 2))!);
            RecognisedException? skip = AssertionRecognition.Classify(global::Xunit.Sdk.SkipException.ForSkip("no database here"));

            Assert.Equal(new RecognisedException(RecognisedKind.Failure, "xUnit", failure!.Value.Message), failure);
            Assert.Equal(new RecognisedException(RecognisedKind.Skip, "xUnit", "no database here"), skip);
        }

        [Theory]
        [InlineData("Assert.Inconclusive. no database here")]
        [InlineData("Assert.Inconclusive failed. no database here")]
        public void MSTestSkipReasonsLoseTheirMarker(string message) =>
            Assert.Equal("no database here", AssertionRecognition.Classify(
                new Microsoft.VisualStudio.TestTools.UnitTesting.AssertInconclusiveException(message))?.Message);

        [Fact]
        public void OtherExceptionsAreNotRecognised()
        {
            Assert.Null(AssertionRecognition.Classify(new InvalidOperationException("application")));
            Assert.Null(AssertionRecognition.Classify(new OperationCanceledException()));
            Assert.Null(AssertionRecognition.Classify(new Microsoft.Playwright.PlaywrightException("net::ERR_CONNECTION_REFUSED at http://localhost:5000/")));
            Assert.Null(AssertionRecognition.Classify(new Microsoft.Playwright.PlaywrightException("Error: boom\n  Locator expected to be visible")));
            Assert.Null(AssertionRecognition.Classify(new Microsoft.Playwright.PlaywrightException("Target page, context or browser has been closed")));
        }

        private sealed class DerivedAssertion(string message) : NUnit.Framework.AssertionException(message);
    }
}

#pragma warning disable CA1032, CA1064, RCS1194
namespace NUnit.Framework
{
    internal class AssertionException(string message) : Exception(message);
    internal sealed class MultipleAssertException(string message) : Exception(message);
    internal sealed class IgnoreException(string message) : Exception(message);
    internal sealed class InconclusiveException(string message) : Exception(message);
}

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    internal sealed class AssertFailedException(string message) : Exception(message);
    internal sealed class AssertInconclusiveException(string message) : Exception(message);
}

namespace TUnit.Assertions.Exceptions
{
    internal class BaseAssertionException(string message) : Exception(message);
    internal sealed class AssertionException(string message) : BaseAssertionException(message);
}

namespace TUnit.Core.Exceptions
{
    internal sealed class SkipTestException(string message) : Exception(message);
}

namespace Shouldly
{
    internal sealed class ShouldAssertException(string message) : Exception(message);
}

namespace FluentAssertions.Execution
{
    internal sealed class AssertionFailedException(string message) : Exception(message);
}

namespace AwesomeAssertions.Execution
{
    internal sealed class AssertionFailedException(string message) : Exception(message);
}

namespace Microsoft.Playwright
{
    internal sealed class PlaywrightException(string message) : Exception(message);
}
#pragma warning restore CA1032, CA1064, RCS1194
