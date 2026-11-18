using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Referrals.Commands.RequestReferralInformation;
using MedApp.Domain.Referrals;
using static MedApp.UnitTests.Common.Constants.ReferralsTestConstants;

namespace MedApp.UnitTests.Application.Referrals.Commands;

[TestFixture]
public sealed class SetReferralInformationRequestedHandlerTests : TriageCommandTestBase
{
    private SetReferralInformationRequestedHandler Handler => new(Repository.Object, Clock);

    [Test]
    public async Task Ask_KeepsItOpenWaitingForInformation_AndRecordsIt_ThenReceivedPutsItBack()
    {
        var referral = GivenReferral();

        var asked = await Handler.Handle(new SetReferralInformationRequestedCommand(referral.Id, true, referral.Version, Triager), Cts.Token);

        referral.Status.Should().Be(ReferralStatus.InformationRequested);
        referral.DecidedAtUtc.Should().BeNull("asking isn't a decision");
        Saved.Single().Action.Should().Be(ReferralEventAction.InformationRequested);
        Saved.Single().Details.Should().Contain("\"to\":\"InformationRequested\"");
        asked.IsOpen.Should().BeTrue();
        asked.CanTriage.Should().BeTrue("it can still be triaged while waiting");
        Sent.Should().BeEmpty();

        await Handler.Handle(new SetReferralInformationRequestedCommand(referral.Id, false, referral.Version, Triager), Cts.Token);

        referral.Status.Should().Be(ReferralStatus.AwaitingTriage);
        Saved.Last().Action.Should().Be(ReferralEventAction.InformationReceived);
        Tokens.Should().AllSatisfy(t => t.Should().Be(Cts.Token));
    }

    [Test]
    public async Task Ask_Twice_OrReceivedWithoutAsking_IsAConflict()
    {
        var referral = GivenReferral();

        await FluentActions.Awaiting(() => Handler.Handle(new SetReferralInformationRequestedCommand(referral.Id, false, referral.Version, Triager), Cts.Token))
            .Should().ThrowAsync<ConflictException>().WithMessage("*nothing to record*");

        referral.Status = ReferralStatus.InformationRequested;
        await FluentActions.Awaiting(() => Handler.Handle(new SetReferralInformationRequestedCommand(referral.Id, true, referral.Version, Triager), Cts.Token))
            .Should().ThrowAsync<ConflictException>().WithMessage("*asked for already*");
        Saved.Should().BeEmpty();
    }

    [Test]
    public async Task Ask_WithoutTriagePermission_IsForbidden_AndAClosedReferral_AConflict()
    {
        var referral = GivenReferral();

        await FluentActions.Awaiting(() => Handler.Handle(new SetReferralInformationRequestedCommand(referral.Id, true, referral.Version, Registrar), Cts.Token))
            .Should().ThrowAsync<ForbiddenException>();

        referral.Status = ReferralStatus.Rejected;
        await FluentActions.Awaiting(() => Handler.Handle(new SetReferralInformationRequestedCommand(referral.Id, true, referral.Version, Triager), Cts.Token))
            .Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Ask_SomeoneDecidedItMeanwhile_IsAConflictNamingThem()
    {
        var referral = GivenReferral();
        GivenSomeoneElseDecidedItFirst(referral, ReferralStatus.Accepted);

        var act = () => Handler.Handle(new SetReferralInformationRequestedCommand(referral.Id, true, referral.Version, Triager), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*o.synthetic*");
    }

    [Test]
    public void Validator_NeedsTheReferralAndAVersion()
    {
        var validator = new SetReferralInformationRequestedValidator();
        validator.Validate(new SetReferralInformationRequestedCommand(Guid.Empty, true, 0, Triager)).Errors.Should().HaveCount(2);
        validator.Validate(new SetReferralInformationRequestedCommand(Guid.NewGuid(), true, 1, Triager)).IsValid.Should().BeTrue();
    }
}
