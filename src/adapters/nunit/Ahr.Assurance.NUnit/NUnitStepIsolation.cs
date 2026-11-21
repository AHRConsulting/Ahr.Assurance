using NUnit.Framework.Internal;

namespace Ahr.Assurance.NUnit;

internal static class NUnitStepIsolation
{
    internal static async Task RunAsync(TestExecutionContext test, Func<Task> step)
    {
        string? output = null;
        try
        {
            using (new TestExecutionContext.IsolatedContext())
            {
                try
                {
                    await step().ConfigureAwait(false);
                }
                finally
                {
                    output = TestExecutionContext.CurrentContext.CurrentResult.Output;
                }
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(output))
            {
                test.CurrentResult.OutWriter.Write(output);
            }
        }
    }
}
