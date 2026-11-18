using FluentAssertions;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Practices.Common;
using MedApp.Application.Practices.Repositories;
using Moq;
using static MedApp.UnitTests.Common.Constants.PracticesTestConstants;

namespace MedApp.UnitTests.Application.Practices.Common;

/// <summary>The practice mapper, and the shared rules for choosing a practice and for its secure email.</summary>
[TestFixture]
public sealed class PracticeMapperTests
{
    [Test]
    public void ToResponse_GivesEveryDetailTheUsageAndWhetherItCanBeDeleted()
    {
        var practice = CreatePractice();
        practice.SecureEmail = SecureEmail;

        var response = PracticeMapper.ToResponse(practice, new PracticeUsage(2, 1));

        response.Should().BeEquivalentTo(new
        {
            practice.Id,
            Name = PracticeName,
            OdsCode,
            AddressLines = new[] { AddressLine1, AddressLine2 },
            Postcode,
            Phone,
            SecureEmail,
            IsArchived = false,
            Version = 3,
            PatientCount = 2,
            ReferralCount = 1,
            CanBeDeleted = false,
            CreatedAt = CreatedAtUtc,
            LastUpdated = CreatedAtUtc
        });
    }

    [Test]
    public void ToResponse_Unused_CanBeDeleted() =>
        PracticeMapper.ToResponse(CreatePractice(), PracticeUsage.None).CanBeDeleted.Should().BeTrue();

    [Test]
    public async Task ToResponseAsync_LooksUpTheUsage()
    {
        var practice = CreatePractice();
        var repository = new Mock<IPracticeRepository>();
        repository.Setup(r => r.GetUsageAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, PracticeUsage> { [practice.Id] = new(0, 4) });

        var response = await PracticeMapper.ToResponseAsync(practice, repository.Object, CancellationToken.None);

        response.ReferralCount.Should().Be(4);
        response.CanBeDeleted.Should().BeFalse();
    }

    [Test]
    public void ToSummary_GivesWhatAPatientsPageShows()
    {
        var practice = CreatePractice(isArchived: true);

        PracticeMapper.ToSummary(practice).Should().BeEquivalentTo(new
        {
            practice.Id,
            Name = PracticeName,
            OdsCode,
            AddressLines = new[] { AddressLine1, AddressLine2 },
            Postcode,
            Phone,
            IsArchived = true
        });
    }

    [Test]
    public async Task CheckChosen_ArchivedNewlyChosen_IsAConflict_ButKeptIsFine()
    {
        var practice = CreatePractice(isArchived: true);
        var repository = RepositoryWith(practice);

        var choose = () => PracticeRules.CheckChosenAsync(repository.Object, practice.Id, null, CancellationToken.None);
        await choose.Should().ThrowAsync<ConflictException>().WithMessage(PracticeRules.ArchivedMessage(practice));

        (await PracticeRules.CheckChosenAsync(repository.Object, practice.Id, practice.Id, CancellationToken.None))
            .Should().BeSameAs(practice);
    }

    [Test]
    public async Task CheckChosen_Unknown_IsNotFound()
    {
        var act = () => PracticeRules.CheckChosenAsync(RepositoryWith().Object, Guid.NewGuid(), null, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [TestCase("a@nhs.net", true)]
    [TestCase("a@NHS.NET", true)]
    [TestCase("a@team.nhs.net", true)]
    [TestCase("a@notnhs.net", false)]
    [TestCase("a@nhs.net.example", false)]
    [TestCase("not an address", false)]
    [TestCase(null, false)]
    public void IsInAllowedDomain_MatchesTheDomainOrASubdomain(string? email, bool allowed) =>
        PracticeRules.IsInAllowedDomain(email, LettersOptions.DefaultSecureEmailDomains).Should().Be(allowed);

    [Test]
    public void LettersOptions_NoneConfigured_AllowsNhsNet()
    {
        new LettersOptions().AllowedSecureEmailDomains.Should().Equal("nhs.net");
        new LettersOptions { SecureEmailDomains = [" ", ""] }.AllowedSecureEmailDomains.Should().Equal("nhs.net");
        new LettersOptions { SecureEmailDomains = [" @Secure.Example "] }.AllowedSecureEmailDomains.Should().Equal("secure.example");
    }

    [Test]
    public void PracticeDetails_From_TidiesEachDetail_AndMatchesAPracticeWithThem()
    {
        var details = PracticeDetails.From($" {PracticeName} ", " z999 01", [AddressLine1, AddressLine2], PostcodeAsTyped, Phone, " ");

        details.Should().Be(new PracticeDetails(PracticeName, OdsCode, $"{AddressLine1}\n{AddressLine2}", Postcode, Phone, null));
        details.Matches(CreatePractice()).Should().BeTrue();
        (details with { Phone = null }).Matches(CreatePractice()).Should().BeFalse();
    }
}
