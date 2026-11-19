using MedApp.Application.Pathways.Common;
using MedApp.Application.Patients.Common;
using MedApp.Domain.Specimens;

namespace MedApp.Application.Lab.Common;

/// <summary>What the lab can do with a sample and its lab case.</summary>
public static class LabSamples
{
    /// <summary>
    /// Whether the viewer can open a lab case for the sample by hand (the light demo's <c>canOpenCaseFor</c>): received,
    /// in no case, the viewer works lab cases, and the record isn't anonymised.
    /// </summary>
    public static bool CanOpenCaseFor(Specimen sample, PathwayActor viewer, Guid? labTeamId) =>
        sample.Status == SpecimenStatus.Received
        && sample.LabCaseId is null
        && LabCaseRules.WorksCases(viewer, labTeamId)
        && !PatientRules.IsAnonymised(sample.Patient);
}
