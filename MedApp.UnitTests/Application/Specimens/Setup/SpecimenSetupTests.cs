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

    [TestCase(0)]
    [TestCase(91)]
    public void Validator_TurnaroundOutside1To90_IsRefused(int days)
    {
        new CreateSpecimenTypeValidator().Validate(new CreateSpecimenTypeCommand("Urine", days)).IsValid.Should().BeFalse();
        new UpdateSpecimenTypeValidator().Validate(new UpdateSpecimenTypeCommand(Guid.NewGuid(), " ", 5)).IsValid.Should().BeFalse();
    }
}

[TestFixture]
public sealed class LaboratoryHandlersTests : SpecimenTestBase
{
    private LaboratoryHandlers Handlers => new(Repository.Object, Clock);

    [Test]
    public async Task CreateUpdateArchive_Work()
    {
        Laboratory? added = null;
        Repository.Setup(r => r.AddLaboratoryAsync(It.IsAny<Laboratory>(), Cts.Token)).Callback((Laboratory l, CancellationToken _) => added = l);
        Repository.Setup(r => r.GetLaboratoriesInUseAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HashSet<Guid>());

        await Handlers.Handle(new CreateLaboratoryCommand(" Partner lab ", false), Cts.Token);
        added!.Name.Should().Be("Partner lab");

        var updated = await Handlers.Handle(new UpdateLaboratoryCommand(InHouse.Id, "Centre lab", false), Cts.Token);
        updated.Should().BeEquivalentTo(new { Name = "Centre lab", UsesMedApp = false });

        (await Handlers.Handle(new SetLaboratoryArchivedCommand(InHouse.Id, true), Cts.Token)).IsArchived.Should().BeTrue();
    }

    [Test]
    public async Task Create_NameTaken_Throws409()
    {
        Repository.Setup(r => r.LaboratoryNameExistsAsync(InHouseLabName, null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var act = () => Handlers.Handle(new CreateLaboratoryCommand(InHouseLabName, true), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Test]
    public async Task Delete_InUse_Throws409()
    {
        Repository.Setup(r => r.GetLaboratoriesInUseAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HashSet<Guid> { InHouse.Id });

        var act = () => Handlers.Handle(new DeleteLaboratoryCommand(InHouse.Id), Cts.Token);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(LaboratoryHandlers.InUse(InHouse));
    }

    [Test]
    public async Task Get_ListsThem()
    {
        Repository.Setup(r => r.GetLaboratoriesAsync(false, It.IsAny<CancellationToken>())).ReturnsAsync([InHouse]);
        Repository.Setup(r => r.GetLaboratoriesInUseAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new HashSet<Guid>());

        (await Handlers.Handle(new GetLaboratoriesQuery(false), Cts.Token)).Should().ContainSingle(l => l.Name == InHouseLabName);
    }

    [Test]
    public void Validator_NameTooLong_IsRefused()
    {
        new CreateLaboratoryValidator().Validate(new CreateLaboratoryCommand(new string('x', 201), false)).IsValid.Should().BeFalse();
    }
}
