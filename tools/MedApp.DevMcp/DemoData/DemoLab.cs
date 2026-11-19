using MedApp.Application.Hospitals.Requests;
using MedApp.Application.Lab.Cases.Commands;
using MedApp.Contracts.Hospitals.Requests;
using Microsoft.EntityFrameworkCore;
using static MedApp.DevMcp.DemoData.DemoReplay;

namespace MedApp.DevMcp.DemoData;

/// <summary>
/// The hospital's work in the lab (the light demo's <c>slices/hospitals.ts</c> and <c>labCases.ts</c>, MC18 to MC20):
/// requests from the nearby hospital, typed in by the lab's pathologist, each opening a lab case of its own with no
/// pathway. Two of its patients are linked to the centre only by these (hidden from other staff's search); Samuel
/// Okafor, a centre patient too, is matched to his record. Two earlier biopsies were reported and sent back; one of
/// them is closed since the hospital's surgery details came.
/// </summary>
public static class DemoLab
{
    private const string SurgeonA = "Ms A. Example";
    private const string Report = "Skin, punch biopsy: squamous cell carcinoma, well differentiated.";

    public static async Task CreateAsync(DemoSeed seed)
    {
        var t = seed.Timeline;

        // Wilfred Ashdown's diagnostic biopsy (MC20): reported and sent back ten days ago, then closed when the
        // hospital's surgery details came.
        var wilfredBiopsy = await RegisterAsync(seed, 16, null, DemoPatients.WilfredAshdown, "RG-204817", SurgeonA, "Urgent", true, null);
        var wilfredId = await seed.Db(db => db.HospitalRequests.Where(r => r.Id == wilfredBiopsy).Select(r => r.PatientId).SingleAsync(seed.Ct));
        await ReportAsync(seed, wilfredBiopsy, t.Ago(10, 11, 0));
        await AddSurgeryDetailsAsync(seed, wilfredBiopsy, t.Ago(2, 9, 0), "Wide local excision", "Completed; the excision sent to the lab.", null);

        // Samuel Okafor's earlier biopsy (MC20): reported and sent twelve days ago, awaiting the hospital's surgery details.
        var samuel = seed.Patients[DemoPatients.SamuelOkafor];
        var samuelBiopsy = await RegisterAsync(seed, 18, samuel, null, "RG-118953", SurgeonA, "Routine", false, null);
        await ReportAsync(seed, samuelBiopsy, t.Ago(12, 11, 0));
        seed.Tour["labCaseAwaitingSurgeryDetails"] = await CaseOfRequestAsync(seed, samuelBiopsy);

        // Samuel Okafor's excision: received nine days ago, so past the lab's 7 (breached, MC19), away for a second opinion.
        var samuelExcision = await RegisterAsync(seed, 9, samuel, null, "RG-118953", SurgeonA, "Routine", false, t.DaysFrom(6));
        await WorkCaseAsync(seed, await CaseOfRequestAsync(seed, samuelExcision), t.Ago(9, 9, 35), t.HoursAgo(5),
        [
            .. DemoReplay.Histology,
            new LabWork(Choice: "NeedsMoreWork", Outcome: "SecondOpinion"),
            LabWork.TestStep("Sent for a second opinion", "Regional dermatopathology service (synthetic)"),
        ]);

        // Wilfred Ashdown's excision: urgent, on a cancer pathway, the report needed in 3 days (sooner than the lab's 7:
        // its clock is the hospital's, MC19); its surgery details came with it; worked up to "Needs more work?".
        var wilfredExcision = await RegisterAsync(seed, 2, wilfredId, null, "RG-204817", SurgeonA, "Urgent", true, t.DaysFrom(3));
        var excisionCase = await CaseOfRequestAsync(seed, wilfredExcision);
        seed.Tour["hospitalRequest"] = wilfredExcision;
        seed.Tour["labCaseAtChoice"] = excisionCase;
        await AddSurgeryDetailsAsync(seed, wilfredExcision, t.Ago(2, 9, 45), "Wide local excision, left forearm", null, "Suture marks 12 o’clock.");
        await WorkCaseAsync(seed, excisionCase, t.Ago(2, 9, 50), t.HoursAgo(3), DemoReplay.Histology);

        // Maya Thistlewood's biopsy: routine, no date given, just received.
        var maya = await RegisterAsync(seed, 1, null, DemoPatients.MayaThistlewood, "RG-311402", "Mr B. Sample", "Routine", false, null);
        seed.Tour["labCaseJustReceived"] = await CaseOfRequestAsync(seed, maya);
    }

    /// <summary>
    /// The lab types in a hospital request, received <paramref name="daysAgo"/> days ago: written the afternoon before,
    /// the sample collected that morning. Its case opens with it, the sample received.
    /// </summary>
    private static async Task<Guid> RegisterAsync(
        DemoSeed seed, int daysAgo, Guid? patientId, DemoPatient? newPatient, string hospitalNumber, string surgeon, string priority, bool cancer, DateOnly? neededBy)
    {
        var t = seed.Timeline;
        var lab = DemoPeople.Aldous;
        var request = await seed.Send(t.Ago(daysAgo, 9, 30), lab, new RegisterHospitalRequestCommand(
            seed.Hospitals[DemoSetup.Riverside],
            patientId,
            newPatient is null ? null : new HospitalPatientRequest
            {
                FirstName = newPatient.FirstName,
                LastName = newPatient.LastName,
                DateOfBirth = newPatient.DateOfBirth,
                NhsNumber = newPatient.NhsNumber,
            },
            hospitalNumber,
            newPatient?.NhsNumber ?? DemoPatients.All.Single(p => p.Name == DemoPatients.SamuelOkafor).NhsNumber,
            surgeon,
            priority,
            cancer,
            t.Ago(daysAgo + 1, 16, 0),
            neededBy,
            t.Ago(daysAgo + 1, 11, 15),
            seed.SpecimenTypes[DemoSetup.SkinBiopsy],
            seed.LabTests[DemoSetup.SkinHistology],
            lab.Actor), "register a hospital request");
        return request.Id;
    }

    private static async Task<Guid> CaseOfRequestAsync(DemoSeed seed, Guid requestId) =>
        await seed.Db(db => db.HospitalRequests.Where(r => r.Id == requestId).Select(r => r.LabCaseId).SingleAsync(seed.Ct));

    /// <summary>Works the case through histology, then authorises its report, which goes back to the hospital (MC20).</summary>
    private static async Task ReportAsync(DemoSeed seed, Guid requestId, DateTime reportedAt)
    {
        var labCase = await CaseOfRequestAsync(seed, requestId);
        var receivedAt = await seed.Db(db => db.LabCases.Where(c => c.Id == labCase).Select(c => c.ReceivedAtUtc).SingleAsync(seed.Ct)) ?? reportedAt.AddDays(-5);
        await WorkCaseAsync(seed, labCase, receivedAt, reportedAt.AddMinutes(-30), [.. DemoReplay.Histology, LabWork.NoMoreWork]);
        await AuthoriseAsync(seed, labCase, reportedAt, Report);
    }

    /// <summary>What the hospital sent after its request, typed in by the lab (free text, as written).</summary>
    private static async Task AddSurgeryDetailsAsync(DemoSeed seed, Guid requestId, DateTime at, string procedure, string? outcome, string? detail)
    {
        var labCase = await CaseOfRequestAsync(seed, requestId);
        var version = await seed.Db(db => db.LabCases.Where(c => c.Id == labCase).Select(c => c.Version).SingleAsync(seed.Ct));
        await seed.Send(at, DemoPeople.Aldous, new AddSurgeryDetailsCommand(labCase, seed.Timeline.DaysFrom(-3), procedure, outcome, false, detail, version,
            DemoPeople.Aldous.Actor), "add the hospital's surgery details");
    }
}
