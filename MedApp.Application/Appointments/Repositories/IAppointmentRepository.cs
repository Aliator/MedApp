using MedApp.Application.Appointments.Queries.SearchAppointments;
using MedApp.Application.Common.Paging;
using MedApp.Application.Procedures.Timeline.Common;
using MedApp.Application.Procedures.Timeline.Queries.GetPatientTimeline;
using MedApp.Domain.Appointments;

namespace MedApp.Application.Appointments.Repositories;

/// <summary>
/// Appointments, with their patient, procedure and stage. Every change saves its history entries in the same save, so
/// the history can't miss a change.
/// <para>
/// Appointments loaded for update here share the request's DbContext with the procedure repository, so a procedure
/// change that tidies them up (<c>ProcedureChange.CancelRequestedVisit</c>) saves them in the same
/// <c>SaveChangesAsync</c> as the procedure (<c>IPatientProcedureRepository.SaveAsync(ProcedureChange, ...)</c>).
/// </para>
/// </summary>
public interface IAppointmentRepository
{
    /// <summary>An appointment with its patient, procedure and stage, read-only.</summary>
    Task<Appointment?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>An appointment with its patient, procedure and stage, tracked so <see cref="SaveAsync"/> saves changes to it.</summary>
    Task<Appointment?> GetForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>Adds a new appointment and its history entries.</summary>
    Task AddAsync(Appointment appointment, IEnumerable<AppointmentEvent> events, CancellationToken ct);

    /// <summary>Saves changes to appointments loaded with <see cref="GetForUpdateAsync"/>, with their history entries.</summary>
    Task SaveAsync(IEnumerable<AppointmentEvent> events, CancellationToken ct);

    /// <summary>The stage's Requested or Booked appointment, if it has one, read-only.</summary>
    Task<Appointment?> GetOpenForStageAsync(Guid stageId, CancellationToken ct);

    /// <summary>Every appointment for these stages, in any status, tracked for a procedure change.</summary>
    Task<IReadOnlyList<Appointment>> GetForStagesForUpdateAsync(IEnumerable<Guid> stageIds, CancellationToken ct);

    /// <summary>A procedure's Requested appointments, tracked for a procedure change.</summary>
    Task<IReadOnlyList<Appointment>> GetRequestedForProcedureForUpdateAsync(Guid procedureId, CancellationToken ct);

    /// <summary>One page of appointments matching the query (see <c>AppointmentSearch</c>), with patients, procedures and stages.</summary>
    Task<PagedResult<Appointment>> SearchAsync(SearchAppointmentsQuery query, CancellationToken ct);

    /// <summary>
    /// A page of a patient's appointment history for their timeline and one entry more, if there is one (see
    /// <c>AppointmentTimelineSearch</c>), each with its procedure's title now.
    /// </summary>
    Task<IReadOnlyList<AppointmentTimelineRow>> GetPatientTimelineAsync(GetPatientTimelineQuery query, CancellationToken ct);

    /// <summary>How many Booked appointments a member of staff has starting after <paramref name="nowUtc"/>.</summary>
    Task<int> CountUpcomingForStaffAsync(Guid staffUserId, DateTime nowUtc, CancellationToken ct);
}
