using FluentAssertions;
using MedApp.Application.Appointments.Common;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Patients.Common;
using MedApp.Domain.Appointments;
using MedApp.UnitTests.Common.Constants;
using Moq;
using static MedApp.UnitTests.Common.Constants.CalendarTestConstants;

namespace MedApp.UnitTests.Application.Appointments.Common;

[TestFixture]
public sealed class AppointmentRepositoryExtensionsTests
{
    private Mock<IAppointmentRepository> _repository = null!;
    private Mock<IIdentityReadService> _identity = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new Mock<IAppointmentRepository>();
        _identity = new Mock<IIdentityReadService>();
    }

    [Test]
    public async Task GetForUpdateOrThrowAsync_Exists_ReturnsItWithTheToken()
    {
        var appointment = CreateAppointment();
        using var cts = new CancellationTokenSource();
        _repository.Setup(r => r.GetForUpdateAsync(appointment.Id, cts.Token)).ReturnsAsync(appointment);

        (await _repository.Object.GetForUpdateOrThrowAsync(appointment.Id, cts.Token)).Should().BeSameAs(appointment);
    }

    [Test]
    public async Task GetForUpdateOrThrowAsync_Missing_ThrowsNotFound()
    {
        var id = Guid.NewGuid();

        var act = () => _repository.Object.GetForUpdateOrThrowAsync(id, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Appointment '{id}' was not found.");
    }

    [Test]
    public async Task GetStaffUsernameAsync_Booked_LooksUpTheStaffMember()
    {
        using var cts = new CancellationTokenSource();
        _identity.Setup(i => i.GetUsernamesByIdAsync(It.Is<IEnumerable<Guid>>(ids => ids.Single() == StaffUserId), cts.Token))
            .ReturnsAsync(new Dictionary<Guid, string> { [StaffUserId] = StaffUsername });

        (await _identity.Object.GetStaffUsernameAsync(CreateAppointment(), cts.Token)).Should().Be(StaffUsername);
    }

    [Test]
    public async Task GetStaffUsernameAsync_StaffMemberDeleted_IsNull()
    {
        _identity.Setup(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string>());

        (await _identity.Object.GetStaffUsernameAsync(CreateAppointment(), CancellationToken.None)).Should().BeNull();
    }

    [Test]
    public async Task GetStaffUsernameAsync_Requested_IsNullWithoutALookup()
    {
        (await _identity.Object.GetStaffUsernameAsync(CreateAppointment(AppointmentStatus.Requested), CancellationToken.None))
            .Should().BeNull();

        _identity.Verify(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task GetForUpdateOrThrowAsync_PatientRestricted_ThrowsConflict()
    {
        var appointment = GivenAppointmentFor(PrivacyTestConstants.CreateRestrictedPatient());

        var act = () => _repository.Object.GetForUpdateOrThrowAsync(appointment.Id, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.RestrictedMessage);
    }

    [Test]
    public async Task GetForUpdateOrThrowAsync_PatientAnonymised_ThrowsConflict()
    {
        var appointment = GivenAppointmentFor(PrivacyTestConstants.CreateAnonymisedPatient());

        var act = () => _repository.Object.GetForUpdateOrThrowAsync(appointment.Id, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.AnonymisedMessage);
    }

    [Test]
    public async Task GetForCancelOrThrowAsync_PatientRestricted_ReturnsItWithTheToken()
    {
        var appointment = GivenAppointmentFor(PrivacyTestConstants.CreateRestrictedPatient());
        using var cts = new CancellationTokenSource();
        _repository.Setup(r => r.GetForUpdateAsync(appointment.Id, cts.Token)).ReturnsAsync(appointment);

        (await _repository.Object.GetForCancelOrThrowAsync(appointment.Id, cts.Token)).Should().BeSameAs(appointment);
    }

    [Test]
    public async Task GetForCancelOrThrowAsync_PatientAnonymised_ThrowsConflict()
    {
        var appointment = GivenAppointmentFor(PrivacyTestConstants.CreateAnonymisedPatient());

        var act = () => _repository.Object.GetForCancelOrThrowAsync(appointment.Id, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>().WithMessage(PatientRules.AnonymisedMessage);
    }

    [Test]
    public async Task GetForCancelOrThrowAsync_Missing_ThrowsNotFound()
    {
        var id = Guid.NewGuid();

        var act = () => _repository.Object.GetForCancelOrThrowAsync(id, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"Appointment '{id}' was not found.");
    }

    /// <summary>An appointment for this patient, found by the repository.</summary>
    private Appointment GivenAppointmentFor(MedApp.Domain.Patients.Patient patient)
    {
        var appointment = CreateAppointment();
        appointment.Patient = patient;
        appointment.PatientId = patient.Id;
        _repository.Setup(r => r.GetForUpdateAsync(appointment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(appointment);
        return appointment;
    }
}
