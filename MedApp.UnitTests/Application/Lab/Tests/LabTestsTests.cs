using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Lab.Repositories;
using MedApp.Application.Lab.Tests;
using MedApp.Contracts.Lab.Requests;
using MedApp.Domain.Lab;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using static MedApp.UnitTests.Common.Constants.SpecimensTestConstants;

namespace MedApp.UnitTests.Application.TheLab.Tests;

[TestFixture]
public sealed class LabTestHandlersTests
{
    private Mock<ILabTestRepository> _repository = null!;
    private CancellationTokenSource _cts = null!;
    private LabTest? _added;
    private List<LabTestStep> _newSteps = null!;
    private List<LabTestStep> _removedSteps = null!;

    private LabTestHandlers Handlers => new(_repository.Object, new FixedTimeProvider(Now));

    private static LabTestRequest Request(string name = " Histology ", params (string Name, string Kind, bool Optional)[] steps) => new()
    {
        Name = name,
        Kind = "histology",
        SnomedCode = " 252416005 ",
        TargetDays = 7,
        LimitDays = 10,
        AtRiskDays = 2,
        Steps = (steps.Length == 0 ? [("Grossing", "Standard", false)] : steps)
            .Select(s => new LabTestStepRequest { Name = s.Name, Kind = s.Kind, IsOptional = s.Optional })
            .ToList(),
    };

    [SetUp]
    public void SetUp()
    {
        _cts = new CancellationTokenSource();
        _added = null;
        _newSteps = [];
        _removedSteps = [];
        _repository = new Mock<ILabTestRepository>();
        _repository.Setup(r => r.AddAsync(It.IsAny<LabTest>(), It.IsAny<CancellationToken>()))
            .Callback((LabTest t, CancellationToken _) => _added = t);
        _repository.Setup(r => r.UpdateAsync(It.IsAny<LabTest>(), It.IsAny<IEnumerable<LabTestStep>>(), It.IsAny<IEnumerable<LabTestStep>>(), It.IsAny<CancellationToken>()))
            .Callback((LabTest _, IEnumerable<LabTestStep> added, IEnumerable<LabTestStep> removed, CancellationToken _) =>
            {
                _newSteps.AddRange(added);
                _removedSteps.AddRange(removed);
            });
        _repository.Setup(r => r.GetInUseAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid>());
    }

    [TearDown]
    public void TearDown() => _cts.Dispose();

    [Test]
    public async Task Create_SavesItTrimmed_WithItsStepsInOrder()
    {
        var response = await Handlers.Handle(
            new CreateLabTestCommand(Request(" Histology ", ("Grossing", "Standard", false), ("Stains", "ExtraTests", true))), _cts.Token);

        _added!.Name.Should().Be("Histology");
        _added.Kind.Should().Be(LabTestKind.Histology);
        _added.SnomedCode.Should().Be("252416005");
        _added.Steps.Select(s => (s.Position, s.Name, s.Kind, s.IsOptional))
            .Should().Equal((0, "Grossing", LabStepKind.Standard, false), (1, "Stains", LabStepKind.ExtraTests, true));
        response.Should().BeEquivalentTo(new { Name = "Histology", Kind = "Histology", TargetDays = 7, LimitDays = 10, AtRiskDays = 2, CanBeDeleted = true });
        response.Steps.Select(s => s.Name).Should().Equal("Grossing", "Stains");
    }

    [Test]
    public async Task Create_NameTaken_Throws409()
    {
        _repository.Setup(r => r.NameExistsAsync("Histology", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => Handlers.Handle(new CreateLabTestCommand(Request()), _cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(LabTestHandlers.NameTaken("Histology"));
    }

    [Test]
    public async Task Update_KeepsAStepInItsPlace_AddsNewPlaces_AndRemovesThoseGone()
    {
        var test = Histology();
        var firstId = test.Steps.Single(s => s.Position == 0).Id;
        _repository.Setup(r => r.GetAsync(test.Id, It.IsAny<CancellationToken>())).ReturnsAsync(test);

        await Handlers.Handle(new UpdateLabTestCommand(test.Id, Request("Histology",
            ("Grossing and cut-up", "Standard", false))), _cts.Token);

        test.Steps.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Id = firstId, Name = "Grossing and cut-up", Position = 0 });
        _removedSteps.Should().HaveCount(4);
        _newSteps.Should().BeEmpty();

        await Handlers.Handle(new UpdateLabTestCommand(test.Id, Request("Histology",
            ("Grossing and cut-up", "Standard", false), ("Processing", "Standard", false))), _cts.Token);

        _newSteps.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Name = "Processing", Position = 1, LabTestId = test.Id });
    }

    [Test]
    public async Task Delete_InUse_Throws409_ElseDeletes()
    {
        var test = Histology();
        _repository.Setup(r => r.GetAsync(test.Id, It.IsAny<CancellationToken>())).ReturnsAsync(test);
        _repository.Setup(r => r.GetInUseAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { test.Id });

        var act = () => Handlers.Handle(new DeleteLabTestCommand(test.Id), _cts.Token);
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("Lab cases have used 'Histology', so it can't be deleted. Archive it instead.");

        _repository.Setup(r => r.GetInUseAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid>());
        await Handlers.Handle(new DeleteLabTestCommand(test.Id), _cts.Token);
        _repository.Verify(r => r.DeleteAsync(test.Id, _cts.Token), Times.Once);
    }

    [Test]
    public async Task ArchiveAndRestore_SetTheFlag_AndUnknownIs404()
    {
        var test = Histology();
        _repository.Setup(r => r.GetAsync(test.Id, It.IsAny<CancellationToken>())).ReturnsAsync(test);

        (await Handlers.Handle(new SetLabTestArchivedCommand(test.Id, true), _cts.Token)).IsArchived.Should().BeTrue();
        (await Handlers.Handle(new SetLabTestArchivedCommand(test.Id, false), _cts.Token)).IsArchived.Should().BeFalse();

        var act = () => Handlers.Handle(new SetLabTestArchivedCommand(Guid.NewGuid(), true), _cts.Token);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Get_MarksTheOnesInUse()
    {
        var used = Histology();
        var unused = Bloods();
        _repository.Setup(r => r.GetAllAsync(true, It.IsAny<CancellationToken>())).ReturnsAsync([used, unused]);
        _repository.Setup(r => r.GetInUseAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { used.Id });

        var tests = await Handlers.Handle(new GetLabTestsQuery(true), _cts.Token);

        tests.Select(t => t.CanBeDeleted).Should().Equal(false, true);
    }
}

[TestFixture]
public sealed class LabTestValidatorTests
{
    private static LabTestRequest Valid() => new()
    {
        Name = "Histology",
        Kind = "Histology",
        TargetDays = 7,
        LimitDays = 10,
        AtRiskDays = 2,
        Steps = [new LabTestStepRequest { Name = "Grossing", Kind = "Standard" }],
    };

    private static Dictionary<string, string> Errors(LabTestRequest request) =>
        new CreateLabTestValidator().Validate(new CreateLabTestCommand(request)).Errors
            .ToDictionary(e => e.PropertyName, e => e.ErrorMessage);

    [Test]
    public void Valid_Passes_ForCreateAndUpdate()
    {
        Errors(Valid()).Should().BeEmpty();
        new UpdateLabTestValidator().Validate(new UpdateLabTestCommand(Guid.NewGuid(), Valid())).IsValid.Should().BeTrue();
    }

    [Test]
    public void EachField_IsRefusedWithTheDemosWords()
    {
        var request = Valid();
        request.Name = " ";
        request.Kind = "Astrology";
        request.TargetDays = 0;
        request.LimitDays = 91;
        request.AtRiskDays = 31;
        request.SnomedCode = "12345";
        request.Steps = [new LabTestStepRequest { Name = "", Kind = "Sideways" }];

        Errors(request).Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["Name"] = "Give it a name.",
            ["Kind"] = LabTestRules.KindMessage,
            ["TargetDays"] = LabTestRules.TargetMessage,
            ["LimitDays"] = LabTestRules.LimitMessage,
            ["AtRiskDays"] = LabTestRules.AtRiskMessage,
            ["SnomedCode"] = LabTestRules.SnomedMessage,
            ["Steps[0].Name"] = "Give step 1 a name of at most 100 characters.",
            ["Steps[0].Kind"] = "Choose step 1's kind.",
        });
    }

    [Test]
    public void LimitBeforeTarget_AndAtRiskNotBeforeTarget_AreRefused()
    {
        var request = Valid();
        request.TargetDays = 7;
        request.LimitDays = 5;
        request.AtRiskDays = 7;

        Errors(request).Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["LimitDays"] = LabTestRules.LimitBeforeTargetMessage,
            ["AtRiskDays"] = LabTestRules.AtRiskAfterTargetMessage,
        });
    }

    [Test]
    public void NoSteps_OrMoreThan20_AreRefused()
    {
        var none = Valid();
        none.Steps = [];
        Errors(none)["Steps"].Should().Be(LabTestRules.NoStepsMessage);

        var many = Valid();
        many.Steps = Enumerable.Range(0, 21).Select(i => new LabTestStepRequest { Name = $"Step {i}", Kind = "Standard" }).ToList();
        Errors(many)["Steps"].Should().Be("A test has at most 20 steps.");
    }

    [TestCase("123456", true)]
    [TestCase("123456789012345678", true)]
    [TestCase("12345", false)]
    [TestCase("12a456", false)]
    [TestCase(null, true)]
    public void SnomedCode_Is6To18Digits_OrNone(string? code, bool valid) =>
        LabTestRules.IsSnomedCode(code).Should().Be(valid);
}
