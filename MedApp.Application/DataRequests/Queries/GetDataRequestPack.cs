using System.IO.Compression;
using System.Text.Json;
using MedApp.Application.Activity.Common;
using MedApp.Application.Attachments.Common;
using MedApp.Application.Calendar.Common;
using MedApp.Application.Common.Exceptions;
using MedApp.Application.Common.Identity;
using MedApp.Application.DataRequests.Common;
using MedApp.Application.DataRequests.Repositories;
using MedApp.Application.Letters.Common;
using MedApp.Application.Pathways.Common;
using MedApp.Application.Patients.Common;
using MedApp.Application.Patients.Repositories;
using MedApp.Application.Practices.Common;
using MedApp.Domain.Activity;
using MediatR;
using Microsoft.Extensions.Options;

namespace MedApp.Application.DataRequests.Queries;

/// <summary>The pack's download: its name and its bytes (a temporary file, deleted when the stream is closed).</summary>
public sealed record DataRequestPack(string FileName, Stream Content);

/// <summary>The pack's file type.</summary>
public static class DataRequestPackFile
{
    public const string ContentType = "application/zip";
}

/// <summary>Makes a data request's response pack (ManagePrivacy). Never stored.</summary>
public sealed record GetDataRequestPackQuery(Guid DataRequestId, PathwayActor Actor) : IRequest<DataRequestPack>;

/// <summary>
/// Builds the pack (decision 162) as a ZIP: the report as a PDF (<see cref="DataRequestReport"/>, by the letters'
/// renderer), the whole record as JSON (<c>record.json</c>, the export after the review's withholding), and the files
/// still held, leaving out withheld ones. 409 until the identity check and the review are recorded, or once the request
/// is closed or the record anonymised (<see cref="DataRequestRules.WhyNoPack"/>). Recorded first as
/// <see cref="ActivityAction.DataRequestPackDownloaded"/> (fails closed), so the access log in the pack includes it.
/// It's written to a temporary file that's deleted when the download ends, never kept.
/// </summary>
public sealed class GetDataRequestPackHandler(
    IDataRequestRepository repository,
    IPatientRecordRepository records,
    IIdentityReadService identity,
    IFileStore fileStore,
    ILetterRenderer renderer,
    IActivityLog activityLog,
    IOptions<LettersOptions> letters,
    IOptions<RetentionOptions> retention,
    TimeProvider timeProvider)
    : IRequestHandler<GetDataRequestPackQuery, DataRequestPack>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task<DataRequestPack> Handle(GetDataRequestPackQuery request, CancellationToken cancellationToken)
    {
        DataRequestRules.EnsureCanHandle(request.Actor);
        var dataRequest = await repository.GetAsync(request.DataRequestId, cancellationToken)
                          ?? throw new NotFoundException($"Data request '{request.DataRequestId}' was not found.");
        if (DataRequestRules.WhyNoPack(dataRequest) is { } why)
            throw new ConflictException(why);

        var record = await records.GetAsync(dataRequest.PatientId, forUpdate: false, cancellationToken)
                     ?? throw new NotFoundException($"Patient '{dataRequest.PatientId}' was not found.");

        var entry = activityLog.Entry(ActivityAction.DataRequestPackDownloaded, dataRequest.PatientId, new { dataRequestId = dataRequest.Id });
        await activityLog.RecordAsync(entry, cancellationToken);
        var withEntry = record with { Activity = [.. record.Activity, entry] };

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var usernames = await identity.GetUsernamesByIdAsync(PatientExportMapper.UserIds(withEntry), cancellationToken);
        var export = PatientExportMapper.ToResponse(withEntry, usernames, now, activityLog.Actor.Username);
        var outcome = DataRequestPackFilter.Apply(export, dataRequest.WithheldItems);

        var path = Path.Combine(Path.GetTempPath(), $"medapp-pack-{Guid.NewGuid():N}.zip");
        var stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920,
            FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        try
        {
            var included = new List<string>();
            var unreadable = new List<string>();
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                // The files still held: deleted ones are only listed (nobody can download them).
                var index = 0;
                foreach (var file in export.Attachments.Where(a => a.DeletedAtUtc is null))
                {
                    var name = $"{++index:000} {AttachmentFileName.Clean(file.FileName) ?? "file"}";
                    await using var content = await fileStore.OpenReadAsync(AttachmentStorageKeys.File(file.Id), cancellationToken);
                    if (content is null)
                    {
                        unreadable.Add(file.FileName);
                        continue;
                    }

                    var zipEntry = zip.CreateEntry($"{DataRequestReport.FilesFolder}/{name}", CompressionLevel.Optimal);
                    await using (var target = zipEntry.Open())
                        await content.CopyToAsync(target, cancellationToken);
                    included.Add(name);
                }

                var recordEntry = zip.CreateEntry(DataRequestReport.RecordFileName, CompressionLevel.Optimal);
                await using (var target = recordEntry.Open())
                    await JsonSerializer.SerializeAsync(target, export, Json, cancellationToken);

                var inputs = new DataRequestReport.Inputs(
                    export, dataRequest, letters.Value.SecureEmail.CentreName, retention.Value, outcome.WithheldCount,
                    included, unreadable, UkTime.Today(timeProvider));
                var pdf = await renderer.RenderAsync(DataRequestReport.Document(inputs), cancellationToken);
                var pdfEntry = zip.CreateEntry(DataRequestReport.PdfFileName, CompressionLevel.Optimal);
                await using (var target = pdfEntry.Open())
                    await target.WriteAsync(pdf, cancellationToken);
            }

            stream.Position = 0;
            return new DataRequestPack($"Data request {UkTime.Today(timeProvider):yyyy-MM-dd}.zip", stream);
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
    }
}
