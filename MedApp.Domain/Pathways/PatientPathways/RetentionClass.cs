namespace MedApp.Domain.Pathways.PatientPathways;

/// <summary>
/// The records management class of a pathway template, copied onto each pathway made from it: it sets how long its
/// patients' records are kept before they're reviewed (the years are settings, <c>Retention:Classes</c>). Standard
/// keeps the adult or child rule. Nothing is ever deleted automatically. Stored as its number, so never renumber these.
/// </summary>
public enum RetentionClass
{
    /// <summary>The adult or child period alone (the default).</summary>
    Standard = 0,

    Oncology = 1,

    Maternity = 2,

    MentalHealth = 3
}
