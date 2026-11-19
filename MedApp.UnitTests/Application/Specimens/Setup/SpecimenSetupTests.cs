using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Specimens.Setup;
using MedApp.Domain.Specimens;
using Moq;
using static MedApp.UnitTests.Common.Constants.PathwaysTestConstants;
using static MedApp.UnitTests.Common.Constants.SpecimensTestConstants;

namespace MedApp.UnitTests.Application.Specimens.Setup;

[TestFixture]
public sealed class SpecimenTypeHandlersTests : SpecimenTestBase
{
    private SpecimenTypeHandlers Handlers => new(Repository.Object, Clock);

    [Test]
    public async Task Create_NewName_AddsItTrimmed()
    {
        SpecimenType? added = null;
        Repository.Setup(r => r.AddTypeAsync(It.IsAny<SpecimenType>(), Cts.Token)).Callback((SpecimenType t, CancellationToken _) => added = t);

        var response = await Handlers.Handle(new CreateSpecimenTypeCommand("  Urine ", 2), Cts.Token);

        added!.Name.Should().Be("Urine");
        added.CreatedAt.Should().Be(Now);
        response.CanBeDeleted.Should().BeTrue();
    }

    [Test]
    public async Task Create_NameTaken_Throws409()
    {
        Repository.Setup(r => r.TypeNameExistsAsync("Urine", null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => Handlers.Handle(new CreateSpecimenTypeCommand("Urine", 2), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(SpecimenTypeHandlers.NameTaken("Urine"));
    }

    [Test]
    public async Task Update_ChangesNameAndDays()
    {
        Repository.Setup(r => r.GetTypesInUseAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HashSet<Guid>());

        var response = await Handlers.Handle(new UpdateSpecimenTypeCommand(Biopsy.Id, "Skin biopsy", 21), Cts.Token);

        Biopsy.Name.Should().Be("Skin biopsy");
        Biopsy.ExpectedTurnaroundDays.Should().Be(21);
        Repository.Verify(r => r.UpdateTypeAsync(Biopsy, Cts.Token), Times.Once);
        response.ExpectedTurnaroundDays.Should().Be(21);
    }

    [Test]
    public async Task ArchiveAndRestore_SetTheFlag()
    {
        Repository.Setup(r => r.GetTypesInUseAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HashSet<Guid> { Biopsy.Id });

        (await Handlers.Handle(new SetSpecimenTypeArchivedCommand(Biopsy.Id, true), Cts.Token)).IsArchived.Should().BeTrue();
        (await Handlers.Handle(new SetSpecimenTypeArchivedCommand(Biopsy.Id, false), Cts.Token)).Should()
            .BeEquivalentTo(new { IsArchived = false, CanBeDeleted = false });
    }

    [Test]
    public async Task Delete_InUse_Throws409_ElseDeletes()
    {
        Repository.Setup(r => r.GetTypesInUseAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HashSet<Guid> { Biopsy.Id });
        var act = () => Handlers.Handle(new DeleteSpecimenTypeCommand(Biopsy.Id), Cts.Token);
        await act.Should().ThrowAsync<ConflictException>().WithMessage("*Archive it instead*");

        Repository.Setup(r => r.GetTypesInUseAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HashSet<Guid>());
        await Handlers.Handle(new DeleteSpecimenTypeCommand(Biopsy.Id), Cts.Token);
        Repository.Verify(r => r.DeleteTypeAsync(Biopsy.Id, Cts.Token), Times.Once);
    }

    [Test]
    public async Task Get_MarksTheOnesInUse()
    {
        var urine = Urine();
        Repository.Setup(r => r.GetTypesAsync(true, It.IsAny<CancellationToken>())).ReturnsAsync([Biopsy, urine]);
        Repository.Setup(r => r.GetTypesInUseAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HashSet<Guid> { Biopsy.Id });

        var types = await Handlers.Handle(new GetSpecimenTypesQuery(true), Cts.Token);

        types.Select(t => t.CanBeDeleted).Should().Equal(false, true);
    }

    [Test]
    public async Task Unknown_Throws404()
    {
        var act = () => Handlers.Handle(new SetSpecimenTypeArchivedCommand(Guid.NewGuid(), true), Cts.Token);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task CreateAndUpdate_KeepTheSnomedCode_Trimmed()
    {
        SpecimenType? added = null;
        Repository.Setup(r => r.AddTypeAsync(It.IsAny<SpecimenType>(), It.IsAny<CancellationToken>()))
            .Callback((SpecimenType t, CancellationToken _) => added = t);

        var response = await Handlers.Handle(new CreateSpecimenTypeCommand("Urine", 2, " 122575003 "), Cts.Token);

        added!.SnomedCode.Should().Be("122575003");
        response.SnomedCode.Should().Be("122575003");
    }

    [TestCase("12345")]
    [TestCase("abc123456")]
    [TestCase("1234567890123456789")]
    public void Validator_ASnomedCodeThatIsNot6To18Digits_IsRefused(string code)
    {
        new CreateSpecimenTypeValidator().Validate(new CreateSpecimenTypeCommand("Urine", 2, code)).Errors
            .Should().ContainSingle(e => e.ErrorMessage == "A SNOMED CT code is 6 to 18 digits.");
    }

    [TestCase(0)]
    [TestCase(91)]
    public void Validator_TurnaroundOutside1To90_IsRefused(int days)
    {
        new CreateSpecimenTypeValidator().Validate(new CreateSpecimenTypeCommand("Urine", days)).IsValid.Should().BeFalse();
        new UpdateSpecimenTypeValidator().Validate(new UpdateSpecimenTypeCommand(Guid.NewGuid(), " ", 5)).IsValid.Should().BeFalse();
    }
}
