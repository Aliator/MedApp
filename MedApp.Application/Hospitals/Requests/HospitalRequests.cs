using FluentValidation;
using MedApp.Application.Activity.Common;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.Common.Paging;
using MedApp.Application.Common.Teams;
using MedApp.Application.Hospitals.Common;
using MedApp.Application.Hospitals.Repositories;
using MedApp.Application.Lab.Cases.Commands;
using MedApp.Application.Lab.Common;
using MedApp.Application.Lab.Repositories;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Patients.Common;
using MedApp.Application.Specimens.Common;
using MedApp.Application.Specimens.Repositories;
using MedApp.Application.Teams.Repositories;
using MedApp.Contracts.Common;
using MedApp.Contracts.Hospitals.Requests;
using MedApp.Contracts.Hospitals.Responses;
using MedApp.Domain.Activity;
using MedApp.Domain.Hospitals;
using MedApp.Domain.Patients;
using MedApp.Domain.Specimens;
using MediatR;

namespace MedApp.Application.Hospitals.Requests;

// Hospital requests (MC18 of docs/multi-clinic-plan.md; the light demo's hospitals.ts): typed in by the lab
// (ProcessSpecimens, in the lab team), each opening its lab case with no pathway. Hospital cases are the lab's: the lab
// and admins see them; anyone else gets a 404 for one and an empty list. The patient is chosen by a person from the
// matches, never matched automatically (H-72).

/// <summary>The patients a request could be for: by NHS number, or last name and date of birth.</summary>
public sealed record FindHospitalPatientMatchesQuery(string? NhsNumber, string? LastName, DateOnly? DateOfBirth, PathwayActor Actor)
    : IRequest<IReadOnlyList<HospitalPatientMatchResponse>>;

/// <summary>Registers a hospital's request: its patient (chosen or new), its sample and its lab case, in one save.</summary>
public sealed record RegisterHospitalRequestCommand(
    Guid HospitalId,
    Guid? PatientId,
    HospitalPatientRequest? NewPatient,
    string? HospitalPatientNumber,
    string? NhsNumber,
    string? SurgeonName,
    string? Priority,
    bool IsCancerPathway,
    DateTime AuthoredOn,
    DateOnly? ReportNeededBy,
    DateTime? CollectedAtUtc,
    Guid SpecimenTypeId,
    Guid LabTestId,
    PathwayActor Actor)
    : IRequest<HospitalRequestResponse>;

/// <summary>A page of hospital requests (the lab's and admins'; anyone else an empty page).</summary>
public sealed record SearchHospitalRequestsQuery(
    int Page,
    int PageSize,
    Guid? HospitalId,
    string? Priority,
    string? Search,
    string? Sort,
    bool Ascending,
    PathwayActor Viewer)
    : IRequest<PagedResponse<HospitalRequestSummaryResponse>>;

/// <summary>One hospital request (the lab's and admins'); viewing it records <c>LabCaseViewed</c>.</summary>
public sealed record GetHospitalRequestQuery(Guid Id, PathwayActor Viewer) : IRequest<HospitalRequestResponse>;

/// <summary>The words and checks the hospital request handlers share.</summary>
public static class HospitalRequestRules
{
    public const string TargetKind = "HospitalRequest";
    public const string LabOnlyMessage = "Hospital requests are registered by the lab team.";
    public const string MatchMissingMessage = "Enter the NHS number, or the last name and date of birth.";
    public const string HospitalMissingMessage = "Choose the hospital.";
    public const string TypeMissingMessage = "Choose the type of sample.";
    public const string TestMissingMessage = "Choose the test the request asks for.";
    public const string NumberMissingMessage = "Enter the hospital's patient number.";
    public const string NhsNumberMessage = "An NHS number is 10 digits.";
    public const string SurgeonMissingMessage = "Enter the surgeon's name, as written.";
    public const string PriorityMessage = "Choose the priority the request gives.";
    public const string AuthoredOnMessage = "Enter when the request was made, not in the future.";
    public const string CollectedAtMessage = "The collection time can’t be in the future.";
    public const string NeededByMessage = "The report can’t be needed before the request was made.";
    public const string BothPatientsMessage = "Choose a patient from the matches, or add a new one, not both.";
    public const string NoPatientMessage = "Choose the patient from the matches, or add a new one.";
    public const string FirstNameMessage = "Enter a first name.";
    public const string LastNameMessage = "Enter a last name.";
    public const string DateOfBirthMessage = "Enter a date of birth, not in the future.";
    public const int MaxSearchLength = 200;
    public const int MaxPatientNameLength = 100;

    /// <summary>A moment the request says has happened may be up to a minute ahead (clocks differ).</summary>
    public static readonly TimeSpan ClockSlack = TimeSpan.FromMinutes(1);

    public static string AtMost(int max) => $"At most {max} characters.";

    public static string Archived(Hospital hospital) => $"'{hospital.Name}' is archived.";

    public static NotFoundException NotFound(Guid id) => new($"Hospital request '{id}' was not found.");

    /// <summary>An NHS number without its spaces, or null.</summary>
    public static string? Compact(string? number) => NhsNumbers.Normalise(number);

    /// <summary>
    /// Refuses intake to anyone outside the lab team (admins read only): a 409. Hospital cases are the lab's.
    /// </summary>
    public static void EnsureLabIntake(PathwayActor actor, Guid? labTeamId)
    {
        if (!LabCaseRules.WorksCases(actor, labTeamId))
            throw new ConflictException(LabOnlyMessage);
    }
}

public sealed class FindHospitalPatientMatchesValidator : AbstractValidator<FindHospitalPatientMatchesQuery>
{
    public FindHospitalPatientMatchesValidator()
    {
        RuleFor(x => x.NhsNumber)
            .Must((x, nhs) => HospitalRequestRules.Compact(nhs) is not null
                              || (!string.IsNullOrWhiteSpace(x.LastName) && x.DateOfBirth is not null))
            .WithMessage(HospitalRequestRules.MatchMissingMessage);
    }
}

public sealed class RegisterHospitalRequestValidator : AbstractValidator<RegisterHospitalRequestCommand>
{
    public RegisterHospitalRequestValidator(TimeProvider timeProvider)
    {
        RuleFor(x => x.HospitalId).NotEmpty().WithMessage(HospitalRequestRules.HospitalMissingMessage);
        RuleFor(x => x.SpecimenTypeId).NotEmpty().WithMessage(HospitalRequestRules.TypeMissingMessage);
        RuleFor(x => x.LabTestId).NotEmpty().WithMessage(HospitalRequestRules.TestMissingMessage);
        RuleFor(x => x.HospitalPatientNumber)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage(HospitalRequestRules.NumberMissingMessage)
            .Must(n => n is null || n.Trim().Length <= HospitalRequest.MaxHospitalPatientNumberLength)
            .WithMessage(HospitalRequestRules.AtMost(HospitalRequest.MaxHospitalPatientNumberLength));
        RuleFor(x => x.NhsNumber)
            .Must(n => string.IsNullOrWhiteSpace(n) || NhsNumbers.IsTenDigits(n)).WithMessage(HospitalRequestRules.NhsNumberMessage)
            .Must(n => !NhsNumbers.IsTenDigits(n) || NhsNumbers.IsValid(n)).WithMessage(NhsNumberRules.WrongCheckDigit);
        RuleFor(x => x.SurgeonName)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage(HospitalRequestRules.SurgeonMissingMessage)
            .Must(n => n is null || n.Trim().Length <= HospitalRequest.MaxSurgeonNameLength)
            .WithMessage(HospitalRequestRules.AtMost(HospitalRequest.MaxSurgeonNameLength));
        RuleFor(x => x.Priority)
            .Must(p => PathwayEnums.TryParse<LabRequestPriority>(p, out _)).WithMessage(HospitalRequestRules.PriorityMessage);
        RuleFor(x => x.AuthoredOn)
            .Must(at => at != default && at <= timeProvider.GetUtcNow().UtcDateTime + HospitalRequestRules.ClockSlack)
            .WithMessage(HospitalRequestRules.AuthoredOnMessage);
        RuleFor(x => x.CollectedAtUtc)
            .Must(at => at is null || at <= timeProvider.GetUtcNow().UtcDateTime + HospitalRequestRules.ClockSlack)
            .WithMessage(HospitalRequestRules.CollectedAtMessage);
        RuleFor(x => x.ReportNeededBy)
            .Must((x, by) => by is null || x.AuthoredOn == default || by >= UkTime.DateOf(x.AuthoredOn))
            .WithMessage(HospitalRequestRules.NeededByMessage);

        RuleFor(x => x.PatientId)
            .Must((x, id) => !(id is not null && x.NewPatient is not null)).WithMessage(HospitalRequestRules.BothPatientsMessage)
            .Must((x, id) => id is not null || x.NewPatient is not null).WithMessage(HospitalRequestRules.NoPatientMessage);
        When(x => x.PatientId is null && x.NewPatient is not null, () =>
        {
            RuleFor(x => x.NewPatient!.FirstName)
                .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage(HospitalRequestRules.FirstNameMessage)
                .Must(n => n is null || n.Trim().Length <= HospitalRequestRules.MaxPatientNameLength)
                .WithMessage(HospitalRequestRules.AtMost(HospitalRequestRules.MaxPatientNameLength));
            RuleFor(x => x.NewPatient!.LastName)
                .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage(HospitalRequestRules.LastNameMessage)
                .Must(n => n is null || n.Trim().Length <= HospitalRequestRules.MaxPatientNameLength)
                .WithMessage(HospitalRequestRules.AtMost(HospitalRequestRules.MaxPatientNameLength));
            RuleFor(x => x.NewPatient!.DateOfBirth)
                .Must(d => d != default && d <= UkTime.DateOf(timeProvider.GetUtcNow().UtcDateTime))
                .WithMessage(HospitalRequestRules.DateOfBirthMessage);
            RuleFor(x => x.NewPatient!.NhsNumber).NhsNumber();
        });
    }
}

public sealed class SearchHospitalRequestsValidator : AbstractValidator<SearchHospitalRequestsQuery>
{
    public SearchHospitalRequestsValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithMessage("Page must be 1 or more.");
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, Paging.MaxPageSize)
            .WithMessage($"Page size must be between 1 and {Paging.MaxPageSize}.");
        RuleFor(x => x.Priority)
            .Must(p => string.IsNullOrWhiteSpace(p) || PathwayEnums.TryParse<LabRequestPriority>(p, out _))
            .WithMessage($"Priority must be one of: {string.Join(", ", Enum.GetNames<LabRequestPriority>())}.");
        RuleFor(x => x.Search)
            .MaximumLength(HospitalRequestRules.MaxSearchLength)
            .WithMessage($"Search must not exceed {HospitalRequestRules.MaxSearchLength} characters.");
    }
}

/// <summary>
/// The intake list's filters and order (the light demo's): narrowed by hospital, priority and a search (the case number,
/// the hospital's patient number, the patient's name or NHS number), newest registered first unless a column is chosen;
/// every order ends on the ID. LINQ that runs in SQL and in memory.
/// </summary>
public static class HospitalRequestSearch
{
    public const string SortByCase = "case";
    public const string SortByPatient = "patient";
    public const string SortByHospital = "hospital";
    public const string SortByNeeded = "needed";
    public const string SortByRegistered = "registered";

    public static readonly IReadOnlyList<string> SortColumns =
        [SortByCase, SortByPatient, SortByHospital, SortByNeeded, SortByRegistered];

    public static IQueryable<HospitalRequest> Filter(this IQueryable<HospitalRequest> requests, SearchHospitalRequestsQuery query)
    {
        if (query.HospitalId is { } hospitalId)
            requests = requests.Where(r => r.HospitalId == hospitalId);
        if (PathwayEnums.TryParse<LabRequestPriority>(query.Priority, out var priority))
            requests = requests.Where(r => r.Priority == priority);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            var compact = term.Replace(" ", "");
            requests = requests.Where(r =>
                r.LabCaseNumber.ToLower().Contains(compact)
                || r.HospitalPatientNumber.ToLower().Contains(compact)
                || (r.Patient.FirstName + " " + r.Patient.LastName).ToLower().Contains(term)
                || (r.Patient.NhsNumber != null && r.Patient.NhsNumber.Contains(compact)));
        }

        return requests;
    }

    public static IOrderedQueryable<HospitalRequest> Sort(this IQueryable<HospitalRequest> requests, string? sort, bool ascending)
    {
        var ordered = Ordering.ColumnOf(sort, SortColumns) switch
        {
            SortByCase => requests.OrderByDirection(r => r.LabCaseNumber, ascending),
            SortByPatient => requests.OrderByDirection(r => r.Patient.LastName, ascending)
                .ThenByDirection(r => r.Patient.FirstName, ascending),
            SortByHospital => requests.OrderByDirection(r => r.Hospital.Name, ascending),
            // A request with no date sorts as the latest.
            SortByNeeded => requests.OrderByDirection(r => r.ReportNeededBy ?? DateOnly.MaxValue, ascending),
            SortByRegistered => requests.OrderByDirection(r => r.RegisteredAtUtc, ascending),
            _ => null
        };
        return ordered?.ThenBy(r => r.Id)
               ?? requests.OrderByDescending(r => r.RegisteredAtUtc).ThenBy(r => r.Id);
    }
}

/// <summary>
/// The patients a request could be for (the lab team): by NHS number, or last name and date of birth, never anonymised;
/// each listed is recorded (<c>PatientListed</c>). Never chooses one (H-72).
/// </summary>
public sealed class FindHospitalPatientMatchesHandler(
    IHospitalRepository repository,
    ITeamRepository teams,
    IActivityLog activityLog)
    : IRequestHandler<FindHospitalPatientMatchesQuery, IReadOnlyList<HospitalPatientMatchResponse>>
{
    public async Task<IReadOnlyList<HospitalPatientMatchResponse>> Handle(
        FindHospitalPatientMatchesQuery request, CancellationToken cancellationToken)
    {
        HospitalRequestRules.EnsureLabIntake(request.Actor, (await teams.GetLabAsync(cancellationToken))?.Id);

        var lastName = string.IsNullOrWhiteSpace(request.LastName) ? null : request.LastName.Trim();
        var matches = await repository.FindPatientMatchesAsync(
            HospitalRequestRules.Compact(request.NhsNumber),
            lastName,
            lastName is null ? null : request.DateOfBirth,
            cancellationToken);

        await activityLog.RecordViewsAsync(ActivityAction.PatientListed, matches.Select(p => p.Id).Distinct(), cancellationToken);
        return matches.Select(HospitalMapper.ToMatch).ToList();
    }
}

/// <summary>
/// Registers a hospital's request (the lab team; 409 for anyone else): the hospital, sample type and test must exist and
/// not be archived (400); the patient is the one chosen (404 if unknown, 409 if restricted or anonymised) or a new
/// record (409 if another has its NHS number). Its sample joins the samples database (received, the hospital as its
/// source, no pathway) and its lab case opens, numbered from the one sequence (H-73), all in one save.
/// </summary>
public sealed class RegisterHospitalRequestHandler(
    IHospitalRepository repository,
    ISpecimenRepository specimens,
    ILabCaseRepository labCases,
    ITeamRepository teams,
    LabReceipt receipt,
    IActivityLog activityLog,
    TimeProvider timeProvider)
    : IRequestHandler<RegisterHospitalRequestCommand, HospitalRequestResponse>
{
    public async Task<HospitalRequestResponse> Handle(RegisterHospitalRequestCommand request, CancellationToken cancellationToken)
    {
        var labTeamId = (await teams.GetLabAsync(cancellationToken))?.Id;
        HospitalRequestRules.EnsureLabIntake(request.Actor, labTeamId);

        var hospital = await repository.GetAsync(request.HospitalId, cancellationToken);
        if (hospital is null)
            throw LabCaseValidation.Invalid(nameof(request.HospitalId), HospitalRequestRules.HospitalMissingMessage);
        if (hospital.IsArchived)
            throw LabCaseValidation.Invalid(nameof(request.HospitalId), HospitalRequestRules.Archived(hospital));
        var type = await specimens.GetTypeAsync(request.SpecimenTypeId, cancellationToken);
        if (type is null || type.IsArchived)
            throw LabCaseValidation.Invalid(nameof(request.SpecimenTypeId), HospitalRequestRules.TypeMissingMessage);
        var test = await labCases.GetTestAsync(request.LabTestId, cancellationToken);
        if (test is null || test.IsArchived)
            throw LabCaseValidation.Invalid(nameof(request.LabTestId), HospitalRequestRules.TestMissingMessage);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var (patient, isNew) = await PatientAsync(request, now, cancellationToken);
        var number = await receipt.NextNumberAsync(now, cancellationToken);
        PathwayEnums.TryParse<LabRequestPriority>(request.Priority, out var priority);

        var hospitalRequest = new HospitalRequest
        {
            Id = Guid.NewGuid(),
            HospitalId = hospital.Id,
            Hospital = hospital,
            PatientId = patient.Id,
            Patient = patient,
            TeamId = labTeamId!.Value,
            HospitalPatientNumber = request.HospitalPatientNumber!.Trim(),
            NhsNumber = HospitalRequestRules.Compact(request.NhsNumber),
            SurgeonName = request.SurgeonName!.Trim(),
            Priority = priority,
            IsCancerPathway = request.IsCancerPathway,
            AuthoredOnUtc = request.AuthoredOn,
            ReportNeededBy = request.ReportNeededBy,
            CollectedAtUtc = request.CollectedAtUtc,
            SpecimenTypeId = type.Id,
            SpecimenType = type,
            LabTestId = test.Id,
            LabTest = test,
            LabCaseNumber = number,
            RegisteredAtUtc = now,
            RegisteredByUserId = request.Actor.UserId,
        };

        // Its sample joins the samples database (MC14), received now, the hospital its source.
        var sample = new Specimen
        {
            Id = Guid.NewGuid(),
            Number = number,
            PatientId = patient.Id,
            Patient = patient,
            TeamId = labTeamId.Value,
            Source = SpecimenSource.Hospital,
            HospitalId = hospital.Id,
            Hospital = hospital,
            HospitalRequestId = hospitalRequest.Id,
            HospitalRequest = hospitalRequest,
            LabTestId = test.Id,
            LabTest = test,
            SpecimenTypeId = type.Id,
            SpecimenType = type,
            RequestingUserId = request.Actor.UserId,
            Status = SpecimenStatus.Taken,
            TakenAtUtc = request.CollectedAtUtc ?? now,
            TakenByUserId = request.Actor.UserId,
            RecordedAtUtc = now,
            Version = 0,
        };
        var received = new SpecimenChange(sample, null, request.Actor, now);
        received.Receive(now, null);

        // Its lab case opens (MC15), with no pathway: the lab's own.
        var opened = LabCaseChange.Open(
            number, test.Id, test, patient.Id, labTeamId.Value, SpecimenSource.Hospital, null, hospital.Id, hospitalRequest.Id,
            null, null, [sample], request.Actor, now.AddTicks(received.AuditEntries.Count));
        opened.Case.Hospital = hospital;
        opened.Case.HospitalRequest = hospitalRequest;
        hospitalRequest.LabCaseId = opened.Case.Id;

        repository.TrackRegistration(
            hospitalRequest, sample, isNew ? patient : null,
            isNew ? activityLog.Entry(ActivityAction.PatientCreated, patient.Id) : null,
            received.AuditEntries);
        labCases.Track(opened, isNew: true);
        await labCases.SaveAsync(cancellationToken);

        return HospitalMapper.ToResponse(hospitalRequest, opened.Case.Status, request.Actor.Username);
    }

    /// <summary>The patient chosen by a person from the matches, or a new record made with the request.</summary>
    private async Task<(Patient Patient, bool IsNew)> PatientAsync(RegisterHospitalRequestCommand request, DateTime nowUtc, CancellationToken ct)
    {
        if (request.PatientId is { } patientId)
        {
            var chosen = await repository.GetPatientForUpdateAsync(patientId, ct)
                         ?? throw new NotFoundException($"Patient '{patientId}' was not found.");
            PatientRules.EnsureCanChange(chosen);
            return (chosen, false);
        }

        var details = request.NewPatient!;
        var nhsNumber = NhsNumbers.Normalise(details.NhsNumber);
        if (nhsNumber is not null && await repository.NhsNumberInUseAsync(nhsNumber, ct))
            throw new ConflictException(NhsNumberRules.InUse);

        return (new Patient
        {
            Id = Guid.NewGuid(),
            FirstName = details.FirstName.Trim(),
            LastName = details.LastName.Trim(),
            DateOfBirth = details.DateOfBirth,
            NhsNumber = nhsNumber,
            CreatedAt = nowUtc,
            LastUpdated = nowUtc,
        }, true);
    }
}

/// <summary>
/// A page of hospital requests: the lab's and admins' (anyone else an empty page, as the light demo). Each patient on
/// the page is recorded as listed.
/// </summary>
public sealed class SearchHospitalRequestsHandler(
    IHospitalRepository repository,
    ITeamRepository teams,
    IActivityLog activityLog)
    : IRequestHandler<SearchHospitalRequestsQuery, PagedResponse<HospitalRequestSummaryResponse>>
{
    public async Task<PagedResponse<HospitalRequestSummaryResponse>> Handle(
        SearchHospitalRequestsQuery request, CancellationToken cancellationToken)
    {
        var labTeamId = (await teams.GetLabAsync(cancellationToken))?.Id;
        if (!LabWalls.SeesAll(request.Viewer.Teams, labTeamId))
            return PagedMapper.ToResponse(new PagedResult<HospitalRequestSummaryResponse>([], 0), request.Page, request.PageSize);

        var result = await repository.SearchAsync(request, cancellationToken);
        var statuses = await repository.GetCaseStatusesAsync(result.Items.Select(r => r.LabCaseId).ToList(), cancellationToken);
        await activityLog.RecordViewsAsync(
            ActivityAction.PatientListed, result.Items.Select(r => r.PatientId).Distinct(), cancellationToken);

        return PagedMapper.ToResponse(result, request.Page, request.PageSize,
            r => HospitalMapper.ToSummary(r, statuses.TryGetValue(r.LabCaseId, out var status) ? status : null));
    }
}

/// <summary>
/// One hospital request: the lab's and admins'; anyone else a 404 (recorded, as a missing one). Viewing it records
/// <c>LabCaseViewed</c> with its case and why it's seen (<c>access</c>).
/// </summary>
public sealed class GetHospitalRequestHandler(
    IHospitalRepository repository,
    ITeamRepository teams,
    ITeamBoundary boundary,
    IIdentityReadService identity,
    IActivityLog activityLog)
    : IRequestHandler<GetHospitalRequestQuery, HospitalRequestResponse>
{
    public async Task<HospitalRequestResponse> Handle(GetHospitalRequestQuery request, CancellationToken cancellationToken)
    {
        var hospitalRequest = await repository.GetRequestAsync(request.Id, cancellationToken)
                              ?? throw HospitalRequestRules.NotFound(request.Id);
        var scope = request.Viewer.Teams;
        var labTeamId = (await teams.GetLabAsync(cancellationToken))?.Id;
        if (!LabWalls.SeesAll(scope, labTeamId))
            throw await boundary.RefuseAsync(
                HospitalRequestRules.TargetKind, hospitalRequest.Id, HospitalRequestRules.NotFound(request.Id).Message, cancellationToken);

        var access = scope.IsSystem ? TeamAccessKind.System : scope.SeesAllTeams ? TeamAccessKind.Admin : TeamAccessKind.Owner;
        await activityLog.RecordViewAsync(
            ActivityAction.LabCaseViewed, hospitalRequest.PatientId,
            new { labCaseId = hospitalRequest.LabCaseId, access = access.ToString() }, cancellationToken);

        var statuses = await repository.GetCaseStatusesAsync([hospitalRequest.LabCaseId], cancellationToken);
        var usernames = await identity.GetUsernamesByIdAsync([hospitalRequest.RegisteredByUserId], cancellationToken);
        return HospitalMapper.ToResponse(
            hospitalRequest,
            statuses.TryGetValue(hospitalRequest.LabCaseId, out var status) ? status : null,
            usernames.TryGetValue(hospitalRequest.RegisteredByUserId, out var name) ? name : PathwayMapper.DeletedUser);
    }
}
