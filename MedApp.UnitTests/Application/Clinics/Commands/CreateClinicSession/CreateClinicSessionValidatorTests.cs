using FluentValidation.TestHelper;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Calendar.Repositories;
using MedApp.Application.Clinics.Commands.CreateClinicSession;
using MedApp.Application.Clinics.Common;
using MedApp.Domain.Calendar;
using MedApp.UnitTests.Common.Fixtures;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;
using static MedApp.UnitTests.Common.Constants.ClinicsTestConstants;

namespace MedApp.UnitTests.Application.Clinics.Commands.CreateClinicSession;

[TestFixture]
public sealed class CreateClinicSessionValidatorTests
{
    private CreateClinicSessionValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        var closedDays = new Mock<IClosedDayRepository>();
        closedDays.Setup(r => r.GetAsync(It.IsAny<DateOnly?>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<ClosedDay>());
        var hours = new Mock<IWorkingHoursRepository>();
        hours.Setup(r => r.GetAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(WorkingTimeSet.None);
        _validator = new CreateClinicSessionValidator(new WorkingDays(closedDays.Object, hours.Object), new FixedTimeProvider(Now));
    }

    private static CreateClinicSessionCommand Command() =>
        new(StaffUserId, Guid.NewGuid(), SpecialtyId, At(NextMonday, 9), At(NextMonday, 12), 20, AdminActor);

    [Test]
    public async Task Validate_FutureSessionInsideTheWorkingDay_Passes() =>
        (await _validator.TestValidateAsync(Command())).ShouldNotHaveAnyValidationErrors();

    [Test]
    public async Task Validate_MissingIds_Refused()
    {
        var result = await _validator.TestValidateAsync(Command() with { StaffUserId = Guid.Empty, RoomId = Guid.Empty, SpecialtyId = Guid.Empty });

        result.ShouldHaveValidationErrorFor(x => x.StaffUserId);
        result.ShouldHaveValidationErrorFor(x => x.RoomId);
        result.ShouldHaveValidationErrorFor(x => x.SpecialtyId);
    }

    [Test]
    public async Task Validate_InThePast_Refused() =>
        (await _validator.TestValidateAsync(Command() with { StartUtc = At(Today, 8), EndUtc = At(Today, 9) }))
            .ShouldHaveValidationErrorFor(x => x.StartUtc).WithErrorMessage(ClinicRules.InThePast);

    [Test]
    public async Task Validate_OnADayTheyDontWork_Refused() =>
        (await _validator.TestValidateAsync(Command() with { StartUtc = At(NextSaturday, 9), EndUtc = At(NextSaturday, 12) }))
            .ShouldHaveValidationErrorFor(x => x.StartUtc);

    [Test]
    public async Task Validate_SlotsThatDontDivide_Refused() =>
        (await _validator.TestValidateAsync(Command() with { SlotMinutes = 25 }))
            .ShouldHaveValidationErrorFor(x => x.SlotMinutes).WithErrorMessage(ClinicRules.SlotsDontDivide);
}
