namespace MedApp.Application.Demographics.Common;

/// <summary>Which demographics lookup the forms offer (the <c>Pds</c> section of the configuration).</summary>
public sealed class PdsOptions
{
    public const string Section = "Pds";

    /// <summary><see cref="PdsMode.Off"/> unless set: there's no lookup, and the forms offer none.</summary>
    public PdsMode Mode { get; set; } = PdsMode.Off;
}

public enum PdsMode
{
    /// <summary>No lookup (the default).</summary>
    Off,

    /// <summary>
    /// The offline stub: a few synthetic 999 numbers, for demonstrations and tests. Never real people, and never the
    /// real PDS.
    /// </summary>
    Offline
}
