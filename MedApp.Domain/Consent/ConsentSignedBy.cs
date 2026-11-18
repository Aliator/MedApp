namespace MedApp.Domain.Consent;

/// <summary>
/// Who signed a consent form, as the clinician recording it states (decision 145). Stored by number, so never renumber
/// it. MedApp records the statement; it never assesses capacity or whether the consent is valid.
/// </summary>
public enum ConsentSignedBy
{
    /// <summary>The patient themselves.</summary>
    Patient = 0,

    /// <summary>A person with parental responsibility, for a child.</summary>
    PersonWithParentalResponsibility = 1,

    /// <summary>No signature: a best-interests decision recorded by a clinician.</summary>
    BestInterestsDecision = 2
}
