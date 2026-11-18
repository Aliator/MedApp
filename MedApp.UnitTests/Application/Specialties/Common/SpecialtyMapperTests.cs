using FluentAssertions;
using MedApp.Application.Specialties.Common;
using static MedApp.UnitTests.Common.Constants.SpecialtiesTestConstants;

namespace MedApp.UnitTests.Application.Specialties.Common;

[TestFixture]
public sealed class SpecialtyMapperTests
{
    [Test]
    public void ToResponse_CopiesItAndWhatHasIt()
    {
        var specialty = CreateSpecialty(isArchived: true, version: 7);

        var response = SpecialtyMapper.ToResponse(specialty, new SpecialtyUsage(2, 9, 4));

        response.Should().BeEquivalentTo(new
        {
            specialty.Id,
            Name = SpecialtyName,
            IsArchived = true,
            Version = 7,
            TemplateCount = 2,
            PathwayCount = 9,
            MemberCount = 4,
            CanBeDeleted = false,
            CreatedAt = CreatedAtUtc,
            LastUpdated = CreatedAtUtc
        });
    }

    [Test]
    public void ToResponse_NothingHasIt_CanBeDeleted() =>
        SpecialtyMapper.ToResponse(CreateSpecialty(), SpecialtyUsage.None).CanBeDeleted.Should().BeTrue();

    [Test]
    public void ToStaffResponses_SortsByNameIgnoringCase()
    {
        var urology = CreateSpecialty(OtherSpecialtyName, isArchived: true);
        var biomechanics = CreateSpecialty("synthetic Biomechanics");
        var dermatology = CreateSpecialty();

        var responses = SpecialtyMapper.ToStaffResponses([urology, biomechanics, dermatology]);

        responses.Select(r => (r.Id, r.Name, r.IsArchived)).Should().Equal(
            (biomechanics.Id, "synthetic Biomechanics", false),
            (dermatology.Id, SpecialtyName, false),
            (urology.Id, OtherSpecialtyName, true));
    }
}
