using MedApp.Contracts.Patients.Responses;
using MediatR;

namespace MedApp.Application.Patients.Commands.AnonymisePatient;

/// <summary>
/// Anonymises a patient's record (admins only; see <c>PatientAnonymiser</c>), confirmed by typing the patient's full
/// name (<paramref name="Confirmation"/>), with a reason for the activity log.
/// </summary>
public sealed record AnonymisePatientCommand(
    Guid PatientId,
    string Reason,
    string Confirmation
) : IRequest<PatientResponse>;
