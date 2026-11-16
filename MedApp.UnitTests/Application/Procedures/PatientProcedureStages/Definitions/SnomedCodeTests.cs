using AutoFixture;
using FluentAssertions;
using MedApp.Application.Procedures.PatientProcedureStages.Definitions;
using MedApp.Application.Procedures.PatientProcedureStages.Definitions.Commands.CreatePatientProcedureStageDefinition;
using MedApp.Application.Procedures.PatientProcedureStages.Definitions.Commands.UpdatePatientProcedureStageDefinition;
using MedApp.Application.Procedures.PatientProcedureStages.Templates;
using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.NhsTestConstants;
using static MedApp.UnitTests.Common.Constants.ProceduresTestConstants;

namespace MedApp.UnitTests.Application.Procedures.PatientProcedureStages.Definitions;

/// <summary>A stage definition's SNOMED CT code and term: validated, saved, shown, and on templates.</summary>
[TestFixture]
public sealed class SnomedCodeTests
{
    private IFixture _fixture = null!;
    private Mock<IPatientProcedureStagesRepository> _repository = null!;

    [SetUp]
    public void SetUp()
    {
        _fixture = AutoFixtureFactory.Create();
        _repository = new Mock<IPatientProcedureStagesRepository>();
        _repository
            .Setup(r => r.GetStageDefinitionUsageAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, StageDefinitionUsage>());
        _fixture.Inject(_repository.Object);
    }

    private static CreatePatientProcedureStageDefinitionCommand Create(string? code, string? term) =>
        new(ConsultationStage, "d", "i", code, term);

    private static UpdatePatientProcedureStageDefinitionCommand Update(string? code, string? term) =>
        new(Guid.NewGuid(), null, null, null, code, term);

    [TestCase(null, null)]
    [TestCase("", "")]
    [TestCase(SnomedCode, SnomedTerm)]
    [TestCase(" 80146002 ", " Appendectomy ")]
    [TestCase(ExtensionConceptCode, "A local concept")]
    public void Validate_NeitherOrAValidPair_Passes(string? code, string? term)
    {
        new CreatePatientProcedureStageDefinitionValidator().Validate(Create(code, term)).IsValid.Should().BeTrue();
        new UpdatePatientProcedureStageDefinitionValidator().Validate(Update(code, term)).IsValid.Should().BeTrue();
    }

    [TestCase(WrongVerhoeff, SnomedTerm, "SnomedCode", "check digit")]
    [TestCase(DescriptionId, SnomedTerm, "SnomedCode", "not a concept")]
    [TestCase("12345", SnomedTerm, "SnomedCode", "6 to 18 digits")]
    [TestCase("abc", SnomedTerm, "SnomedCode", "digits only")]
    [TestCase(SnomedCode, null, "SnomedTerm", SnomedRules.TermRequired)]
    [TestCase(SnomedCode, " ", "SnomedTerm", SnomedRules.TermRequired)]
    [TestCase(null, SnomedTerm, "SnomedCode", SnomedRules.CodeRequired)]
    [TestCase("", SnomedTerm, "SnomedCode", SnomedRules.CodeRequired)]
    public void Validate_InvalidPair_FailsOnTheField(string? code, string? term, string field, string message)
    {
        foreach (var result in new[]
                 {
                     new CreatePatientProcedureStageDefinitionValidator().Validate(Create(code, term)),
                     new UpdatePatientProcedureStageDefinitionValidator().Validate(Update(code, term))
                 })
            result.Errors.Should().ContainSingle().Which.Should().Match<FluentValidation.Results.ValidationFailure>(
                e => e.PropertyName == field && e.ErrorMessage.Contains(message));
    }

    [Test]
    public void Validate_TermTooLong_Fails()
    {
        var result = new CreatePatientProcedureStageDefinitionValidator().Validate(Create(SnomedCode, new string('a', 256)));

        result.Errors.Should().ContainSingle(e => e.PropertyName == "SnomedTerm" && e.ErrorMessage == SnomedRules.TermTooLong);
    }

    [Test]
    public async Task Create_WithACode_SavesItAndItsTermTrimmed()
    {
        var response = await _fixture.Create<CreatePatientProcedureStageDefinitionHandler>()
            .Handle(Create($" {SnomedCode} ", $" {SnomedTerm} "), CancellationToken.None);

        response.SnomedCode.Should().Be(SnomedCode);
        response.SnomedTerm.Should().Be(SnomedTerm);
        _repository.Verify(r => r.AddStageDefinitionAsync(
            It.Is<PatientProcedureStageDefinition>(d => d.SnomedCode == SnomedCode && d.SnomedTerm == SnomedTerm),
            It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task Create_WithoutACode_SavesNeither()
    {
        var response = await _fixture.Create<CreatePatientProcedureStageDefinitionHandler>()
            .Handle(Create("", ""), CancellationToken.None);

        response.SnomedCode.Should().BeNull();
        response.SnomedTerm.Should().BeNull();
    }

    private PatientProcedureStageDefinition GivenCoded()
    {
        var definition = CreateDefinition(ConsultationStage);
        definition.SnomedCode = SnomedCode;
        definition.SnomedTerm = SnomedTerm;
        _repository.Setup(r => r.GetStageDefinitionByIdAsync(definition.Id, It.IsAny<CancellationToken>())).ReturnsAsync(definition);
        return definition;
    }

    [Test]
    public async Task Update_CodeNotGiven_LeavesBoth()
    {
        var definition = GivenCoded();

        await _fixture.Create<UpdatePatientProcedureStageDefinitionHandler>().Handle(
            new UpdatePatientProcedureStageDefinitionCommand(definition.Id, null, "New description", null), CancellationToken.None);

        definition.SnomedCode.Should().Be(SnomedCode);
        definition.SnomedTerm.Should().Be(SnomedTerm);
    }

    [Test]
    public async Task Update_EmptyCode_RemovesBoth()
    {
        var definition = GivenCoded();

        var response = await _fixture.Create<UpdatePatientProcedureStageDefinitionHandler>().Handle(
            new UpdatePatientProcedureStageDefinitionCommand(definition.Id, null, null, null, "", null), CancellationToken.None);

        definition.SnomedCode.Should().BeNull();
        definition.SnomedTerm.Should().BeNull();
        response.SnomedCode.Should().BeNull();
    }

    [Test]
    public async Task Update_NewCode_ReplacesBoth()
    {
        var definition = GivenCoded();

        var response = await _fixture.Create<UpdatePatientProcedureStageDefinitionHandler>().Handle(
            new UpdatePatientProcedureStageDefinitionCommand(definition.Id, null, null, null, "71388002", "Procedure"),
            CancellationToken.None);

        response.SnomedCode.Should().Be("71388002");
        response.SnomedTerm.Should().Be("Procedure");
    }

    [Test]
    public void StageTemplateMapper_TemplateStages_CarryTheirDefinitionsCodes()
    {
        var coded = CreateDefinition(ConsultationStage);
        coded.SnomedCode = SnomedCode;
        coded.SnomedTerm = SnomedTerm;
        var template = CreateTemplate(TemplateName, false, coded, CreateDefinition(ProcedureStage));

        var stages = StageTemplateMapper.ToResponse(template, 0).Stages;

        stages[0].SnomedCode.Should().Be(SnomedCode);
        stages[0].SnomedTerm.Should().Be(SnomedTerm);
        stages[1].SnomedCode.Should().BeNull();
    }

    [Test]
    public void SnomedRules_Normalise_NoCodeMeansNeither()
    {
        SnomedRules.Normalise(" ", SnomedTerm).Should().Be((null, null));
        SnomedRules.Normalise(null, null).Should().Be((null, null));
        SnomedRules.Normalise($" {SnomedCode}", $"{SnomedTerm} ").Should().Be((SnomedCode, SnomedTerm));
    }
}
