using FluentValidation;
using FluentValidation.Results;
using MedApp.Application.Common.Identity;
using MedApp.Application.Specimens.Repositories;
using MedApp.Domain.Specimens;

namespace MedApp.Application.Specimens.Common;

/// <summary>
/// Checks what a specimen being recorded or edited names: its type and lab (existing, and not archived unless the
/// specimen already has it) and its requesting clinician (someone whose roles allow working on pathways, as an
/// assignee must). Each refusal is a 400 on its field.
/// </summary>
public static class SpecimenChoices
{
    public const string TypeMessage = "Choose a specimen type that isn't archived.";
    public const string LaboratoryMessage = "Choose a lab that isn't archived.";

    /// <summary>
    /// The requesting clinician must hold this. Results sign-off (PR 14) changes it to SignOffResults, the permission
    /// that makes someone a responsible clinician (decision 81).
    /// </summary>
    public const Permission RequestingClinicianPermission = Permission.WorkPathways;

    public const string RequestingUserNotClinicianMessage =
        "Choose a clinician whose roles allow working on pathways: they'll get the result.";

    public static async Task<(SpecimenType Type, Laboratory Laboratory)> CheckAsync(
        ISpecimenRepository repository,
        IPermissionHolders permissionHolders,
        Guid typeId,
        Guid laboratoryId,
        Guid requestingUserId,
        Specimen? existing,
        CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();

        var type = await repository.GetTypeAsync(typeId, ct);
        if (type is null || (type.IsArchived && existing?.SpecimenTypeId != type.Id))
            failures.Add(new ValidationFailure("SpecimenTypeId", TypeMessage));

        var laboratory = await repository.GetLaboratoryAsync(laboratoryId, ct);
        if (laboratory is null || (laboratory.IsArchived && existing?.LaboratoryId != laboratory.Id))
            failures.Add(new ValidationFailure("LaboratoryId", LaboratoryMessage));

        // Someone already named keeps being allowed (their roles may have changed since), as assignees are.
        if (existing?.RequestingUserId != requestingUserId)
        {
            var holders = await permissionHolders.WhoHoldsAsync([requestingUserId], RequestingClinicianPermission, ct);
            if (!holders.Contains(requestingUserId))
                failures.Add(new ValidationFailure("RequestingUserId", RequestingUserNotClinicianMessage));
        }

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return (type!, laboratory!);
    }
}
