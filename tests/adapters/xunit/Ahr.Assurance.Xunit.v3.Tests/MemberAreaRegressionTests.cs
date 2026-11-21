using Ahr.Assurance.Adapters.Conformance;
using Ahr.Assurance.Core;
using Ahr.Assurance.Execution;
using Ahr.Foundation;
using static Ahr.Assurance.XunitV3.Tests.VerificationSteps;

namespace Ahr.Assurance.XunitV3.Tests;

public sealed class MemberAreaRegressionTests
{
    [Fact]
    public Task JourneysVerifiedTogetherKeepTheirOwnVerdicts() =>
        Scenario.Define<VerificationUnderTest>("Member, guest and non-member journeys verified together each keep their own verdict", AdapterBehaviour.Verdicts)
            .Given("The member, guest and non-member journeys, each with its own access policy", verification => verification.Use("Member-area regression journeys",
            [
                MemberAreaJourney.Build(MemberProfile.Member, MemberAccessPolicy.Member),
                MemberAreaJourney.Build(MemberProfile.Guest, MemberAccessPolicy.Guest),
                MemberAreaJourney.Build(MemberProfile.NonMember, MemberAccessPolicy.NonMember),
            ]))
            .And(TheBuiltInHtmlReport)
            .When(ItIsVerified)
            .Then(TheTestPasses)
            .And("Each of the three journeys is satisfied", verification =>
            {
                Assert.Equal(3, verification.Result!.Scenarios.Length);
                Assert.All(verification.Result.Scenarios, scenario => Assert.Equal(AssuranceStatus.Satisfied, scenario.Status));
            })
            .VerifyAsync();

    [Fact]
    public Task ContentShownToAGuestFailsTheTest() =>
        Scenario.Define<VerificationUnderTest>("Protected content shown to a guest fails the test, even though the guest was asked to sign in", AdapterBehaviour.Verdicts)
            .Given("A guest journey on a member area that leaks protected content", verification =>
                verification.Use(MemberAreaJourney.Build(MemberProfile.Guest, MemberAccessPolicy.Guest, leakContent: true)))
            .And(TheBuiltInHtmlReport)
            .When(ItIsVerified)
            .Then(TheTestFailsSaying("Protected content leaked to a blocked user."))
            .And(TheReportSays("Member handbook"))
            .And(TheReportSays("Protected content leaked to a blocked user."))
            .VerifyAsync();

    [Fact]
    public Task AJourneyRunTwiceStartsFreshEachTime()
    {
        Scenario<MemberAreaContext> journey = MemberAreaJourney.Build(MemberProfile.Member, MemberAccessPolicy.Member);
        List<Result<AssuranceRunResult, AssuranceExecutionError>> outcomes = [];
        return Scenario.Define<VerificationUnderTest>("A journey run twice runs each time from a fresh context", AdapterBehaviour.Isolation)
            .Given("The member journey", _ => { })
            .When("It is run twice", async (_, token) =>
            {
                outcomes.Add(await journey.RunAsync(token));
                outcomes.Add(await journey.RunAsync(token));
            })
            .Then("Both runs complete, each with its own result", _ => Assert.NotSame(
                outcomes[0].Value,
                outcomes[1].Value))
            .VerifyAsync();
    }
}

internal enum MemberProfile
{
    Member,
    Guest,
    NonMember,
}

internal enum AccessDecision
{
    Granted,
    AuthenticationRequired,
    Forbidden,
}

internal sealed record ExpectedAccess(AccessDecision Decision, string? Content);

internal static class MemberAccessPolicy
{
    internal static ExpectedAccess Member { get; } = new(AccessDecision.Granted, "Member handbook");

    internal static ExpectedAccess Guest { get; } = new(AccessDecision.AuthenticationRequired, null);

    internal static ExpectedAccess NonMember { get; } = new(AccessDecision.Forbidden, null);
}

internal static class MemberAreaJourney
{
    private static readonly BusinessContext _business = BusinessContext
        .Define("Only eligible members receive protected content")
        .Capability("member-area", "Open the member area")
        .Outcome("eligible-content", "Members receive protected content")
        .Build();

    internal static Scenario<MemberAreaContext> Build(
        MemberProfile profile,
        ExpectedAccess expected,
        bool leakContent = false)
    {
        ArgumentNullException.ThrowIfNull(expected);
        return Scenario.Define($"{profile} opens the member area", _ => Task.FromResult(new MemberAreaContext(new SimulatedMemberArea(leakContent))), _business, id: profile.ToString())
            .Given("The user's identity is established", context => context.Identity = profile switch
            {
                MemberProfile.Member => new(true, true),
                MemberProfile.Guest => new(false, false),
                MemberProfile.NonMember => new(true, false),
                _ => throw new ArgumentOutOfRangeException(nameof(profile))
            })
            .When("The user requests the member area", async (context, cancellationToken) => context.Response =
                await context.Application.RequestAsync(
                    context.Identity ?? throw new InvalidOperationException("The user's identity is not set."),
                    cancellationToken).ConfigureAwait(false))
            .Then("The application decision matches the supplied policy", context =>
                context.Response is { } response
                    ? ExpectationResult.Check(
                        response.Decision == expected.Decision,
                        "Compare the response with an independently supplied access policy.",
                        expected.Decision.ToString(),
                        response.Decision.ToString())
                    : new(AssuranceStatus.NotEvaluated, "The application returned no response."))
            .And("Ineligible users receive no protected content", context =>
                context.Response is { } response
                    ? ExpectationResult.Check(
                        response.Content == expected.Content,
                        response.Content is null ? "Protected content was withheld." : "Protected content leaked to a blocked user.",
                        expected.Content ?? "No protected content",
                        response.Content ?? "No protected content")
                    : new(AssuranceStatus.NotEvaluated, "The application returned no response."));
    }
}

internal sealed record MemberIdentity(bool IsAuthenticated, bool IsMember);

internal sealed record MemberAreaResponse(AccessDecision Decision, string? Content);

internal sealed class MemberAreaContext(SimulatedMemberArea application)
{
    internal SimulatedMemberArea Application { get; } = application;

    internal MemberIdentity? Identity { get; set; }

    internal MemberAreaResponse? Response { get; set; }
}

internal sealed class SimulatedMemberArea(bool leakContent)
{
    internal Task<MemberAreaResponse> RequestAsync(MemberIdentity identity, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AccessDecision decision = !identity.IsAuthenticated
            ? AccessDecision.AuthenticationRequired
            : identity.IsMember ? AccessDecision.Granted : AccessDecision.Forbidden;
        var content = decision == AccessDecision.Granted || leakContent ? "Member handbook" : null;
        return Task.FromResult(new MemberAreaResponse(decision, content));
    }
}
