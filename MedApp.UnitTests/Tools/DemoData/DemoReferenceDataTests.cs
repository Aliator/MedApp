using FluentAssertions;
using MedApp.Application.Attachments.Common;
using MedApp.DevMcp.DemoData;
using MedApp.Domain.Patients;
using MedApp.Domain.Pathways.PatientPathways;
using MedApp.Domain.Referrals;
using MedApp.Domain.Terminology;
using static MedApp.UnitTests.Common.Constants.DemoDataTestConstants;

namespace MedApp.UnitTests.Tools.DemoData;

/// <summary>The demo's fixed data: stage definitions, pathways, NHS numbers and the files it attaches.</summary>
[TestFixture]
public sealed class DemoReferenceDataTests
{
    [Test]
    public void Definitions_SnomedCodes_AreValidConceptIdsWithTerms_AndSomeAreLeftBlank()
    {
        var coded = DemoPathwayKinds.Definitions.Where(d => d.SnomedCode is not null).ToList();

        coded.Should().OnlyContain(d => SnomedCt.IsConceptId(d.SnomedCode) && !string.IsNullOrWhiteSpace(d.SnomedTerm));
        DemoPathwayKinds.Definitions.Should().Contain(d => d.SnomedCode == null && !d.IsArchived);
    }

    [Test]
    public void PathwayKinds_EveryStage_NamesADefinitionThatIsntArchived()
    {
        var active = DemoPathwayKinds.Definitions.Where(d => !d.IsArchived).Select(d => d.Name).ToHashSet();

        DemoPathwayKinds.PathwayKinds.SelectMany(p => p.Stages).Should().OnlyContain(s => active.Contains(s.Definition));
    }

    [Test]
    public void PathwayKinds_StagesWithAVisitHaveAVisitLength_AndOnlyThose()
    {
        var visits = DemoPathwayKinds.Definitions.ToDictionary(d => d.Name, d => d.Visit);

        foreach (var stage in DemoPathwayKinds.PathwayKinds.SelectMany(p => p.Stages).Where(s => s.VisitMinutes > 0))
            visits[stage.Definition].Should().NotBe(VisitRequirement.None, stage.Definition);
        foreach (var stage in DemoPathwayKinds.PathwayKinds.SelectMany(p => p.Stages)
                     .Where(s => visits[s.Definition] == VisitRequirement.Required))
            stage.VisitMinutes.Should().BePositive($"{stage.Definition} needs a visit, so the seed books one");
    }

    /// <summary>
    /// Every live definition is used by a pathway; the retired ones (the paper triage form, and the Referral received and
    /// Triage stages that referrals replaced, decision 26) by none; and the archived pathway is the only archived one.
    /// </summary>
    [Test]
    public void PathwayKinds_UseEveryLiveDefinition_AndNoneOfTheRetiredOnes()
    {
        var used = DemoPathwayKinds.PathwayKinds.SelectMany(p => p.Stages).Select(s => s.Definition).ToHashSet();

        DemoPathwayKinds.Definitions.Where(d => !d.IsArchived).Select(d => d.Name).Should().BeEquivalentTo(used);
        DemoPathwayKinds.Definitions.Where(d => d.IsArchived).Select(d => d.Name)
            .Should().BeEquivalentTo([DemoPathwayKinds.ReferralReceived, DemoPathwayKinds.Triage, "Paper triage form (retired)"]);
        DemoPathwayKinds.PathwayKinds.Should().ContainSingle(p => p.IsArchived).Which.PathwayKind.Should().Be(PathwayKind.SkinLesion2024);
        DemoPathwayKinds.PathwayKinds.Select(p => p.TemplateName).Should().OnlyHaveUniqueItems();
    }

    /// <summary>
    /// Visits are needed where the patient must be seen, suggested where a call often does, and never for the lab's work
    /// or a letter.
    /// </summary>
    [Test]
    public void Definitions_VisitRequirements_FitTheWork()
    {
        var visits = DemoPathwayKinds.Definitions.ToDictionary(d => d.Name, d => d.Visit);

        new[] { DemoPathwayKinds.FirstClinicVisit, DemoPathwayKinds.BiopsyOrSample, DemoPathwayKinds.Treatment, DemoPathwayKinds.BloodAndUrineTests, DemoPathwayKinds.ClinicReview, DemoPathwayKinds.BiomechanicsSession }
            .Should().OnlyContain(name => visits[name] == VisitRequirement.Required);
        new[] { DemoPathwayKinds.FollowUp, DemoPathwayKinds.ResultsAndTeamReview, DemoPathwayKinds.MedicinesReview }
            .Should().OnlyContain(name => visits[name] == VisitRequirement.Suggested);
        new[] { DemoPathwayKinds.ReferralReceived, DemoPathwayKinds.Triage, DemoPathwayKinds.Histology, DemoPathwayKinds.LabTests, DemoPathwayKinds.DischargeLetter }
            .Should().OnlyContain(name => visits[name] == VisitRequirement.None);
    }

    /// <summary>
    /// Every template has a clock-stop stage (decision 36): one of its own stages, where the patient is seen (the first
    /// clinic visit, or the diabetes clinic review), so the wait ends with being seen.
    /// </summary>
    [Test]
    public void PathwayKinds_EachHasAClockStopStage_WhereThePatientIsSeen()
    {
        var visits = DemoPathwayKinds.Definitions.ToDictionary(d => d.Name, d => d.Visit);

        foreach (var kind in DemoPathwayKinds.PathwayKinds)
        {
            kind.ClockStopIndex.Should().BeGreaterThanOrEqualTo(0, kind.TemplateName);
            visits[kind.ClockStop].Should().Be(VisitRequirement.Required, kind.TemplateName);
        }
    }

    /// <summary>
    /// Each team's waiting-time targets are ones the app accepts (1 to 365 days), shorter for higher priorities;
    /// the new service, which nobody belongs to, isn't one of the centre's original five.
    /// </summary>
    [Test]
    public void Teams_TargetsAreValid_AndTheNewServiceHasNoMembers()
    {
        foreach (var name in DemoTeams.Names.Append(DemoTeams.NewService))
        {
            var (urgent, high, medium, low) = DemoTeams.Targets(name);
            new[] { urgent, high, medium, low }.Should().OnlyContain(days => days >= 1 && days <= 365, name);
            urgent.Should().BeLessThanOrEqualTo(high);
            high.Should().BeLessThanOrEqualTo(medium);
            medium.Should().BeLessThanOrEqualTo(low);
        }

        DemoTeams.Names.Should().NotContain(DemoTeams.NewService);
        Enum.GetValues<StaffKind>().Should().OnlyContain(kind => !DemoTeams.For(kind).Contains(DemoTeams.NewService));
    }

    /// <summary>What staff "type" on referrals fits the app's limits, and the practices are distinct invented names.</summary>
    [Test]
    public void ReferralTexts_FitTheAppsLimits()
    {
        var random = new Random(Seed);
        var kinds = DemoPathwayKinds.PathwayKinds.SelectMany(p => ProcedureKinds.For(p.PathwayKind)).Append(null).ToList();

        for (var i = 0; i < 200; i++)
        {
            ReferralTexts.Reason(kinds[i % kinds.Count], random).Length.Should().BeInRange(20, Referral.MaxReasonLength);
            ReferralTexts.Clinician(random)?.Length.Should().BeLessThanOrEqualTo(Referral.MaxClinicianLength);
            foreach (var reason in Enum.GetValues<ReferralRejectionReason>())
                ReferralTexts.RejectionDetail(reason, random)?.Length.Should().BeLessThanOrEqualTo(Referral.MaxDetailLength);
            foreach (var reason in Enum.GetValues<ClockPauseReason>())
                ReferralTexts.PauseDetail(reason, random)?.Length.Should().BeLessThanOrEqualTo(ReferralClockPause.MaxDetailLength);
            foreach (var team in DemoTeams.Names)
            {
                var (elsewhere, detail) = ReferralTexts.Elsewhere(team, random);
                elsewhere.Length.Should().BeLessThanOrEqualTo(Referral.MaxRedirectedToLength);
                detail.Length.Should().BeInRange(1, Referral.MaxDetailLength);
            }
        }

        ReferralTexts.RejectionDetail(ReferralRejectionReason.Other, random).Should().NotBeNullOrWhiteSpace("Other needs a detail");
        ReferralTexts.PauseDetail(ClockPauseReason.Other, random).Should().NotBeNullOrWhiteSpace("Other needs a detail");
        ReferralTexts.WithdrawalDetail(random).Length.Should().BeInRange(1, Referral.MaxDetailLength);
        ReferralTexts.EnteredInErrorDetail.Length.Should().BeInRange(1, Referral.MaxDetailLength);
        ReferralTexts.Practices.Should().OnlyHaveUniqueItems().And.OnlyContain(p => p.Length <= Referral.MaxPracticeLength);
    }

    /// <summary>The two pathways that see only women, or only men, see only adults old enough; children only where the story says.</summary>
    [Test]
    public void Sees_TheTwoSingleSexPathways_SeeOnlyOlderAdults()
    {
        ProcedureKinds.Sees(PathwayKind.PostMenopausalBleeding).Should().Be((true, 50));
        ProcedureKinds.Sees(PathwayKind.Prostate).Should().Be((false, 50));
        ProcedureKinds.Sees(PathwayKind.Diabetes).Should().Be(((bool?)null, 0));
        Enum.GetValues<PathwayKind>().Where(ProcedureKinds.SeesChildren).Should().BeEquivalentTo([PathwayKind.InflammatorySkin, PathwayKind.Biomechanics]);
    }

    [Test]
    public void SyntheticNhsNumber_IsValidAndInThe999TestRange()
    {
        var numbers = Enumerable.Range(0, 300).Select(DemoPatients.SyntheticNhsNumber).ToList();

        numbers.Should().OnlyHaveUniqueItems();
        numbers.Should().OnlyContain(n => n.StartsWith("999") && NhsNumbers.IsValid(n) && !NhsNumbers.IsPlaceholder(n));
    }

    [Test]
    public void SyntheticFiles_AreRecognisedByTheAppsOwnTypeSniffer()
    {
        var random = new Random(Seed);

        Sniff(SyntheticFiles.Pdf("Report", ["Line one", "Line (two)"]), "Report.pdf").Should().Be(AttachmentFileTypes.Pdf);
        Sniff(SyntheticFiles.FlowTracePng(12, random), "Flow test.png").Should().Be(AttachmentFileTypes.Png);
        Sniff(SyntheticFiles.DiagramPng("Home programme", ["Stretch", "Strength"]), "Home programme.png").Should().Be(AttachmentFileTypes.Png);
        foreach (var site in DemoPathwayKinds.PathwayKinds.SelectMany(p => ProcedureKinds.For(p.PathwayKind)).SelectMany(k => k.Sites).Distinct())
            Sniff(SyntheticFiles.SiteMapPng(site), "Lesion site map.png").Should().Be(AttachmentFileTypes.Png, site);
        Sniff(SyntheticFiles.Text("Note", ["Called the patient."]), "Note.txt").ContentType.Should().StartWith("text/plain");

        static AttachmentFileType Sniff(byte[] bytes, string name) => FileTypeSniffer.Identify(new MemoryStream(bytes), name);
    }

    [Test]
    public void SyntheticFiles_PdfHasNoActiveContentAndSaysItsSynthetic()
    {
        var text = System.Text.Encoding.ASCII.GetString(SyntheticFiles.Pdf("Report", ["Line"]));

        FileTypeSniffer.PdfActiveContent.Should().OnlyContain(marker => !text.Contains(marker));
        text.Should().Contain(SyntheticFiles.Banner).And.EndWith("%%EOF\n");
    }
}
