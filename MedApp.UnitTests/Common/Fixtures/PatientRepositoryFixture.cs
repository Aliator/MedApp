using MedApp.Application.Patients.Repositories;
using Moq;

namespace MedApp.UnitTests.Common.Fixtures;

public sealed class PatientRepositoryFixture
{
    public Mock<IPatientRepository> Mock { get; } = new();

    public IPatientRepository Object => Mock.Object;
}