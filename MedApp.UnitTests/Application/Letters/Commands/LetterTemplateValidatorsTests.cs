using FluentAssertions;
using MedApp.Application.Letters.Commands.CreateLetterTemplate;
using MedApp.Application.Letters.Commands.UpdateLetterTemplate;
using MedApp.Application.Letters.Common;
using MedApp.Application.Letters.Queries.PreviewLetterTemplate;
using MedApp.Domain.Letters;
using static MedApp.UnitTests.Common.Constants.LettersTestConstants;

namespace MedApp.UnitTests.Application.Letters.Commands;

[TestFixture]
public sealed class LetterTemplateValidatorsTests
{
    private static CreateLetterTemplateCommand Create(
        string name = TemplateName,
        string kind = "ClinicLetter",
        Guid? specialtyId = null,
        string letterhead = Letterhead,
        string body = BodySkeleton,
        string closing = Closing) =>
        new(name, kind, specialtyId, letterhead, body, closing);

    private static CreateLetterTemplateValidator CreateValidator(params string[] missing) =>
        new(RendererMissing(missing).Object);

    [Test]
    public void Create_Valid_Passes() => CreateValidator().Validate(Create()).IsValid.Should().BeTrue();

    [Test]
    public void Create_OnlyANameAndKind_Passes() =>
        CreateValidator().Validate(Create(letterhead: "", body: "", closing: "")).IsValid.Should().BeTrue();

    [TestCase("")]
    [TestCase("   ")]
    public void Create_NoName_Fails(string name) =>
        CreateValidator().Validate(Create(name: name)).Errors.Should().ContainSingle(e => e.PropertyName == "Name");

    [Test]
    public void Create_NameTooLong_Fails() =>
        CreateValidator().Validate(Create(name: new string('a', LetterTemplate.MaxNameLength + 1)))
            .Errors.Should().ContainSingle(e => e.PropertyName == "Name");

    [TestCase("")]
    [TestCase("Letter")]
    [TestCase("0")]
    [TestCase("6")]
    public void Create_UnknownKind_Fails(string kind) =>
        CreateValidator().Validate(Create(kind: kind)).Errors.Should().ContainSingle(e => e.PropertyName == "Kind");

    [Test]
    public void Create_KindIgnoringCase_Passes() =>
        CreateValidator().Validate(Create(kind: "dischargesummary")).IsValid.Should().BeTrue();

    [Test]
    public void Create_EmptySpecialtyId_Fails() =>
        CreateValidator().Validate(Create(specialtyId: Guid.Empty))
            .Errors.Should().ContainSingle(e => e.PropertyName == "SpecialtyId");

    [Test]
    public void Create_TextsTooLong_Fail()
    {
        var errors = CreateValidator().Validate(Create(
            letterhead: new string('a', LetterTemplate.MaxLetterheadLength + 1),
            body: new string('a', LetterTemplate.MaxBodyLength + 1),
            closing: new string('a', LetterTemplate.MaxClosingLength + 1))).Errors;

        errors.Select(e => e.PropertyName).Should().BeEquivalentTo("Letterhead", "BodySkeleton", "Closing");
        errors.Single(e => e.PropertyName == "BodySkeleton").ErrorMessage.Should().Be("Body skeleton must not exceed 20,000 characters.");
    }

    [Test]
    public void Create_LengthIsCountedAfterTidying() =>
        CreateValidator().Validate(Create(letterhead: new string('a', LetterTemplate.MaxLetterheadLength) + "\r\n   "))
            .IsValid.Should().BeTrue();

    [Test]
    public void Create_UnknownMergeField_FailsOnTheFieldHoldingIt()
    {
        var errors = CreateValidator().Validate(Create(body: "Dear {{patient.nme}}")).Errors;

        errors.Should().ContainSingle(e => e.PropertyName == "BodySkeleton")
            .Which.ErrorMessage.Should().StartWith("{{patient.nme}} isn't a merge field.");
    }

    [Test]
    public void Create_CharactersTheFontLacks_FailOnTheFieldHoldingThem()
    {
        var errors = CreateValidator("✓").Validate(Create(closing: "Done ✓")).Errors;

        errors.Should().ContainSingle(e => e.PropertyName == "Closing")
            .Which.ErrorMessage.Should().Be(LetterTemplateValidation.UnsupportedMessage(["✓"]));
    }

    [Test]
    public void Update_Valid_Passes() =>
        new UpdateLetterTemplateValidator(RendererMissing().Object)
            .Validate(new UpdateLetterTemplateCommand(Guid.NewGuid(), TemplateName, "Other", Guid.NewGuid(), Letterhead,
                BodySkeleton, Closing, 2))
            .IsValid.Should().BeTrue();

    [Test]
    public void Update_NoIdOrVersion_Fails() =>
        new UpdateLetterTemplateValidator(RendererMissing().Object)
            .Validate(new UpdateLetterTemplateCommand(Guid.Empty, "{{x}}", "Nope", Guid.Empty, "{{x}}", "", "", 0))
            .Errors.Select(e => e.PropertyName)
            .Should().BeEquivalentTo("TemplateId", "Kind", "SpecialtyId", "Letterhead", "ExpectedVersion");

    [Test]
    public void Preview_ChecksTheTextAndAnOptionalName()
    {
        var validator = new PreviewLetterTemplateValidator(RendererMissing("中").Object);

        validator.Validate(new PreviewLetterTemplateQuery(null, Letterhead, BodySkeleton, Closing, true)).IsValid.Should().BeTrue();
        validator.Validate(new PreviewLetterTemplateQuery(new string('a', 201), "中", "{{x}}", "", false))
            .Errors.Select(e => e.PropertyName).Should().BeEquivalentTo("Name", "Letterhead", "BodySkeleton");
    }
}
