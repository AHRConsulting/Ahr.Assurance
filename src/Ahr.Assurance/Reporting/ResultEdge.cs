using Ahr.Foundation;

namespace Ahr.Assurance.Reporting;

internal static class ResultEdge
{
    internal static T OrThrow<T>(this Result<T> result, Func<Error, Exception> exception) =>
        result.IsSuccess ? result.Value : throw exception(result.Error);

    internal static T OrThrow<T, TError>(this Result<T, TError> result, Func<TError, Exception> exception) =>
        result.IsSuccess ? result.Value : throw exception(result.Error);

    internal static void OrThrow(this Result result, Func<Error, Exception> exception)
    {
        if (result.IsFailure)
        {
            throw exception(result.Error);
        }
    }
}
