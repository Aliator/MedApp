using MedApp.Application.Appointments.Queries.SearchAppointments;
using MedApp.Application.Common.Paging;
using MedApp.Application.Pathways.Timeline.Common;
using MedApp.Application.Pathways.Timeline.Queries.GetPatientTimeline;
using MedApp.Domain.Appointments;

namespace MedApp.Application.Appointments.Repositories;

/// <summary>
/// Appointments, with their patient, pathway and stage. Every change saves its history entries in the same save, so
/// the history can't miss a change.
/// <para>
/// Appointments loaded for update here share the request's DbContext with the pathway repository, so a pathway
/// change that tidies them up (<c>PathwayChange.CancelRequestedVisit</c>) saves them in the same
/// <c>SaveChangesAsync</c> as the pathway (<c>IPatientPathwayRepository.SaveAsync(PathwayChange, ...)</c>).
/// </para>
/// </summary>
public interface IAppointmentRepository
{
    /// <summary>An appointment with its patient, pathway and stage, read-only.</summary>
    Task<Appointment?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>An appointment with its patient, pathway and stage, tracked so <see cref="SaveAsync"/> saves changes to it.</summary>
    Task<Appointment?> GetForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Adds a new appointment and its history entries. Throws <see cref="Common.StageHasOpenAppointmentException"/>
    /// if its stage already has an open one (the database's unique index: another request added it after this one's
    /// check).
    /// </summary>
    Task AddAsync(Appointment appointment, IEnumerable<AppointmentEvent> events, CancellationToken ct);

    /// <summary>
    /// Saves changes to appointments loaded with <see cref="GetForUpdateAsync"/>, with their history entries. If
    /// someone else saved one of them since it was loaded (its row version changed), nothing is saved and it's a 409
    /// (<see cref="Common.AppointmentWorkflow.ChangedMeanwhileMessage"/>).
    /// </summary>
    Task SaveAsync(IEnumerable<AppointmentEvent> events, CancellationToken ct);

    /// <summary>The stage's Requested or Booked appointment, if it has one, read-only.</summary>
    Task<Appointment?> GetOpenForStageAsync(Guid stageId, CancellationToken ct);

    /// <summary>Every appointment for these stages, in any status, tracked for a pathway change.</summary>
    Task<IReadOnlyList<Appointment>> GetForStagesForUpdateAsync(IEnumerable<Guid> stageIds, CancellationToken ct);

    /// <summary>A pathway's Requested appointments, tracked for a pathway change.</summary>
    Task<IReadOnlyList<Appointment>> GetRequestedForPathwayForUpdateAsync(Guid pathwayId, CancellationToken ct);

    /// <summary>One page of appointments matching the query (see <c>AppointmentSearch</c>), with patients, pathways and stages.</summary>
    Task<PagedResult<Appointment>> SearchAsync(SearchAppointmentsQuery query, CancellationToken ct);

    /// <summary>
    /// A page of a patient's appointment history for their timeline and one entry more, if there is one (see
    /// <c>AppointmentTimelineSearch</c>), each with its pathway's title now.
    /// </summary>
    Task<IReadOnlyList<AppointmentTimelineRow>> GetPatientTimelineAsync(GetPatientTimelineQuery query, CancellationToken ct);

    /// <summary>How many Booked appointments a member of staff has starting after <paramref name="nowUtc"/>.</summary>
    Task<int> CountUpcomingForStaffAsync(Guid staffUserId, DateTime nowUtc, CancellationToken ct);
}
