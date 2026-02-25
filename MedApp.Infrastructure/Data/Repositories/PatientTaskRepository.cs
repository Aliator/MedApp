using MedApp.Application.Tasks.Repositories;
using MedApp.Domain.Tasks.PatientTasks;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

public sealed class PatientTaskRepository(MedAppDbContext context) : IPatientTaskRepository
{
    public async Task AddAsync(PatientTask task, CancellationToken ct)
    {
        context.PatientTasks.Add(task);
        await context.SaveChangesAsync(ct);
    }

    public async Task<PatientTask?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var task = await context.PatientTasks
            .AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Assignments)
            .Include(x => x.Stages)
            .ThenInclude(x => x.StageDefinition)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (task is null)
        {
            return null;
        }

        task.Stages = task.Stages.OrderBy(x => x.StageOrder).ToList();
        return task;
    }

    public async Task<IEnumerable<PatientTask>> GetAllAsync(CancellationToken ct)
    {
        var tasks = await context.PatientTasks
            .AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Assignments)
            .Include(x => x.Stages)
            .ThenInclude(x => x.StageDefinition)
            .ToListAsync(ct);

        foreach (var task in tasks)
        {
            task.Stages = task.Stages.OrderBy(x => x.StageOrder).ToList();
        }

        return tasks;
    }
    
    public async Task<IEnumerable<PatientTask>> GetAllForUserAsync(Guid userId, CancellationToken ct)
    {
        var tasks = await context.PatientTasks
            .AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Assignments)
            .Include(x => x.Stages)
            .ThenInclude(x => x.StageDefinition)
            .Where(x => x.Assignments.Any(assignment => assignment.UserId == userId))
            .ToListAsync(ct);

        foreach (var task in tasks)
        {
            task.Stages = task.Stages.OrderBy(x => x.StageOrder).ToList();
        }

        return tasks;
    }

    public async Task<IEnumerable<PatientTask>> GetAllForPatientAsync(Guid patientId, CancellationToken ct)
    {
        var tasks = await context.PatientTasks
            .AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Assignments)
            .Include(x => x.Stages)
            .ThenInclude(x => x.StageDefinition)
            .Where(x => x.PatientId == patientId)
            .ToListAsync(ct);

        foreach (var task in tasks)
        {
            task.Stages = task.Stages.OrderBy(x => x.StageOrder).ToList();
        }

        return tasks;
    }

    public async Task UpdateAsync(PatientTask task, CancellationToken ct)
    {
        var existing = await context.PatientTasks
            .Include(x => x.Stages)
            .Include(x => x.Assignments)
            .FirstOrDefaultAsync(x => x.Id == task.Id, ct);

        if (existing is null)
        {
            return;
        }

        existing.Title = task.Title;
        existing.Notes = task.Notes;
        existing.DueDateUtc = task.DueDateUtc;
        existing.Priority = task.Priority;
        existing.Status = task.Status;
        existing.LastUpdated = task.LastUpdated;

        context.Set<PatientTaskStage>().RemoveRange(existing.Stages);
        existing.Stages = task.Stages;

        context.Set<PatientTaskAssignment>().RemoveRange(existing.Assignments);
        existing.Assignments = task.Assignments;

        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var task = await context.PatientTasks.FindAsync([id], ct);

        if (task is null)
        {
            return;
        }

        context.PatientTasks.Remove(task);
        await context.SaveChangesAsync(ct);
    }
}