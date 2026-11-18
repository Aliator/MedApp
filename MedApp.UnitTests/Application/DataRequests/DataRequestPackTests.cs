using FluentAssertions;
using MedApp.Application.DataRequests.Common;
using MedApp.Application.Patients.Common;
using MedApp.Contracts.Patients.Responses;
using MedApp.Domain.DataRequests;
using MedApp.UnitTests.Common.Constants;
using static MedApp.UnitTests.Common.Constants.DataRequestTestConstants;

namespace MedApp.UnitTests.Application.DataRequests;

/// <summary>What the review can withhold, what the pack leaves out, and the report's words.</summary>
[TestFixture]
public sealed class DataRequestPackTests
{
    private static readonly Guid PathwayId = Guid.NewGuid();
    private static readonly Guid KeptNoteId = Guid.NewGuid();
    private static readonly Guid OtherPatientNoteId = Guid.NewGuid();
    private static readonly Guid LetterId = Guid.NewGuid();
    private static readonly Guid LetterPdfId = Guid.NewGuid();
    private static readonly Guid FileId = Guid.NewGuid();

    private static PatientExportResponse Export() => new()
    {
        Patient = new ExportedPatientResponse { FirstName = "Sam", LastName = "Example", DateOfBirth = new DateOnly(1970, 1, 1) },
        Pathways =
        [
            new ExportedPathwayResponse
            {
                Id = PathwayId, Title = "Synthetic pathway", Status = "Blocked",
                Notes =
                [
                    new ExportedPathwayNoteResponse { Id = KeptNoteId, Author = "k.ashford", Text = "Kept *note*." },
                    new ExportedPathwayNoteResponse
                    {
                        Id = OtherPatientNoteId, Author = "k.ashford", Text = "Another patient's note.", IsAboutAnotherPatient = true,
                        DeletedAtUtc = PathwaysTestConstants.Now
                    }
                ]
            }
        ],
        NoteHistory =
        [
            new ExportedNoteVersionResponse { NoteId = OtherPatientNoteId, PathwayId = PathwayId, Version = 1, Text = "Another patient's note." }
        ],
        Letters = [new ExportedLetterResponse { Id = LetterId, TemplateName = "Clinic letter", Status = "Approved", Body = "Letter body.", PdfAttachmentId = LetterPdfId }],
        Attachments =
        [
            new ExportedAttachmentResponse { Id = LetterPdfId, FileName = "Clinic letter.pdf" },
            new ExportedAttachmentResponse { Id = FileId, FileName = "scan.pdf" }
        ],
        PathwayHistory =
        [
            new ExportedHistoryEntryResponse { Action = "LetterApproved", Details = $$"""{"letterId":"{{LetterId}}"}""" },
            new ExportedHistoryEntryResponse { Action = "Created", Details = "{}" }
        ],
        DataRequests = [new ExportedDataRequestResponse { Withheld = [new ExportedWithheldItemResponse { Reason = WithheldReason }] }]
    };

    private static DataRequestWithheldItem Withhold(DataRequestItemKind kind, Guid id) =>
        new() { Id = Guid.NewGuid(), Kind = kind, ItemId = id, Reason = WithheldReason };

    [Test]
    public void ReviewItems_ANoteAboutAnotherPatient_StartsWithheld_UntilAReviewDecides()
    {
        var request = Open();

        var before = DataRequestReviewItems.From(Export(), request);
        before.Should().Contain(i => i.ItemId == OtherPatientNoteId && i.IsWithheld
                                     && i.WithheldReason == DataRequestReviewItems.AboutAnotherPatientReason);
        before.Should().Contain(i => i.ItemId == KeptNoteId && !i.IsWithheld);
        before.Should().Contain(i => i.Kind == "Letter" && i.ItemId == LetterId && i.Text == "Letter body.");
        before.Should().Contain(i => i.Kind == "File" && i.ItemId == FileId);

        request.ReviewedAtUtc = PathwaysTestConstants.Now;
        request.WithheldItems.Add(Withhold(DataRequestItemKind.Note, KeptNoteId));
        var after = DataRequestReviewItems.From(Export(), request);
        after.Should().Contain(i => i.ItemId == KeptNoteId && i.IsWithheld && i.WithheldReason == WithheldReason);
        after.Should().Contain(i => i.ItemId == OtherPatientNoteId && !i.IsWithheld);
    }

    [Test]
    public void Filter_AWithheldNoteGoesWithItsVersions_AWithheldLetterWithItsPdfAndHistory()
    {
        var export = Export();

        var outcome = DataRequestPackFilter.Apply(export,
            [Withhold(DataRequestItemKind.Note, OtherPatientNoteId), Withhold(DataRequestItemKind.Letter, LetterId)]);

        export.Pathways.Single().Notes.Select(n => n.Id).Should().Equal(KeptNoteId);
        export.NoteHistory.Should().BeEmpty();
        export.Letters.Should().BeEmpty();
        export.Attachments.Select(a => a.Id).Should().Equal(FileId);
        export.PathwayHistory.Select(e => e.Action).Should().Equal("Created");
        export.DataRequests.Single().Withheld.Should().BeEmpty("saying what was withheld, or why, could reveal it");
        outcome.WithheldCount.Should().Be(2);
        outcome.WithheldFileIds.Should().Contain(LetterPdfId);
    }

    [Test]
    public void Report_HasWhatArticle15AsksFor_EscapesTheRecord_AndSaysSomethingWasWithheldOnlyWhenItWas()
    {
        var inputs = new DataRequestReport.Inputs(
            Export(), Ready(), "Synthetic Centre", new RetentionOptions(), WithheldCount: 0, [], [], Today);

        var body = DataRequestReport.Body(inputs);

        body.Should().Contain("## Why we hold your information")
            .And.Contain("## Who we share it with")
            .And.Contain("## How long we keep it")
            .And.Contain("## Where it came from")
            .And.Contain("## Your rights")
            .And.Contain(@"ico\.org\.uk")
            .And.Contain(@"Kept \*note\*\.")
            .And.Contain("On hold")
            .And.NotContain("serious harm");

        DataRequestReport.Body(inputs with { WithheldCount = 1 }).Should().Contain("serious harm");

        var document = DataRequestReport.Document(inputs);
        document.Title.Should().NotContain("Example");
        document.RecipientAddress.Should().Equal(RequesterName);
        document.PatientIdentifiers.Should().Contain("Sam Example");
    }

    [Test]
    public async Task Report_RendersWithTheLettersEngine()
    {
        var inputs = new DataRequestReport.Inputs(
            Export(), Ready(), "Synthetic Centre", new RetentionOptions(), WithheldCount: 1, [], ["missing.pdf"], Today);

        MedApp.Infrastructure.Letters.LetterFonts.Install();
        var pdf = await new MedApp.Infrastructure.Letters.PdfSharpLetterRenderer()
            .RenderAsync(DataRequestReport.Document(inputs), CancellationToken.None);

        System.Text.Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
    }

    [TestCase("InProgress", "In progress")]
    [TestCase("Blocked", "On hold")]
    [TestCase("Completed", "Discharged")]
    public void Words_PathwayStatus_AsStaffSeeIt(string status, string words)
    {
        Words.PathwayStatus(status).Should().Be(words);
    }
}
