using FluentAssertions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Consults.Common;
using MedApp.Domain.Consults;
using MedApp.Domain.Pathways.PatientPathways;
using static MedApp.UnitTests.Common.Constants.ConsultsTestConstants;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;

namespace MedApp.UnitTests.Application.Consults.Common;

[TestFixture]
public sealed class ConsultMapperTests
{
    [Test]
    public void ToResponse_RepliedConsult_GivesEverythingShown()
    {
        var pathway = CreateDermatologyPathway();
        var consult = CreateConsult(pathway, ConsultStatus.Replied);

        var response = ConsultMapper.ToResponse(consult, Asker);

        response.Id.Should().Be(consult.Id);
        response.Status.Should().Be("Replied");
        response.Patient!.Id.Should().Be(pathway.PatientId);
        response.PathwayId.Should().Be(pathway.Id);
        response.PathwayTitle.Should().Be(PathwayTitle);
        response.AskingTeamId.Should().Be(Dermatology.Id);
        response.AskingTeamName.Should().Be("Dermatology");
        response.AskedTeamId.Should().Be(Lab.Id);
        response.AskedTeamName.Should().Be("Lab");
        response.Question.Should().Be(Question);
        response.RequestedByUsername.Should().Be(Asker.Username);
        response.RequestedAtUtc.Should().Be(consult.RequestedAtUtc);
        response.Reply.Should().Be(Reply);
        response.RepliedByUsername.Should().Be(Pathologist.Username);
        response.RepliedAtUtc.Should().Be(consult.RepliedAtUtc);
        response.ClosedByUsername.Should().BeNull();
        response.Version.Should().Be(2);
    }

    [TestCase(ConsultStatus.Open, false, false, true)]
    [TestCase(ConsultStatus.Replied, false, true, false)]
    [TestCase(ConsultStatus.Closed, false, false, false)]
    [TestCase(ConsultStatus.Withdrawn, false, false, false)]
    public void ToResponse_ForTheAskingTeam_ClosesAfterAReplyAndWithdrawsBefore(
        ConsultStatus status, bool canReply, bool canClose, bool canWithdraw)
    {
        var response = ConsultMapper.ToResponse(CreateConsult(CreateDermatologyPathway(), status), Asker);

        response.Should().BeEquivalentTo(new { CanReply = canReply, CanClose = canClose, CanWithdraw = canWithdraw });
    }

    [TestCase(ConsultStatus.Open, true)]
    [TestCase(ConsultStatus.Replied, false)]
    [TestCase(ConsultStatus.Closed, false)]
    public void ToResponse_ForTheAskedTeam_RepliesOnlyWhileOpen(ConsultStatus status, bool canReply)
    {
        var response = ConsultMapper.ToResponse(CreateConsult(CreateDermatologyPathway(), status), Pathologist);

        response.CanReply.Should().Be(canReply);
        response.CanClose.Should().BeFalse();
        response.CanWithdraw.Should().BeFalse();
    }

    [Test]
    public void ToResponse_RestrictedRecord_OffersNoReply()
    {
        var pathway = CreateDermatologyPathway();
        pathway.Patient.RestrictedAtUtc = Now;

        ConsultMapper.ToResponse(CreateConsult(pathway), Pathologist).CanReply.Should().BeFalse();
    }

    [Test]
    public void ToResponse_WithoutWorkPathways_OffersNothing()
    {
        var reader = Asker with { Permissions = new HashSet<Permission> { Permission.ViewPatients } };

        var response = ConsultMapper.ToResponse(CreateConsult(CreateDermatologyPathway()), reader);

        response.Should().BeEquivalentTo(new { CanReply = false, CanClose = false, CanWithdraw = false });
    }

    [Test]
    public void CanRequest_PathwaysOwnTeam_WhileOpenAndChangeable()
    {
        var pathway = CreateDermatologyPathway();

        ConsultMapper.CanRequest(pathway, Asker).Should().BeTrue();
        ConsultMapper.CanRequest(pathway, Pathologist).Should().BeFalse("only the pathway's own team asks");
        ConsultMapper.CanRequest(pathway, AdminViewer).Should().BeFalse();
        ConsultMapper.CanRequest(pathway, Asker with { Permissions = new HashSet<Permission>() }).Should().BeFalse();
    }

    [TestCase(PatientPathwayStatus.Completed)]
    [TestCase(PatientPathwayStatus.Cancelled)]
    [TestCase(PatientPathwayStatus.Archived)]
    public void CanRequest_ClosedPathway_IsFalse(PatientPathwayStatus status) =>
        ConsultMapper.CanRequest(CreateDermatologyPathway(status), Asker).Should().BeFalse();

    [Test]
    public void CanRequest_RestrictedRecord_IsFalse()
    {
        var pathway = CreateDermatologyPathway();
        pathway.Patient.RestrictedAtUtc = Now;

        ConsultMapper.CanRequest(pathway, Asker).Should().BeFalse();
    }
}

[TestFixture]
public sealed class ConsultRulesTests
{
    [TestCase(ConsultStatus.Replied, null)]
    [TestCase(ConsultStatus.Open, ConsultRules.NoReplyYetMessage)]
    [TestCase(ConsultStatus.Closed, ConsultRules.AlreadyClosedMessage)]
    [TestCase(ConsultStatus.Withdrawn, ConsultRules.AlreadyClosedMessage)]
    public void WhyCantClose_OnlyOnceReplied(ConsultStatus status, string? expected) =>
        ConsultRules.WhyCantClose(new Consult { Status = status }).Should().Be(expected);

    [TestCase(ConsultStatus.Open, null)]
    [TestCase(ConsultStatus.Replied, ConsultRules.HasReplyMessage)]
    [TestCase(ConsultStatus.Closed, ConsultRules.AlreadyClosedMessage)]
    public void WhyCantWithdraw_OnlyBeforeAReply(ConsultStatus status, string? expected) =>
        ConsultRules.WhyCantWithdraw(new Consult { Status = status }).Should().Be(expected);

    [Test]
    public void Messages_NameTheTeams_OrAnotherTeam()
    {
        ConsultRules.NotPathwaysTeam("Dermatology").Should().Be("This pathway is Dermatology’s: only its own team asks another team about it.");
        ConsultRules.AlreadyOpen("Lab").Should().Be("This pathway already has an open consult with Lab: close or withdraw it first.");
        ConsultRules.NotAskedTeam("Lab").Should().Be("This consult was asked of Lab: only that team replies.");
        ConsultRules.NotAskingTeam(null).Should().Be("This consult is another team’s: only the team that asked closes or withdraws it.");
    }

    [Test]
    public void IsOpen_OpenOrReplied() =>
        Enum.GetValues<ConsultStatus>().Where(s => new Consult { Status = s }.IsOpen)
            .Should().Equal(ConsultStatus.Open, ConsultStatus.Replied);

    [TestCase(ConsultStatus.Open, 0)]
    [TestCase(ConsultStatus.Replied, 1)]
    [TestCase(ConsultStatus.Closed, 2)]
    [TestCase(ConsultStatus.Withdrawn, 3)]
    public void ConsultStatus_KeepsItsStoredNumbers(ConsultStatus status, int number) => ((int)status).Should().Be(number);

    [TestCase(PatientPathwayAuditAction.ConsultRequested, 59)]
    [TestCase(PatientPathwayAuditAction.ConsultReplied, 60)]
    [TestCase(PatientPathwayAuditAction.ConsultClosed, 61)]
    [TestCase(PatientPathwayAuditAction.ConsultWithdrawn, 62)]
    public void AuditActions_KeepTheirStoredNumbers(PatientPathwayAuditAction action, int number) => ((int)action).Should().Be(number);
}
