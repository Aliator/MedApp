import {QueryClient} from '@tanstack/react-query';
import {renderHook, waitFor} from '@testing-library/react';
import type {ReactNode} from 'react';
import {AppProviders} from '../app/AppProviders';
import {calls, json, mockApi} from '../test/render';
import {stubXhr} from '../test/xhr';
import {
  allowedTypesText,
  attachedToOf,
  attachmentContentUrl,
  attachmentKind,
  attachmentLimits,
  attachmentsQuery,
  attachmentTargetKey,
  attachmentThumbnailUrl,
  checkAttachmentFile,
  DEFAULT_ATTACHMENT_LIMITS,
  fileExtension,
  isUploadCancelled,
  uploadAttachment,
  uploadErrorText,
  useDeleteAttachment,
} from './attachments';
import {ApiError} from './errors';

const patientId = '00000035-0000-0000-0000-000000000001';
const procedureId = '00000035-0000-0000-0000-000000000002';
const stageId = '00000035-0000-0000-0000-000000000003';
const attachmentId = '00000035-0000-0000-0000-000000000004';
const MB = 1024 * 1024;

const file = (name: string, size = 10) => new File([new Uint8Array(size)], name, {type: 'application/octet-stream'});

describe('attachment targets', () => {
  it('names what a target is, and keys it', () => {
    expect(attachedToOf({patientId})).toBe('Patient');
    expect(attachedToOf({procedureId})).toBe('Procedure');
    expect(attachedToOf({stageId})).toBe('Stage');
    expect(attachmentTargetKey({stageId})).toBe(`Stage:${stageId}`);
  });

  it('gives the download and thumbnail addresses', () => {
    expect(attachmentContentUrl(attachmentId)).toBe(`/api/attachments/${attachmentId}/content`);
    expect(attachmentThumbnailUrl(attachmentId)).toBe(`/api/attachments/${attachmentId}/thumbnail`);
  });
});

describe('attachmentsQuery', () => {
  it.each([
    [{patientId}, `PatientId=${patientId}`],
    [{procedureId}, `ProcedureId=${procedureId}`],
    [{stageId}, `StageId=${stageId}`],
  ])('fetches the files on %o', async (target, query) => {
    const fetch = mockApi({'GET /api/attachments': () => json({items: [], totalCount: 0, maxFileBytes: MB, allowedExtensions: ['.pdf']})});

    const list = await new QueryClient().fetchQuery(attachmentsQuery(target));

    expect(list.maxFileBytes).toBe(MB);
    expect(calls(fetch)).toEqual([`GET /api/attachments?${query}`]);
  });

  it('asks for deleted files too, under a key of its own', async () => {
    const fetch = mockApi({'GET /api/attachments': () => json({items: []})});

    await new QueryClient().fetchQuery(attachmentsQuery({patientId}, true));

    expect(calls(fetch)).toEqual([`GET /api/attachments?PatientId=${patientId}&IncludeDeleted=true`]);
    expect(attachmentsQuery({patientId}, true).queryKey).toEqual(['attachments', {patientId, includeDeleted: true}]);
    expect(attachmentsQuery({patientId}).queryKey).toEqual(['attachments', {patientId}]);
  });
});

describe('attachment limits', () => {
  it("takes the list's limits, or the defaults before it loads", () => {
    expect(attachmentLimits(undefined)).toEqual(DEFAULT_ATTACHMENT_LIMITS);
    expect(attachmentLimits({items: [], maxFileBytes: 0, allowedExtensions: []})).toEqual(DEFAULT_ATTACHMENT_LIMITS);
    expect(attachmentLimits({maxFileBytes: MB, allowedExtensions: ['.PDF', '.txt']})).toEqual({maxFileBytes: MB, allowedExtensions: ['.pdf', '.txt']});
  });

  it('lists the types in words', () => {
    expect(allowedTypesText(DEFAULT_ATTACHMENT_LIMITS)).toBe('PDF, PNG, JPG, JPEG, HEIC, HEIF, DOCX or TXT');
    expect(allowedTypesText({maxFileBytes: MB, allowedExtensions: ['.pdf']})).toBe('PDF');
    expect(allowedTypesText({maxFileBytes: MB, allowedExtensions: []})).toBe('');
  });

  it("reads a name's extension", () => {
    expect(fileExtension('scan.final.PDF')).toBe('.pdf');
    expect(fileExtension('notes')).toBe('');
    expect(fileExtension('.hidden')).toBe('');
    expect(fileExtension('trailing.')).toBe('');
  });

  it('refuses a file of another type, without a type, or too large, naming it', () => {
    expect(checkAttachmentFile({name: 'scan.pdf', size: 20 * MB}, DEFAULT_ATTACHMENT_LIMITS)).toBeNull();
    expect(checkAttachmentFile({name: 'scan.tiff', size: 10}, DEFAULT_ATTACHMENT_LIMITS)).toBe(
      "scan.tiff: TIFF files can't be attached. Attach PDF, PNG, JPG, JPEG, HEIC, HEIF, DOCX or TXT.",
    );
    expect(checkAttachmentFile({name: 'notes', size: 10}, DEFAULT_ATTACHMENT_LIMITS)).toMatch(
      /^notes: files without a type at the end of their name \(such as \.pdf\) can't be attached\./,
    );
    expect(checkAttachmentFile({name: 'photo.jpg', size: 31.2 * MB}, DEFAULT_ATTACHMENT_LIMITS)).toBe('photo.jpg is 31.2 MB; the most is 20 MB.');
  });
});

describe('attachmentKind', () => {
  it.each([
    [{contentType: 'application/pdf', fileName: 'a.pdf'}, 'pdf'],
    [{contentType: null, fileName: 'a.pdf'}, 'pdf'],
    [{contentType: 'image/jpeg', fileName: 'a.jpg'}, 'image'],
    [{contentType: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document', fileName: 'a.docx'}, 'word'],
    [{contentType: undefined, fileName: 'a.docx'}, 'word'],
    [{contentType: 'text/plain', fileName: 'a.txt'}, 'text'],
    [{contentType: 'application/zip', fileName: null}, 'other'],
  ] as const)('%o is %s', (attachment, kind) => {
    expect(attachmentKind(attachment)).toBe(kind);
  });
});

describe('uploadAttachment', () => {
  it("sends the file's bytes with its name encoded, reports progress, and resolves with the attachment", async () => {
    const sent = stubXhr();
    const progress: number[] = [];
    const upload = file('Überweisung März.pdf');

    const result = uploadAttachment({target: {stageId}, file: upload, onProgress: (p) => progress.push(p)});
    const xhr = sent[0];
    xhr.progress(5, 10);
    xhr.progress(5, 0);
    xhr.progress(5, 10, false);
    xhr.respond(201, {id: attachmentId, fileName: 'Überweisung März.pdf'});

    await expect(result).resolves.toEqual({id: attachmentId, fileName: 'Überweisung März.pdf'});
    expect(xhr.method).toBe('POST');
    expect(xhr.url).toBe(`/api/attachments?stageId=${stageId}`);
    expect(xhr.withCredentials).toBe(true);
    expect(xhr.headers).toEqual({'Content-Type': 'application/octet-stream', 'X-File-Name': '%C3%9Cberweisung%20M%C3%A4rz.pdf'});
    expect(xhr.body).toBe(upload);
    expect(progress).toEqual([0.5, 1]);
  });

  it.each([
    [{patientId}, `patientId=${patientId}`],
    [{procedureId}, `procedureId=${procedureId}`],
  ])('attaches to %o', async (target, query) => {
    const sent = stubXhr();
    const result = uploadAttachment({target, file: file('a.pdf')});
    sent[0].respond(201, {id: attachmentId});
    await result;
    expect(sent[0].url).toBe(`/api/attachments?${query}`);
  });

  it.each([
    [409, {title: "The 'Consultation' stage is finished and locked. An admin can unlock it."}, "The 'Consultation' stage is finished and locked. An admin can unlock it."],
    [413, {title: 'The file is larger than 20 MB, the most that can be attached.'}, 'The file is larger than 20 MB, the most that can be attached.'],
    [415, {title: "SVG images can't be attached. Attach PDF, PNG, …"}, "SVG images can't be attached. Attach PDF, PNG, …"],
    [422, {title: "The file failed its malware scan, so it wasn't attached."}, "The file failed its malware scan, so it wasn't attached."],
    [503, {title: "File storage isn't set up, so files can't be attached or downloaded."}, "File storage isn't set up, so files can't be attached or downloaded."],
    [404, {title: "Stage '…' was not found."}, "Stage '…' was not found."],
    [400, {title: 'One or more validation errors occurred.', errors: {FileName: ['The file needs a name.']}}, 'The file needs a name.'],
    [400, {title: 'Choose one target.'}, 'Choose one target.'],
    [413, '', 'The file is too large to attach.'],
    [503, 'not json', "File storage isn't set up, so files can't be attached."],
    [500, '', "The file couldn't be attached. Please try again."],
  ])('rejects a %d with the message to show', async (status, body, message) => {
    const sent = stubXhr();
    const result = uploadAttachment({target: {patientId}, file: file('a.pdf')});
    sent[0].respond(status, body);

    const error = await result.catch((e: unknown) => e);
    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(status);
    expect(uploadErrorText(error)).toBe(message);
  });

  it('rejects a dropped connection (as when the server closes it on a body too large) with a message', async () => {
    const sent = stubXhr();
    const result = uploadAttachment({target: {patientId}, file: file('a.pdf')});
    sent[0].fail();

    expect(uploadErrorText(await result.catch((e: unknown) => e))).toMatch(/^The file couldn't be sent\./);
  });

  it('cancels when its signal aborts, and never starts if it already has', async () => {
    const sent = stubXhr();
    const controller = new AbortController();
    const result = uploadAttachment({target: {patientId}, file: file('a.pdf'), signal: controller.signal});
    controller.abort();

    expect(isUploadCancelled(await result.catch((e: unknown) => e))).toBe(true);
    expect(sent[0].isAborted).toBe(true);

    const again = uploadAttachment({target: {patientId}, file: file('a.pdf'), signal: controller.signal});
    expect(isUploadCancelled(await again.catch((e: unknown) => e))).toBe(true);
    expect(sent).toHaveLength(1);
  });

  it('has a message for anything else', () => {
    expect(uploadErrorText(new Error('boom'))).toBe("The file couldn't be attached. Please try again.");
    expect(isUploadCancelled(new Error('boom'))).toBe(false);
  });
});

describe('useDeleteAttachment', () => {
  it('deletes the file, then refreshes every list of files and the timelines', async () => {
    const fetch = mockApi({[`DELETE /api/attachments/${attachmentId}`]: () => new Response(null, {status: 204})});
    const queryClient = new QueryClient();
    queryClient.setQueryData(['attachments', {patientId}], {items: []});
    queryClient.setQueryData(['procedures', 'timeline', patientId, 'overview'], {});
    queryClient.setQueryData(['patients', patientId], {});
    const wrapper = ({children}: {children: ReactNode}) => <AppProviders queryClient={queryClient}>{children}</AppProviders>;
    const {result} = renderHook(() => useDeleteAttachment(), {wrapper});

    await result.current.mutateAsync(attachmentId);

    expect(calls(fetch)).toEqual([`DELETE /api/attachments/${attachmentId}`]);
    await waitFor(() => expect(queryClient.getQueryState(['attachments', {patientId}])?.isInvalidated).toBe(true));
    expect(queryClient.getQueryState(['procedures', 'timeline', patientId, 'overview'])?.isInvalidated).toBe(true);
    expect(queryClient.getQueryState(['patients', patientId])?.isInvalidated).toBe(false);
  });
});
