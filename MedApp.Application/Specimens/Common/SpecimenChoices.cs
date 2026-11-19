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
    public const string TypeMessage = "Choose a sample type that isn't archived.";
    public const string LabTestMessage = "Choose a lab test that isn't archived, or none.";

    /// <summary>
    /// The requesting clinician must hold this: SignOffResults, the permission that makes someone a responsible
    /// clinician (decision 81), as they get the result to sign off.
    /// </summary>
    public const Permission RequestingClinicianPermission = Permission.SignOffResults;

    public const string RequestingUserNotClinicianMessage =
        "Choose a clinician whose roles allow signing off results: they'll get the result.";

    public static async Task<(SpecimenType Type, Domain.Lab.LabTest? LabTest)> CheckAsync(
        ISpecimenRepository repository,
        IPermissionHolders permissionHolders,
        Guid typeId,
        Guid? labTestId,
        Guid requestingUserId,
        Specimen? existing,
        CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();

        var type = await repository.GetTypeAsync(typeId, ct);
        if (type is null || (type.IsArchived && existing?.SpecimenTypeId != type.Id))
            failures.Add(new ValidationFailure("SpecimenTypeId", TypeMessage));

        // The test is optional and never chosen for the clinician (decision 191); an archived one only if it already had it.
        Domain.Lab.LabTest? test = null;
        if (labTestId is { } testId && testId != Guid.Empty)
        {
            test = await repository.GetLabTestAsync(testId, ct);
            if (test is null || (test.IsArchived && existing?.LabTestId != test.Id))
                failures.Add(new ValidationFailure("LabTestId", LabTestMessage));
        }

        // Someone already named keeps being allowed (their roles may have changed since), as assignees are.
        if (existing?.RequestingUserId != requestingUserId)
        {
            var holders = await permissionHolders.WhoHoldsAsync([requestingUserId], RequestingClinicianPermission, ct);
            if (!holders.Contains(requestingUserId))
                failures.Add(new ValidationFailure("RequestingUserId", RequestingUserNotClinicianMessage));
        }

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return (type!, test);
    }
}
