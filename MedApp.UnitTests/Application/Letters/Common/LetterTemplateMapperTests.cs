using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Letters.Common;
using MedApp.Application.Letters.Repositories;
using MedApp.Domain.Letters;
using Moq;
using static MedApp.UnitTests.Common.Constants.LettersTestConstants;

namespace MedApp.UnitTests.Application.Letters.Common;

[TestFixture]
public sealed class LetterTemplateMapperTests
{
    [Test]
    public void ToResponse_CopiesTheTemplate_WithItsTeamsName_AndTheMergeFieldsItUses()
    {
        var teamId = Guid.NewGuid();
        var template = CreateTemplate(teamId: teamId, kind: LetterKind.DischargeSummary, isArchived: true);

        var response = LetterTemplateMapper.ToResponse(template, "Synthetic dermatology");

        response.Id.Should().Be(template.Id);
        response.Name.Should().Be(TemplateName);
        response.Kind.Should().Be("DischargeSummary");
        response.TeamId.Should().Be(teamId);
        response.TeamName.Should().Be("Synthetic dermatology");
        response.Letterhead.Should().Be(Letterhead);
        response.BodySkeleton.Should().Be(BodySkeleton);
        response.Closing.Should().Be(Closing);
        response.MergeFields.Should().Equal("patient.name", "patient.dateOfBirth", "author.name", "author.jobTitle");
        response.IsArchived.Should().BeTrue();
        response.Version.Should().Be(3);
        response.CreatedAt.Should().Be(CreatedAtUtc);
        response.LastUpdated.Should().Be(CreatedAtUtc);
    }

    [Test]
    public void ToResponse_ForEveryTeam_HasNoTeamName() =>
        LetterTemplateMapper.ToResponse(CreateTemplate(), "ignored").TeamName.Should().BeNull();

    [Test]
    public void ToResponse_MergeField_CopiesIt()
    {
        var field = LetterMergeFields.All[0];

        var response = LetterTemplateMapper.ToResponse(field);

        response.Should().BeEquivalentTo(new { field.Name, field.Label, field.Description, field.Example });
    }

    [Test]
    public void Kinds_AreDescribedInWords()
    {
        LetterKinds.Describe(LetterKind.ClinicLetter).Should().Be("Clinic letter");
        LetterKinds.Describe(LetterKind.DischargeSummary).Should().Be("End of care letter");
        LetterKinds.Describe(LetterKind.ReferralResponse).Should().Be("Referral response");
        LetterKinds.Describe(LetterKind.InformationRequest).Should().Be("Request for more information");
        LetterKinds.Describe(LetterKind.AppointmentLetter).Should().Be("Appointment letter");
        LetterKinds.Describe(LetterKind.OnwardReferral).Should().Be("Onward referral");
        LetterKinds.Describe(LetterKind.Other).Should().Be("Letter");
        LetterKinds.Names.Should().Be(
            "ClinicLetter, DischargeSummary, ReferralResponse, InformationRequest, AppointmentLetter, Other, OnwardReferral");
    }

    [Test]
    public void LetterKind_KeepsItsStoredNumbers() =>
        Enum.GetValues<LetterKind>().Select(k => (int)k).Should().Equal(0, 1, 2, 3, 4, 5);

    [Test]
    public void Identifiers_LeaveOutWhatIsntKnown()
    {
        LetterIdentifiers.Line("Sam Example", "1 January 1970", "999 000 0018")
            .Should().Be("Sam Example · Born 1 January 1970 · NHS number 999 000 0018");
        LetterIdentifiers.Line("Sam Example", null, " ").Should().Be("Sam Example");
    }

    [Test]
    public void Tidy_MakesLineBreaksPlain_AndTrimsTheEnd()
    {
        LetterTemplateRules.Tidy("a\r\nb\rc  \n\n").Should().Be("a\nb\nc");
        LetterTemplateRules.Tidy(null).Should().BeEmpty();
    }

    [Test]
    public void Lines_KeepBlankLinesBetween()
    {
        LetterTemplateRules.Lines("Yours,\r\n\r\nAlex  ").Should().Equal("Yours,", "", "Alex");
        LetterTemplateRules.Lines("  ").Should().BeEmpty();
    }

    [Test]
    public void EnsureVersion_AnotherVersion_IsAConflict()
    {
        var template = CreateTemplate(version: 2);

        FluentActions.Invoking(() => LetterTemplateRules.EnsureVersion(template, 2)).Should().NotThrow();
        FluentActions.Invoking(() => LetterTemplateRules.EnsureVersion(template, 1))
            .Should().Throw<ConflictException>().WithMessage(LetterTemplateRules.ChangedMeanwhileMessage);
    }

    [Test]
    public async Task GetOrThrow_None_IsNotFound()
    {
        var repository = new Mock<ILetterTemplateRepository>();

        var act = () => repository.Object.GetOrThrowAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public void NameTakenMessages_NameTheTeam()
    {
        LetterTemplateRules.NameTakenMessage("X", null).Should().Be("A letter template for every team is already called 'X'.");
        LetterTemplateRules.NameTakenMessage("X", "Synthetic dermatology")
            .Should().Be("A Synthetic dermatology letter template is already called 'X'.");
        LetterTemplateRules.NameTakenMessage("X").Should().Contain("'X'");
    }
}
