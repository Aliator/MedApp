using MedApp.Application.Specialties.Common;
using MedApp.Application.Specialties.Repositories;
using MedApp.Domain.Specialties;
using Moq;

namespace MedApp.UnitTests.Common.Constants;

/// <summary>Synthetic specialties for the tests of specialties, template specialties and staff memberships.</summary>
public static class SpecialtiesTestConstants
{
    public const string SpecialtyName = "Synthetic dermatology";
    public const string OtherSpecialtyName = "Synthetic urology";
    public const string ThirdSpecialtyName = "Synthetic biomechanics";
    public const string StaffUsername = "s.synthetic";

    public static readonly Guid StaffUserId = Guid.Parse("0000005a-0000-0000-0000-000000000001");
    public static readonly DateTime CreatedAtUtc = new(2026, 11, 2, 9, 30, 0, DateTimeKind.Utc);
    public static readonly DateTime NowUtc = new(2026, 11, 18, 10, 15, 0, DateTimeKind.Utc);

    public static Specialty CreateSpecialty(
        string name = SpecialtyName, bool isArchived = false, int version = 3, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = name,
        IsArchived = isArchived,
        Version = version,
        CreatedAt = CreatedAtUtc,
        LastUpdated = CreatedAtUtc
    };

    /// <summary>A repository mock whose <c>GetByIdsAsync</c> and <c>GetAsync</c> find these specialties.</summary>
    public static Mock<ISpecialtyRepository> RepositoryWith(params Specialty[] specialties)
    {
        var repository = new Mock<ISpecialtyRepository>();
        repository
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                (IReadOnlyList<Specialty>)specialties.Where(s => ids.Contains(s.Id)).ToList());
        foreach (var specialty in specialties)
            repository.Setup(r => r.GetAsync(specialty.Id, It.IsAny<CancellationToken>())).ReturnsAsync(specialty);
        repository
            .Setup(r => r.GetUsageAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, SpecialtyUsage>());
        return repository;
    }
}
