using MedApp.Application.Procedures.Repositories;
using MedApp.Domain.Procedures.PatientProcedures;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

public sealed class PatientProcedureRepository(MedAppDbContext context) : IPatientProcedureRepository
{
    public async Task AddAsync(PatientProcedure procedure, CancellationToken ct)
    {
        context.PatientProcedures.Add(procedure);
        await context.SaveChangesAsync(ct);
    }

    public async Task<PatientProcedure?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var procedure = await context.PatientProcedures
            .AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Assignments)
            .Include(x => x.Stages)
            .ThenInclude(x => x.StageDefinition)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (procedure is null)
        {
            return null;
        }

        procedure.Stages = procedure.Stages.OrderBy(x => x.StageOrder).ToList();
        return procedure;
    }

    public async Task<IEnumerable<PatientProcedure>> GetAllAsync(CancellationToken ct)
    {
        var procedures = await context.PatientProcedures
            .AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Assignments)
            .Include(x => x.Stages)
            .ThenInclude(x => x.StageDefinition)
            .ToListAsync(ct);

        foreach (var procedure in procedures)
        {
            procedure.Stages = procedure.Stages.OrderBy(x => x.StageOrder).ToList();
        }

        return procedures;
    }
    
    public async Task<IEnumerable<PatientProcedure>> GetAllForUserAsync(Guid userId, CancellationToken ct)
    {
        var procedures = await context.PatientProcedures
            .AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Assignments)
            .Include(x => x.Stages)
            .ThenInclude(x => x.StageDefinition)
            .Where(x => x.Assignments.Any(assignment => assignment.UserId == userId))
            .ToListAsync(ct);

        foreach (var procedure in procedures)
        {
            procedure.Stages = procedure.Stages.OrderBy(x => x.StageOrder).ToList();
        }

        return procedures;
    }

    public async Task<IEnumerable<PatientProcedure>> GetAllForPatientAsync(Guid patientId, CancellationToken ct)
    {
        var procedures = await context.PatientProcedures
            .AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Assignments)
            .Include(x => x.Stages)
            .ThenInclude(x => x.StageDefinition)
            .Where(x => x.PatientId == patientId)
            .ToListAsync(ct);

        foreach (var procedure in procedures)
        {
            procedure.Stages = procedure.Stages.OrderBy(x => x.StageOrder).ToList();
        }

        return procedures;
    }

    public async Task UpdateAsync(PatientProcedure procedure, CancellationToken ct)
    {
        var existing = await context.PatientProcedures
            .Include(x => x.Stages)
            .Include(x => x.Assignments)
            .FirstOrDefaultAsync(x => x.Id == procedure.Id, ct);

        if (existing is null)
        {
            return;
        }

        existing.Title = procedure.Title;
        existing.Notes = procedure.Notes;
        existing.DueDateUtc = procedure.DueDateUtc;
        existing.Priority = procedure.Priority;
        existing.Status = procedure.Status;
        existing.LastUpdated = procedure.LastUpdated;

        context.Set<PatientProcedureStage>().RemoveRange(existing.Stages);
        existing.Stages = procedure.Stages;

        context.Set<PatientProcedureAssignment>().RemoveRange(existing.Assignments);
        existing.Assignments = procedure.Assignments;

        await context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var procedure = await context.PatientProcedures.FindAsync([id], ct);

        if (procedure is null)
        {
            return;
        }

        context.PatientProcedures.Remove(procedure);
        await context.SaveChangesAsync(ct);
    }
}