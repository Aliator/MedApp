using MedApp.Contracts.Patients.Responses;
using MedApp.Contracts.Procedures.PatientProcedures.Responses;
using MedApp.Contracts.Procedures.PatientProcedureStages.Definitions.Responses;
using MedApp.Contracts.Procedures.PatientProcedureStages.Templates.Responses;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace MedApp.E2ETests.Common;

/// <summary>
/// Base class for end-to-end tests: a fresh browser page per test, at the app's base URL, signed in as the
/// E2E account. The settings match the UI parity screenshots: desktop 1440×900, en-GB, Europe/London.
/// Override <see cref="IsSignedIn"/> for tests that start signed out, such as the login page.
/// <para>
/// <see cref="Api"/> is the app's API as the E2E account, for a test's setup and checks; whatever a test makes it
/// registers with <see cref="Data"/>, which deletes it when the test ends.
/// </para>
/// </summary>
public abstract class E2ETest : PageTest
{
    private IAPIRequestContext? _request;

    protected virtual bool IsSignedIn => true;

    /// <summary>The API, signed in as the E2E account (whether or not the page is).</summary>
    protected E2EApi Api { get; private set; } = null!;

    protected E2EData Data { get; private set; } = null!;

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        BaseURL = E2ESettings.BaseUrl,
        ViewportSize = new ViewportSize { Width = 1440, Height = 900 },
        Locale = "en-GB",
        TimezoneId = "Europe/London",
        IgnoreHTTPSErrors = true, // ASP.NET Core dev certificates
        StorageStatePath = IsSignedIn ? E2ESettings.StorageStatePath : null,
    };

    [SetUp]
    public async Task StartApiAsync()
    {
        // Pages wait for their data and dialogs to open; a slower CI runner gets more time.
        SetDefaultExpectTimeout(10_000);
        Context.SetDefaultTimeout(15_000);

        _request = await NewApiContextAsync(E2ESettings.StorageStatePath);
        Api = new E2EApi(_request);
        Data = new E2EData();
    }

    [TearDown]
    public async Task CleanUpAsync()
    {
        try
        {
            // Null when the set-up itself failed.
            if (Data is not null)
                await Data.CleanUpAsync();
        }
        finally
        {
            if (_request is not null)
                await _request.DisposeAsync();
        }
    }

    /// <summary>An API context: signed in from a saved session, or signed out (null).</summary>
    protected Task<IAPIRequestContext> NewApiContextAsync(string? storageStatePath) =>
        Playwright.APIRequest.NewContextAsync(new APIRequestNewContextOptions
        {
            BaseURL = E2ESettings.BaseUrl,
            IgnoreHTTPSErrors = true,
            StorageStatePath = storageStatePath,
        });

    /// <summary>The API signed in as another account (one the test made), for what that person does.</summary>
    protected async Task<E2EApi> SignInToApiAsync(string username, string password)
    {
        var request = await NewApiContextAsync(null);
        Data.OnCleanUp($"the API session of {username}", async () =>
        {
            await request.PostAsync("/api/auth/logout");
            await request.DisposeAsync();
        });
        var response = await request.PostAsync("/api/auth/login", new APIRequestContextOptions
        {
            DataObject = new { username, password },
        });
        Assert.That(response.Ok, Is.True, $"Signing {username} in failed with {response.Status}.");
        return new E2EApi(request);
    }

    /// <summary>
    /// A synthetic patient, deleted after the test, with any procedures made for them (cancelled and deleted first).
    /// </summary>
    protected async Task<PatientResponse> CreatePatientAsync(string firstName = "Grace")
    {
        var patient = await Api.CreatePatientAsync(firstName, E2EData.UniqueName("Patient"), new DateOnly(1984, 3, 21));
        Data.OnCleanUp($"patient {patient.LastName}", () => Api.DeletePatientAsync(patient.Id));
        Data.OnCleanUp($"the procedures of {patient.LastName}", async () =>
        {
            foreach (var procedure in await Api.ProceduresOfPatientAsync(patient.Id))
                await Api.CancelAndDeleteProcedureAsync(procedure.Id);
        });
        return patient;
    }

    /// <summary>A template of new stages, deleted after the test with its stages (after any procedures made from it).</summary>
    protected async Task<(PatientProcedureStageTemplateResponse Template, IReadOnlyList<PatientProcedureStageDefinitionResponse> Stages)>
        CreateTemplateAsync(int stageCount)
    {
        var stages = new List<PatientProcedureStageDefinitionResponse>();
        for (var i = 1; i <= stageCount; i++)
        {
            var stage = await Api.CreateStageDefinitionAsync(E2EData.UniqueName($"Stage {i}"), $"Synthetic instructions for stage {i}.");
            Data.OnCleanUp($"stage {stage.Name}", () => Api.DeleteStageDefinitionAsync(stage.Id));
            stages.Add(stage);
        }

        var template = await Api.CreateTemplateAsync(E2EData.UniqueName("Template"), [.. stages.Select(s => s.Id)]);
        Data.OnCleanUp($"template {template.Name}", () => Api.DeleteTemplateAsync(template.Id));
        return (template, stages);
    }

    /// <summary>A procedure for a patient made with <see cref="CreatePatientAsync"/> (so it's deleted with them).</summary>
    protected Task<PatientProcedureResponse> CreateProcedureAsync(Guid patientId, Guid templateId) =>
        Api.CreateProcedureAsync(patientId, templateId, E2EData.UniqueName("Procedure"), UkToday.AddDays(7));

    /// <summary>
    /// Opens a page and waits until it can be used. Pages are rendered on the server, then taken over by React in the
    /// browser (hydrated): typing or clicking before that is lost, as React replaces what the server sent.
    /// </summary>
    protected async Task OpenAsync(string path)
    {
        await Page.GotoAsync(path);
        await WaitUntilInteractiveAsync();
    }

    /// <summary>
    /// Waits until React has taken over every control on the page (each has React's props on it), then for the page's
    /// data to load.
    /// </summary>
    protected async Task WaitUntilInteractiveAsync()
    {
        await Page.WaitForFunctionAsync("""
            () => {
              const controls = [...document.querySelectorAll('button, input, a[href]')];
              return controls.length > 0
                && controls.every(c => Object.keys(c).some(k => k.startsWith('__reactProps$')));
            }
            """);
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>A date as the app's date fields take it typed: dd/MM/yyyy (en-GB).</summary>
    protected static string Typed(DateOnly date) => date.ToString("dd/MM/yyyy");

    /// <summary>Today in the UK, the day due dates are counted in.</summary>
    protected static DateOnly UkToday =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/London")));

    protected ILocator Main => Page.GetByRole(AriaRole.Main);
}
