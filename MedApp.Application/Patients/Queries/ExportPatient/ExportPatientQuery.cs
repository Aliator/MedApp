using MedApp.Contracts.Patients.Responses;
using MediatR;

namespace MedApp.Application.Patients.Queries.ExportPatient;

/// <summary>Everything held about a patient, for a subject access request (admins only). Recorded in the activity log.</summary>
public sealed record ExportPatientQuery(Guid PatientId) : IRequest<PatientExportResponse>;
