using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using MedApp.Contracts.Letters.Requests;
using MedApp.Contracts.Letters.Responses;
using MedApp.Contracts.Specialties.Requests;
using MedApp.Contracts.Specialties.Responses;
using MedApp.IntegrationTests.Common;

namespace MedApp.IntegrationTests.Letters;

/// <summary>
/// Letter templates and the PDF engine, through the API against SQL Server: the unique name per specialty (every
/// specialty's too, which a nullable column's unique index must not leave out), versions, archiving, the specialty's
/// foreign key and usage count, who may change them, and a preview rendered by PDFsharp in the API's own process (on
/// Linux in CI, with the bundled font).
/// </summary>
[TestFixture]
public sealed class LetterTemplateTests : IntegrationTest
{
    private const string Templates = "/api/letter-templates";

    private HttpClient _admin = null!;

    [SetUp]
    public async Task SignInAsync() => _admin = await SignInAsAdminAsync();

    private static CreateLetterTemplateRequest Request(string name, Guid? specialtyId = null) => new()
    {
        Name = name,
        Kind = "ClinicLetter",
        SpecialtyId = specialtyId,
        Letterhead = "Synthetic Specialist Centre\n{{site.name}}",
        BodySkeleton = "Dear Doctor,\n\n## Diagnosis\n\n## Plan\n\nRe: {{patient.name}}",
        Closing = "Yours sincerely,\n\n{{author.name}}"
    };

    private async Task<SpecialtyResponse> CreateSpecialtyAsync() =>
        await (await _admin.PostAsJsonAsync("/api/specialties", new CreateSpecialtyRequest { Name = UniqueName("Specialty") }))
            .ReadAsAsync<SpecialtyResponse>(HttpStatusCode.Created);

    [Test]
    public async Task Templates_AreAddedEditedArchivedAndRestored_WithTheirMergeFields()
    {
        var specialty = await CreateSpecialtyAsync();
        var name = UniqueName("Clinic letter");

        var created = await (await _admin.PostAsJsonAsync(Templates, Request(name, specialty.Id)))
            .ReadAsAsync<LetterTemplateResponse>(HttpStatusCode.Created);
        created.Should().BeEquivalentTo(new
        {
            Name = name, Kind = "ClinicLetter", SpecialtyId = specialty.Id, SpecialtyName = specialty.Name, Version = 1,
            IsArchived = false
        });
        created.MergeFields.Should().Equal("site.name", "patient.name", "author.name");

        var updated = await (await _admin.PutAsJsonAsync($"{Templates}/{created.Id}", new UpdateLetterTemplateRequest
            {
                Name = name, Kind = "DischargeSummary", Letterhead = "Centre", BodySkeleton = "Body", Closing = "Bye",
                ExpectedVersion = 1
            }))
            .ReadAsAsync<LetterTemplateResponse>();
        updated.Should().BeEquivalentTo(new { Kind = "DischargeSummary", SpecialtyId = (Guid?)null, Version = 2 });

        // A page loaded before that edit can't overwrite it.
        (await _admin.PostAsJsonAsync($"{Templates}/{created.Id}/archive", new LetterTemplateVersionRequest { ExpectedVersion = 1 }))
            .StatusCode.Should().Be(HttpStatusCode.Conflict);

        var archived = await (await _admin.PostAsJsonAsync(
                $"{Templates}/{created.Id}/archive", new LetterTemplateVersionRequest { ExpectedVersion = 2 }))
            .ReadAsAsync<LetterTemplateResponse>();
        archived.IsArchived.Should().BeTrue();

        (await (await _admin.GetAsync(Templates)).ReadAsAsync<List<LetterTemplateResponse>>())
            .Should().NotContain(t => t.Id == created.Id);
        (await (await _admin.GetAsync($"{Templates}?includeArchived=true")).ReadAsAsync<List<LetterTemplateResponse>>())
            .Should().Contain(t => t.Id == created.Id);

        var restored = await (await _admin.PostAsJsonAsync(
                $"{Templates}/{created.Id}/restore", new LetterTemplateVersionRequest { ExpectedVersion = 3 }))
            .ReadAsAsync<LetterTemplateResponse>();
        restored.Should().BeEquivalentTo(new { IsArchived = false, Version = 4 });

        (await (await _admin.GetAsync($"{Templates}/{created.Id}")).ReadAsAsync<LetterTemplateResponse>()).Version.Should().Be(4);
        (await _admin.GetAsync($"{Templates}/{Guid.NewGuid()}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Names_AreUniqueWithinASpecialty_AndAmongThoseForEverySpecialty_IgnoringCase()
    {
        var specialty = await CreateSpecialtyAsync();
        var name = UniqueName("Discharge summary");

        await (await _admin.PostAsJsonAsync(Templates, Request(name))).ShouldHaveStatusAsync(HttpStatusCode.Created);
        await (await _admin.PostAsJsonAsync(Templates, Request(name.ToUpperInvariant())))
            .ShouldHaveStatusAsync(HttpStatusCode.Conflict);
        await (await _admin.PostAsJsonAsync(Templates, Request(name, specialty.Id))).ShouldHaveStatusAsync(HttpStatusCode.Created);
        await (await _admin.PostAsJsonAsync(Templates, Request(name, specialty.Id))).ShouldHaveStatusAsync(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task ASpecialtyWithATemplate_CountsIt_AndCantBeDeleted()
    {
        var specialty = await CreateSpecialtyAsync();
        await (await _admin.PostAsJsonAsync(Templates, Request(UniqueName("Letter"), specialty.Id)))
            .ShouldHaveStatusAsync(HttpStatusCode.Created);

        var read = await (await _admin.GetAsync($"/api/specialties/{specialty.Id}")).ReadAsAsync<SpecialtyResponse>();
        read.Should().BeEquivalentTo(new { LetterTemplateCount = 1, CanBeDeleted = false });
        (await _admin.DeleteAsync($"/api/specialties/{specialty.Id}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Test]
    public async Task UnknownMergeFields_AndAnUnknownSpecialty_AreRefused()
    {
        var badField = Request(UniqueName("Letter"));
        badField.BodySkeleton = "Dear {{patient.nme}}";
        var problem = await (await _admin.PostAsJsonAsync(Templates, badField)).Content.ReadAsStringAsync();
        problem.Should().Contain("BodySkeleton").And.Contain("{{patient.nme}} isn't a merge field");

        (await _admin.PostAsJsonAsync(Templates, Request(UniqueName("Letter"), Guid.NewGuid())))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Staff_ReadTemplatesAndMergeFields_ButOnlyManageSetupChangesOrPreviewsThem()
    {
        var staff = await CreateStaffAsync("Clinician");

        (await staff.Client.GetAsync(Templates)).StatusCode.Should().Be(HttpStatusCode.OK);
        var fields = await (await staff.Client.GetAsync($"{Templates}/merge-fields")).ReadAsAsync<List<LetterMergeFieldResponse>>();
        fields.Should().Contain(f => f.Name == "patient.nhsNumber" && f.Example == "999 000 0018");
        (await staff.Client.PostAsJsonAsync(Templates, Request(UniqueName("Letter")))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await staff.Client.PostAsJsonAsync($"{Templates}/preview", new PreviewLetterTemplateRequest()))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Preview_IsAPdfDownload_RenderedWithTheBundledFont()
    {
        var response = await _admin.PostAsJsonAsync($"{Templates}/preview", new PreviewLetterTemplateRequest
        {
            Name = "Clinic letter",
            Letterhead = "Synthetic Specialist Centre",
            BodySkeleton = "# Clinic letter\n\nRe: {{patient.name}}, Zoë Łukasz\n\n- one\n- two",
            Closing = "{{author.name}}",
            LargePrint = true
        });

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        response.Content.Headers.ContentDisposition!.DispositionType.Should().Be("attachment");
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        var bytes = await response.Content.ReadAsByteArrayAsync();
        System.Text.Encoding.Latin1.GetString(bytes).Should().StartWith("%PDF-").And.Contain("Atkinson");
    }

    [Test]
    public async Task Preview_CharactersTheFontLacks_AreRefused()
    {
        var response = await _admin.PostAsJsonAsync($"{Templates}/preview", new PreviewLetterTemplateRequest
        {
            Letterhead = "", BodySkeleton = "Done ✓", Closing = ""
        });

        await response.ShouldHaveStatusAsync(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("BodySkeleton").And.Contain("can't show");
    }
}
