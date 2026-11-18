namespace MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Requests;

/// <summary>Edits a template. Only the fields that are set change. Existing pathways keep their copy of the stages.</summary>
public sealed class UpdatePathwayTemplateRequest
{
    public string? Name { get; set; }

    /// <summary>Moves it to another specialty (not an archived one). Only pathways created afterwards are in the new one.</summary>
    public Guid? SpecialtyId { get; set; }

    /// <summary>
    /// Its retention class: Standard, Oncology, Maternity or MentalHealth. Unlike the other fields, a longer class also
    /// raises the pathways already made from it; a shorter one never lowers them.
    /// </summary>
    public string? RetentionClass { get; set; }

    /// <summary>The template's stages in order; the same definition may appear more than once.</summary>
    public List<Guid>? StageDefinitionIdsInOrder { get; set; }

    /// <summary>
    /// Sent with the stage list: the position (from 1) of the stage whose finishing stops a referral's waiting-time
    /// clock, or null for none. Only new pathways take it.
    /// </summary>
    public int? ClockStopStageOrder { get; set; }

    /// <summary>
    /// Each stage's options, one for each of <see cref="StageDefinitionIdsInOrder"/> in the same order (only with it);
    /// left out with a new stage list, every stage is plain.
    /// </summary>
    public List<PathwayTemplateStageOptionsRequest>? StageOptions { get; set; }
}
