using MedApp.Application.Common.Exceptions;
using MedApp.Application.Letters.Common;
using MedApp.Application.Letters.Repositories;
using MedApp.Domain.Letters;
using MedApp.Infrastructure.Data.Configurations.Letters;
using Microsoft.EntityFrameworkCore;

namespace MedApp.Infrastructure.Data.Repositories;

/// <summary>Letter templates. A setup list: the handler sorts it once its specialties' names are known.</summary>
public sealed class LetterTemplateRepository(MedAppDbContext context) : ILetterTemplateRepository
{
    public async Task<IReadOnlyList<LetterTemplate>> GetAllAsync(bool includeArchived, CancellationToken ct) =>
        await context.LetterTemplates
            .AsNoTracking()
            .Where(t => includeArchived || !t.IsArchived)
            .OrderBy(t => t.Name)
            .ThenBy(t => t.Id)
            .ToListAsync(ct);

    public async Task<LetterTemplate?> GetAsync(Guid id, CancellationToken ct) =>
        await context.LetterTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<bool> NameExistsAsync(string name, Guid? specialtyId, Guid? exceptId, CancellationToken ct)
    {
        var lower = name.ToLower();
        return await context.LetterTemplates.AnyAsync(
            t => t.Name.ToLower() == lower && t.SpecialtyId == specialtyId && (exceptId == null || t.Id != exceptId), ct);
    }

    public async Task AddAsync(LetterTemplate template, CancellationToken ct)
    {
        context.LetterTemplates.Add(template);
        try
        {
            await context.SaveChangesAsync(ct);
        }
        // Another was given this name in this specialty meanwhile.
        catch (DbUpdateException e) when (e.IsViolationOf(LetterTemplateConfiguration.NameIndex))
        {
            context.Entry(template).State = EntityState.Detached;
            throw new ConflictException(LetterTemplateRules.NameTakenMessage(template.Name));
        }
    }

    public async Task UpdateAsync(LetterTemplate template, int originalVersion, CancellationToken ct)
    {
        var entry = context.LetterTemplates.Update(template);
        // The version it was read at: the update matches only that row, so a change saved meanwhile is a 409.
        entry.Property(t => t.Version).OriginalValue = originalVersion;
        try
        {
            await context.SaveChangesOrConflictAsync(LetterTemplateRules.ChangedMeanwhileMessage, ct);
        }
        catch (DbUpdateException e) when (e.IsViolationOf(LetterTemplateConfiguration.NameIndex))
        {
            entry.State = EntityState.Detached;
            throw new ConflictException(LetterTemplateRules.NameTakenMessage(template.Name));
        }
    }
}
