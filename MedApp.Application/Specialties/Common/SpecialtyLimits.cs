using MedApp.Domain.Specialties;

namespace MedApp.Application.Specialties.Common;

public static class SpecialtyLimits
{
    public const int MaxNameLength = Specialty.MaxNameLength;

    /// <summary>The most specialties one member of staff can be given at once (far more than any centre has).</summary>
    public const int MaxPerUser = 100;
}
