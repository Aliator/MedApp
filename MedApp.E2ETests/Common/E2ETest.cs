using System.Text.RegularExpressions;
using MedApp.Contracts.Patients.Responses;
using MedApp.Contracts.Pathways.PatientPathways.Responses;
using MedApp.Contracts.Pathways.PatientPathwayStages.Definitions.Responses;
using MedApp.Contracts.Pathways.PatientPathwayStages.Templates.Responses;
using MedApp.Contracts.Teams.Responses;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace MedApp.E2ETests.Common;

/// <summary>
/// Base class for end-to-end tests: a fresh browser page per test, at the app's base URL, signed in as the run's
/// member of staff (<see cref="Staff"/>), who does the clinical work in the E2E clinic, as admins are read only. The
/// settings match the UI parity screenshots: desktop 1440×900, en-GB, Europe/London.
/// Override <see cref="SignedInAs"/> for an admin journey (users, setup), and <see cref="IsSignedIn"/> for tests that
/// start signed out, such as the login page.
/// <para>
/// <see cref="Api"/> is the app's API as the account the page is signed in as, <see cref="StaffApi"/> and
/// <see cref="AdminApi"/> as each, for a test's setup and checks; whatever a test makes it registers with
/// <see cref="Data"/>, which deletes it when the test ends.
/// </para>
/// </summary>
public abstract class E2ETest : PageTest
{
    /// <summary>What the page logs when the browser refuses something under the Content Security Policy.</summary>
    private const string CspViolationMarker = "[medapp-e2e] CSP violation:";

    /// <summary>
    /// Reports every Content Security Policy violation in the page to its console, where the test collects it: the
    /// production image enforces the policy, so a refused script or connection fails the test even if the journey
    /// still works.
    /// </summary>
    private const string CspViolationListener =
        "document.addEventListener('securitypolicyviolation', (e) => console.error('" + CspViolationMarker +
        " ' + e.violatedDirective + ' blocked ' + (e.blockedURI || 'inline') + ' at ' + (e.sourceFile || document.location.href) + ':' + e.lineNumber));";

    private readonly List<string> _cspViolations = [];
    private IAPIRequestContext? _adminRequest;
    private IAPIRequestContext? _staffRequest;

    protected virtual bool IsSignedIn => true;

    /// <summary>Who the page is signed in as: the run's member of staff, unless an admin journey says otherwise.</summary>
    protected virtual E2EAccount SignedInAs => E2EAccount.Staff;

    /// <summary>The API as the account the page is signed in as (<see cref="SignedInAs"/>), whether or not the page is.</summary>
    protected E2EApi Api => SignedInAs == E2EAccount.Admin ? AdminApi : StaffApi;

    /// <summary>The API as the run's member of staff: patients, pathways, referrals, appointments, letters.</summary>
    protected E2EApi StaffApi { get; private set; } = null!;

    /// <summary>The API as the E2E admin: accounts, roles, teams, the stage library and templates.</summary>
    protected E2EApi AdminApi { get; private set; } = null!;

    /// <summary>The run's member of staff (<see cref="E2ESession"/>).</summary>
    protected static E2EStaff Staff => E2ESession.Staff;

    /// <summary>The E2E clinic, the run's member of staff's team, where templates and referrals go.</summary>
    protected static TeamResponse Team => Staff.Team;

    protected E2EData Data { get; private set; } = null!;

    public override BrowserNewContextOptions ContextOptions() => new()
    {
        BaseURL = E2ESettings.BaseUrl,
        ViewportSize = new ViewportSize { Width = 1440, Height = 900 },
        Locale = "en-GB",
        TimezoneId = "Europe/London",
        IgnoreHTTPSErrors = true, // ASP.NET Core dev certificates
        StorageStatePath = IsSignedIn ? E2ESettings.StorageStatePath(SignedInAs) : null,
    };

    [SetUp]
    public async Task StartApiAsync()
    {
        // Pages wait for their data and dialogs to open; a slower CI runner gets more time.
        SetDefaultExpectTimeout(10_000);
        Context.SetDefaultTimeout(15_000);

        // Every page this test opens, and every document it navigates to.
        _cspViolations.Clear();
        await Context.AddInitScriptAsync(CspViolationListener);
        Context.Console += (_, message) =>
        {
            if (message.Text.StartsWith(CspViolationMarker, StringComparison.Ordinal)
                || message.Text.Contains("Content Security Policy", StringComparison.Ordinal))
                _cspViolations.Add(message.Text);
        };

        if (E2ESettings.TraceDirectory is not null)
            await Context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true });

        _adminRequest = await NewApiContextAsync(E2ESettings.AdminStorageStatePath);
        _staffRequest = await NewApiContextAsync(E2ESettings.StaffStorageStatePath);
        AdminApi = new E2EApi(_adminRequest);
        StaffApi = new E2EApi(_staffRequest);
        Data = new E2EData();
    }

    [TearDown]
    public async Task CleanUpAsync()
    {
        try
        {
            await SaveTraceIfFailedAsync();
        }
        catch (PlaywrightException)
        {
            // A trace is a help, not part of the test.
        }

        try
        {
            // Null when the set-up itself failed.
            if (Data is not null)
                await Data.CleanUpAsync();
        }
        finally
        {
            if (_staffRequest is not null)
                await _staffRequest.DisposeAsync();
            if (_adminRequest is not null)
                await _adminRequest.DisposeAsync();
        }

        // Last, once everything is cleaned up: a violation fails the test even when the journey worked.
        if (_cspViolations.Count > 0)
            Assert.Fail($"The page broke its Content Security Policy:{Environment.NewLine}{string.Join(Environment.NewLine, _cspViolations)}");
    }

    /// <summary>With tracing on (<see cref="E2ESettings.TraceDirectory"/>), keeps the trace of a failed test only.</summary>
    private async Task SaveTraceIfFailedAsync()
    {
        if (E2ESettings.TraceDirectory is not { } directory)
            return;

        if (TestContext.CurrentContext.Result.Outcome.Status != NUnit.Framework.Interfaces.TestStatus.Failed)
        {
            await Context.Tracing.StopAsync();
            return;
        }

        Directory.CreateDirectory(directory);
        var name = string.Concat(TestContext.CurrentContext.Test.Name.Select(c => char.IsLetterOrDigit(c) ? c : '_'));
        await Context.Tracing.StopAsync(new TracingStopOptions { Path = Path.Combine(directory, $"{name}.zip") });
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
    /// Another member of staff, made by the admin with these roles (by name), in the E2E clinic unless
    /// <paramref name="inTheClinic"/> is false, who has chosen their own password (as an account an admin makes must
    /// first), with their API. Deleted after the test.
    /// </summary>
    protected async Task<E2EColleague> CreateColleagueAsync(string[] roles, bool inTheClinic = true)
    {
        var username = E2EData.UniqueName("staff").Replace(' ', '-').ToLowerInvariant();
        var firstPassword = await AdminApi.CreateUserAsync(username, roles);
        Data.OnCleanUp($"user {username}", () => AdminApi.DeleteUserAsync(username, E2ESettings.Password!));
        if (inTheClinic)
            await AdminApi.SetTeamsAsync(username, Team.Id);

        var password = E2EData.NewPassword();
        await (await SignInToApiAsync(username, firstPassword)).UpdateOwnPasswordAsync(username, firstPassword, password);
        return new E2EColleague(username, password, await AdminApi.UserIdOfAsync(username), await SignInToApiAsync(username, password));
    }

    /// <summary>
    /// A browser page of its own, signed in as <paramref name="account"/> from its saved session, for a second person in
    /// the journey (closed after the test).
    /// </summary>
    protected async Task<IPage> NewPageAsAsync(E2EAccount account)
    {
        var options = ContextOptions();
        options.StorageStatePath = E2ESettings.StorageStatePath(account);
        var context = await Browser.NewContextAsync(options);
        Data.OnCleanUp($"the {account} browser", () => context.CloseAsync());
        context.SetDefaultTimeout(15_000);
        return await context.NewPageAsync();
    }

    /// <summary>
    /// A browser page of its own in which <paramref name="username"/> signs in through the login page (closed after the
    /// test).
    /// </summary>
    protected async Task<IPage> SignInPageAsync(string username, string password)
    {
        var options = ContextOptions();
        options.StorageStatePath = null;
        var context = await Browser.NewContextAsync(options);
        Data.OnCleanUp($"the browser of {username}", () => context.CloseAsync());
        context.SetDefaultTimeout(15_000);
        var page = await context.NewPageAsync();
        await OpenAsync(page, "/login");
        await page.GetByRole(AriaRole.Textbox, new() { Name = "Username" }).FillAsync(username);
        await page.GetByRole(AriaRole.Textbox, new() { Name = "Password" }).FillAsync(password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();
        await Expect(page).Not.ToHaveURLAsync(new Regex("/login"));
        return page;
    }

    /// <summary>
    /// A synthetic patient, deleted after the test, with any pathways made for them (cancelled and deleted first).
    /// A patient who has had an appointment can't be deleted (the app never deletes appointments): their open ones are
    /// cancelled instead, and the patient stays.
    /// </summary>
    protected async Task<PatientResponse> CreatePatientAsync(string firstName = "Grace")
    {
        var patient = await StaffApi.CreatePatientAsync(firstName, E2EData.UniqueName("Patient"), new DateOnly(1984, 3, 21));
        Data.OnCleanUp($"patient {patient.LastName}", async () =>
        {
            var appointments = await StaffApi.AppointmentsOfPatientAsync(patient.Id);
            foreach (var appointment in appointments.Where(a => a.CanCancel))
                await StaffApi.CancelAppointmentAsync(appointment.Id, "End-to-end test clean-up.");
            if (appointments.Count == 0)
                await StaffApi.DeletePatientAsync(patient.Id);
        });
        Data.OnCleanUp($"the pathways of {patient.LastName}", () => DeletePathwaysOfAsync(patient.Id));
        return patient;
    }

    /// <summary>Cancels and deletes a patient's pathways (the E2E clinic's: all a test makes).</summary>
    protected async Task DeletePathwaysOfAsync(Guid patientId)
    {
        foreach (var pathway in await StaffApi.PathwaysOfPatientAsync(patientId))
            await StaffApi.CancelAndDeletePathwayAsync(pathway.Id);
    }

    /// <summary>
    /// A template of new stages in the E2E clinic, made by the admin, deleted after the test with its stages (after any
    /// pathways made from it).
    /// </summary>
    protected Task<(PathwayTemplateResponse Template, IReadOnlyList<PatientPathwayStageDefinitionResponse> Stages)>
        CreateTemplateAsync(int stageCount) =>
        CreateTemplateAsync([.. Enumerable.Repeat("None", stageCount)]);

    /// <summary>The same, with a stage for each visit requirement given (None, Suggested or Required), in order.</summary>
    protected Task<(PathwayTemplateResponse Template, IReadOnlyList<PatientPathwayStageDefinitionResponse> Stages)>
        CreateTemplateAsync(params string[] visitRequirements) =>
        CreateTemplateAsync(visitRequirements, keptByAPathway: false);

    /// <summary>
    /// A template of new stages for a pathway the test can't delete (one with a letter: README): after the test they're
    /// archived, as the API won't delete what a pathway uses, rather than deleted.
    /// </summary>
    protected Task<(PathwayTemplateResponse Template, IReadOnlyList<PatientPathwayStageDefinitionResponse> Stages)>
        CreateTemplateForAKeptPathwayAsync(int stageCount) =>
        CreateTemplateAsync([.. Enumerable.Repeat("None", stageCount)], keptByAPathway: true);

    private async Task<(PathwayTemplateResponse Template, IReadOnlyList<PatientPathwayStageDefinitionResponse> Stages)>
        CreateTemplateAsync(string[] visitRequirements, bool keptByAPathway)
    {
        var stages = new List<PatientPathwayStageDefinitionResponse>();
        for (var i = 1; i <= visitRequirements.Length; i++)
        {
            var stage = await AdminApi.CreateStageDefinitionAsync(
                E2EData.UniqueName($"Stage {i}"), $"Synthetic instructions for stage {i}.", visitRequirements[i - 1]);
            Data.OnCleanUp($"stage {stage.Name}", () =>
                keptByAPathway ? AdminApi.DeleteOrArchiveStageDefinitionAsync(stage.Id) : AdminApi.DeleteStageDefinitionAsync(stage.Id));
            stages.Add(stage);
        }

        var template = await AdminApi.CreateTemplateAsync(E2EData.UniqueName("Template"), Team.Id, [.. stages.Select(s => s.Id)]);
        Data.OnCleanUp($"template {template.Name}", () =>
            keptByAPathway ? AdminApi.DeleteOrArchiveTemplateAsync(template.Id) : AdminApi.DeleteTemplateAsync(template.Id));
        return (template, stages);
    }

    /// <summary>
    /// A pathway, made by the run's member of staff, for a patient made with <see cref="CreatePatientAsync"/> (so it's
    /// deleted with them).
    /// </summary>
    protected Task<PatientPathwayResponse> CreatePathwayAsync(Guid patientId, Guid templateId) =>
        StaffApi.CreatePathwayAsync(patientId, templateId, E2EData.UniqueName("Pathway"), UkToday.AddDays(7));

    /// <summary>
    /// Opens a page and waits until it can be used. Pages are rendered on the server, then taken over by React in the
    /// browser (hydrated): typing or clicking before that is lost, as React replaces what the server sent.
    /// </summary>
    protected Task OpenAsync(string path) => OpenAsync(Page, path);

    /// <summary>The same, in another page (e.g. a second person's browser).</summary>
    protected static async Task OpenAsync(IPage page, string path)
    {
        await page.GotoAsync(path);
        await WaitUntilInteractiveAsync(page);
    }

    /// <summary>
    /// Waits until React has taken over every control on the page (each has React's props on it), then for the page's
    /// data to load.
    /// </summary>
    protected Task WaitUntilInteractiveAsync() => WaitUntilInteractiveAsync(Page);

    protected static async Task WaitUntilInteractiveAsync(IPage page)
    {
        await page.WaitForFunctionAsync("""
            () => {
              const controls = [...document.querySelectorAll('button, input, a[href]')];
              return controls.length > 0
                && controls.every(c => Object.keys(c).some(k => k.startsWith('__reactProps$')));
            }
            """);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>A date as the app's date fields take it typed: dd/MM/yyyy (en-GB).</summary>
    protected static string Typed(DateOnly date) => date.ToString("dd/MM/yyyy");

    /// <summary>Today in the UK, the day target dates are counted in.</summary>
    protected static DateOnly UkToday =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/London")));

    protected ILocator Main => Page.GetByRole(AriaRole.Main);

    /// <summary>
    /// Shows a grid page's search bar (the one labelled <paramref name="searchLabel"/>, e.g. "Search patients") and
    /// returns it. A grid page (<c>ListFilterBar</c>) opens on its tabs, with the search behind its menu: "List options
    /// ...", then Search. Nothing to do when the bar is already showing.
    /// </summary>
    protected async Task<ILocator> OpenListSearchAsync(string searchLabel)
    {
        var search = Main.GetByRole(AriaRole.Combobox, new() { Name = searchLabel });
        if (await search.IsVisibleAsync())
            return search;

        // Its name says which tab the list shows, if any: "List options (showing All)".
        await Main.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^List options") }).ClickAsync();
        // An item's name runs on into its description ("Search Search with filters").
        await Page.GetByRole(AriaRole.Menuitem, new() { NameRegex = new Regex("^Search") }).ClickAsync();
        await Expect(search).ToBeVisibleAsync();
        return search;
    }
}

/// <summary>Another member of staff a test made (<see cref="E2ETest.CreateColleagueAsync"/>), with their API.</summary>
public sealed record E2EColleague(string Username, string Password, Guid UserId, E2EApi Api);
