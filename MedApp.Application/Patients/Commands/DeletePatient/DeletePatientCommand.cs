using MediatR;

namespace MedApp.Application.Patients.Commands.DeletePatient;

public sealed record DeletePatientCommand(Guid Id) : IRequest;