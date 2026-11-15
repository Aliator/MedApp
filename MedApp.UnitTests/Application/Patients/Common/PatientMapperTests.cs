using FluentAssertions;
using MedApp.Application.Patients.Common;
using MedApp.Contracts.Patients.Responses;
using MedApp.UnitTests.Common.Constants;

namespace MedApp.UnitTests.Application.Patients.Common;

[TestFixture]
public sealed class PatientMapperTests
{
    [Test]
    public void ToResponse_Patient_ReturnsTheirDetails()
    {
        var patient = PatientsTestConstants.CreateValidPatient();

        var response = PatientMapper.ToResponse(patient);

        response.Should().Be(new PatientResponse(patient.Id, patient.FirstName, patient.LastName, patient.DateOfBirth, patient.Email));
    }
}
