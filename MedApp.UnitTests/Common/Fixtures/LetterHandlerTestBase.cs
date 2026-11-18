using AutoFixture;
using MedApp.Application.Activity.Common;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Common.Identity;
using MedApp.Application.Letters.Common;
using MedApp.Application.Letters.Repositories;
using MedApp.Application.Notifications.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Pathways.Repositories;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Practices.Common;
using MedApp.Application.Practices.Repositories;
using MedApp.Application.Referrals.Repositories;
using MedApp.Application.Sites.Repositories;
using MedApp.Domain.Attachments;
using MedApp.Domain.Letters;
using MedApp.Domain.Notifications;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Patients;
using MedApp.Domain.Practices;
using MedApp.Domain.Referrals;
using MedApp.Domain.Sites;
using MedApp.UnitTests.Common.Constants;
using Microsoft.Extensions.Options;
using Moq;

namespace MedApp.UnitTests.Common.Fixtures;

/// <summary>
/// Letters' handlers with the real <see cref="LetterRecords"/> over mocked repositories: a synthetic patient with an
/// address and a registered practice, a pathway, staff with full names, a secure email relay that's set up, a renderer
/// whose font shows everything, and a file store in memory. <see cref="Given(Letter)"/> puts a letter in.
/// </summary>
public abstract class LetterHandlerTestBase
{
    protected IFixture Fixture { get; private set; } = null!;
    protected Mock<ILetterRepository> Letters { get; private set; } = null!;
    protected Mock<ILetterTransport> Transport { get; private set; } = null!;
    protected Mock<ILetterRenderer> Renderer { get; private set; } = null!;
    protected Mock<IStaffDetailsService> Staff { get; private set; } = null!;
    protected Mock<IPermissionHolders> Holders { get; private set; } = null!;
    protected Mock<IPatientPathwayRepository> Pathways { get; private set; } = null!;
    protected Mock<IReferralRepository> Referrals { get; private set; } = null!;
    protected InMemoryFileStore Store { get; private set; } = null!;
    protected RecordingActivityLog Activity { get; private set; } = null!;
    protected List<Notification> Sent { get; } = [];
    protected List<PatientPathwayAuditEntry> SavedAudit { get; } = [];
    protected List<Attachment> SavedFiles { get; } = [];
    protected List<Letter> Added { get; } = [];
    protected LettersOptions Options { get; private set; } = null!;

    protected Patient Patient { get; private set; } = null!;
    protected Practice Practice { get; private set; } = null!;
    protected PatientPathway Pathway { get; private set; } = null!;

    protected static readonly byte[] Pdf = "%PDF-1.7 synthetic"u8.ToArray();

    [SetUp]
    public void SetUpLetterMocks()
    {
        Fixture = AutoFixtureFactory.Create();
        SavedAudit.Clear();
        SavedFiles.Clear();
        Sent.Clear();
        Added.Clear();

        Practice = PracticesTestConstants.CreatePractice();
        Practice.SecureEmail = PracticesTestConstants.SecureEmail;
        Patient = PatientsTestConstants.CreateValidPatient();
        Patient.AddressLines = "2 Invented Road\nFictionton";
        Patient.Postcode = "ZZ1 2ZZ";
        Patient.PracticeId = Practice.Id;
        Patient.NhsNumber = "9990000018";
        Pathway = PathwaysTestConstants.CreatePathway(PatientPathwayStatus.InProgress, patient: Patient);
        Pathway.SpecialtyId = PathwaysTestConstants.SpecialtyId;

        Letters = new Mock<ILetterRepository>();
        Letters.Setup(l => l.AddAsync(It.IsAny<Letter>(), It.IsAny<IEnumerable<PatientPathwayAuditEntry>>(), It.IsAny<CancellationToken>()))
            .Callback((Letter letter, IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken _) =>
            {
                Added.Add(letter);
                SavedAudit.AddRange(audit);
            })
            .Returns(Task.CompletedTask);
        Letters.Setup(l => l.SaveAsync(It.IsAny<IEnumerable<PatientPathwayAuditEntry>>(), It.IsAny<CancellationToken>()))
            .Callback((IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken _) => SavedAudit.AddRange(audit))
            .Returns(Task.CompletedTask);
        Letters.Setup(l => l.SaveApprovalAsync(It.IsAny<Attachment>(), It.IsAny<IEnumerable<PatientPathwayAuditEntry>>(), It.IsAny<CancellationToken>()))
            .Callback((Attachment file, IEnumerable<PatientPathwayAuditEntry> audit, CancellationToken _) =>
            {
                SavedFiles.Add(file);
                SavedAudit.AddRange(audit);
            })
            .Returns(Task.CompletedTask);
        Letters.Setup(l => l.ReplaceRecipients(It.IsAny<Letter>(), It.IsAny<IReadOnlyList<LetterRecipient>>()))
            .Callback((Letter letter, IReadOnlyList<LetterRecipient> recipients) =>
            {
                letter.Recipients.Clear();
                letter.Recipients.AddRange(recipients);
            });
        Letters.Setup(l => l.GetCorrectionIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);
        Letters.Setup(l => l.GetLastAttendedAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((DateTime?)null);

        Pathways = new Mock<IPatientPathwayRepository>();
        Pathways.Setup(p => p.GetByIdAsync(Pathway.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Pathway);
        Referrals = new Mock<IReferralRepository>();
        Referrals.Setup(r => r.GetForPathwayAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Referral?)null);
        var patients = new Mock<IPatientRepository>();
        patients.Setup(p => p.GetByIdAsync(Patient.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Patient);

        var identity = new Mock<IIdentityReadService>();
        identity.Setup(i => i.GetUsernamesByIdAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Guid> ids, CancellationToken _) =>
                (IReadOnlyDictionary<Guid, string>)LettersTestConstants.Usernames.Where(kv => ids.Contains(kv.Key)).ToDictionary());

        Staff = new Mock<IStaffDetailsService>();
        Staff.Setup(s => s.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => LettersTestConstants.StaffDetails.GetValueOrDefault(id));

        var sites = new Mock<ISiteRepository>();
        sites.Setup(s => s.GetSiteAsync(Site.MainSiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Site { Id = Site.MainSiteId, Name = Site.MainSiteName });

        Transport = new Mock<ILetterTransport>();
        Transport.SetupGet(t => t.IsAvailable).Returns(true);
        Transport.Setup(t => t.SendAsync(It.IsAny<SecureLetterEmail>(), It.IsAny<CancellationToken>())).ReturnsAsync("<ref@relay>");

        Renderer = LettersTestConstants.RendererMissing();
        Renderer.Setup(r => r.RenderAsync(It.IsAny<LetterDocument>(), It.IsAny<CancellationToken>())).ReturnsAsync(Pdf);

        Holders = new Mock<IPermissionHolders>();
        Holders.Setup(h => h.WhoHoldsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), Permission.ApproveLetters, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<Guid> ids, Permission _, CancellationToken _) =>
                ids.Where(id => id == LettersTestConstants.Approver.UserId || id == LettersTestConstants.ClinicianAuthor.UserId).ToHashSet());

        Options = new LettersOptions
        {
            SecureEmail = new LetterSecureEmailOptions
            {
                Host = "relay.invalid", FromAddress = "centre@nhs.net", FromName = "Synthetic Specialist Centre", RelayMeetsDcb1596 = true,
            },
        };

        Store = new InMemoryFileStore();
        Activity = new RecordingActivityLog();
        var notifications = new Mock<INotificationSender>();
        notifications.Setup(n => n.SendAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .Callback((Notification n, CancellationToken _) => Sent.Add(n))
            .Returns(Task.CompletedTask);

        Fixture.Inject(Letters.Object);
        Fixture.Inject(Pathways.Object);
        Fixture.Inject(Referrals.Object);
        Fixture.Inject(patients.Object);
        var practices = PracticesTestConstants.RepositoryWith(Practice).Object;
        Fixture.Inject(practices);
        Fixture.Inject(identity.Object);
        Fixture.Inject(Staff.Object);
        Fixture.Inject(sites.Object);
        Fixture.Inject(Transport.Object);
        Fixture.Inject(Renderer.Object);
        Fixture.Inject(Holders.Object);
        Fixture.Inject<IOptions<LettersOptions>>(Microsoft.Extensions.Options.Options.Create(Options));
        Fixture.Inject<IFileStore>(Store);
        Fixture.Inject<IActivityLog>(Activity);
        Fixture.Inject(notifications.Object);
        Fixture.Inject<TimeProvider>(new FixedTimeProvider(LettersTestConstants.NowUtc));
        Fixture.Inject(new LetterRecords(
            Letters.Object, patients.Object, Pathways.Object, Referrals.Object, practices,
            identity.Object, Staff.Object, sites.Object, Transport.Object, Microsoft.Extensions.Options.Options.Create(Options)));
    }

    /// <summary>Puts a letter in the repository, read-only and for update (the same object).</summary>
    protected Letter Given(Letter letter)
    {
        Letters.Setup(l => l.GetByIdAsync(letter.Id, It.IsAny<CancellationToken>())).ReturnsAsync(letter);
        Letters.Setup(l => l.GetForUpdateAsync(letter.Id, It.IsAny<CancellationToken>())).ReturnsAsync(letter);
        return letter;
    }

    /// <summary>A letter on <see cref="Pathway"/> by <paramref name="author"/>, with the practice and the patient as recipients.</summary>
    protected Letter OnPathway(
        LetterStatus status = LetterStatus.Draft,
        PathwayActor? author = null,
        Guid? approverId = null,
        LetterKind kind = LetterKind.ClinicLetter) =>
        LettersTestConstants.CreateLetter(Pathway, Practice, status, author ?? LettersTestConstants.Author, approverId, kind);
}
