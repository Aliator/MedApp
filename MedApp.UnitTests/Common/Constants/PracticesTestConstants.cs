using MedApp.Application.Practices.Common;
using MedApp.Application.Practices.Repositories;
using MedApp.Domain.Practices;
using Moq;

namespace MedApp.UnitTests.Common.Constants;

/// <summary>
/// Synthetic GP practices and contact details: invented names and streets, postcodes in the ZZ area (which doesn't
/// exist), mobiles from Ofcom's range for drama (07700 900000 to 900999), and ODS codes no practice has (Z9…).
/// </summary>
public static class PracticesTestConstants
{
    public const string PracticeName = "Testbrook Surgery";
    public const string OtherPracticeName = "Madeupham Medical Centre";
    public const string OdsCode = "Z99901";
    public const string OtherOdsCode = "Z99902";
    public const string AddressLine1 = "1 Imaginary Lane";
    public const string AddressLine2 = "Fictionton";
    public const string Postcode = "ZZ1 1ZZ";
    public const string PostcodeAsTyped = " zz11zz ";
    public const string Phone = "01632 960000";
    public const string SecureEmail = "testbrook.surgery@nhs.net";

    /// <summary>A practice that isn't in the directory, as typed on a patient ("Not listed here").</summary>
    public const string UnlistedName = "Nowhereville Health Centre";
    public const string UnlistedLine = "2 Pretend Road";
    public const string UnlistedPostcode = "ZZ9 9ZZ";
    public const string UnlistedPhone = "01632 960111";

    /// <summary>A mobile from the drama range, as typed and as stored.</summary>
    public const string MobileAsTyped = "07700 900123";
    public const string Mobile = "+447700900123";

    public static readonly DateTime CreatedAtUtc = new(2026, 11, 2, 9, 30, 0, DateTimeKind.Utc);
    public static readonly DateTime NowUtc = new(2026, 11, 18, 10, 15, 0, DateTimeKind.Utc);

    public static Practice CreatePractice(
        string name = PracticeName, string? odsCode = OdsCode, bool isArchived = false, int version = 3, Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = name,
        OdsCode = odsCode,
        AddressLines = $"{AddressLine1}\n{AddressLine2}",
        Postcode = Postcode,
        Phone = Phone,
        IsArchived = isArchived,
        Version = version,
        CreatedAt = CreatedAtUtc,
        LastUpdated = CreatedAtUtc
    };

    /// <summary>A repository mock whose <c>GetAsync</c> and <c>GetByIdsAsync</c> find these practices, none in use.</summary>
    public static Mock<IPracticeRepository> RepositoryWith(params Practice[] practices)
    {
        var repository = new Mock<IPracticeRepository>();
        foreach (var practice in practices)
            repository.Setup(r => r.GetAsync(practice.Id, It.IsAny<CancellationToken>())).ReturnsAsync(practice);
        repository
            .Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                (IReadOnlyList<Practice>)practices.Where(p => ids.Contains(p.Id)).ToList());
        repository
            .Setup(r => r.GetUsageAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, PracticeUsage>());
        return repository;
    }
}
