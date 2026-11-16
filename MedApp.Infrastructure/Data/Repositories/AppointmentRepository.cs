using MedApp.Application.Appointments.Common;
using MedApp.Application.Appointments.Queries.SearchAppointments;
using MedApp.Application.Appointments.Repositories;
using MedApp.Application.Common.Paging;
using MedApp.Application.Procedures.Timeline.Common;
using MedApp.Application.Procedures.Timeline.Queries.GetPatientTimeline;
using MedApp.Domain.Appointments;
using MedApp.Infrastructure.Data.Configurations.Appointments;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>
/// Appointments. Changes are made to a tracked appointment (<see cref="GetForUpdateAsync"/>) and saved with their
/// history entries in one <c>SaveChangesAsync</c>. Appointments loaded for a procedure change share the request's
/// DbContext with <c>PatientProcedureRepository</c>, which saves them with the procedure.
/// </summary>
public sealed class AppointmentRepository(MedAppDbContext context) : IAppointmentRepository
{
    public async Task<Appointment?> GetByIdAsync(Guid id, CancellationToken ct) =>
        await WithDetails(context.Appointments.AsNoTracking()).FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<Appointment?> GetForUpdateAsync(Guid id, CancellationToken ct) =>
        await WithDetails(context.Appointments).FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task AddAsync(Appointment appointment, IEnumerable<AppointmentEvent> events, CancellationToken ct)
    {
        var history = events.ToList();
        context.Appointments.Add(appointment);
        context.AppointmentEvents.AddRange(history);
        try
        {
            await SaveAsync(ct);
        }
        catch (DbUpdateException e) when (appointment.StageId is { } stageId
                                          && e.IsViolationOf(AppointmentConfiguration.OpenPerStageIndex))
        {
            // Another request gave the stage its open appointment first. Forget this one, so nothing saves it later.
            context.Entry(appointment).State = EntityState.Detached;
            foreach (var entry in history)
                context.Entry(entry).State = EntityState.Detached;
            throw new StageHasOpenAppointmentException(stageId);
        }
    }

    public async Task SaveAsync(IEnumerable<AppointmentEvent> events, CancellationToken ct)
    {
        context.AppointmentEvents.AddRange(events);
        await SaveAsync(ct);
    }

    /// <summary>
    /// Saves, refusing (409) a change to an appointment someone else saved since it was read (its row version), so
    /// two people changing one appointment at once can't silently overwrite each other.
    /// </summary>
    private Task SaveAsync(CancellationToken ct) =>
        context.SaveChangesOrConflictAsync(AppointmentWorkflow.ChangedMeanwhileMessage, ct);

    public async Task<Appointment?> GetOpenForStageAsync(Guid stageId, CancellationToken ct) =>
        await context.Appointments
            .AsNoTracking()
            .Where(a => a.StageId == stageId
                        && (a.Status == AppointmentStatus.Requested || a.Status == AppointmentStatus.Booked))
            .OrderBy(a => a.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Appointment>> GetForStagesForUpdateAsync(IEnumerable<Guid> stageIds, CancellationToken ct)
    {
        var ids = stageIds.Distinct().ToList();
        return await context.Appointments
            .Where(a => a.StageId != null && ids.Contains(a.StageId.Value))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Appointment>> GetRequestedForProcedureForUpdateAsync(Guid procedureId, CancellationToken ct) =>
        await context.Appointments
            .Where(a => a.ProcedureId == procedureId && a.Status == AppointmentStatus.Requested)
            .ToListAsync(ct);

    public async Task<PagedResult<Appointment>> SearchAsync(SearchAppointmentsQuery query, CancellationToken ct)
    {
        var filtered = context.Appointments.AsNoTracking().Filter(query);

        var totalCount = await filtered.CountAsync(ct);

        var page = await WithDetails(filtered
                .Sort(query.Sort, query.Ascending)
                .Skip(Paging.Skip(query.Page, query.PageSize))
                .Take(query.PageSize))
            .AsSplitQuery()
            .ToListAsync(ct);

        return new PagedResult<Appointment>(page, totalCount);
    }

    public async Task<IReadOnlyList<AppointmentTimelineRow>> GetPatientTimelineAsync(GetPatientTimelineQuery query, CancellationToken ct) =>
        await AppointmentTimelineSearch
            .Page(context.AppointmentEvents.AsNoTracking(), context.PatientProcedures.AsNoTracking(), query)
            .ToListAsync(ct);

    public async Task<int> CountUpcomingForStaffAsync(Guid staffUserId, DateTime nowUtc, CancellationToken ct) =>
        await context.Appointments.CountAsync(
            a => a.StaffUserId == staffUserId && a.Status == AppointmentStatus.Booked && a.StartUtc > nowUtc, ct);

    private static IQueryable<Appointment> WithDetails(IQueryable<Appointment> appointments) =>
        appointments
            .Include(a => a.Patient)
            .Include(a => a.Procedure)
            .Include(a => a.Stage);
}
