import {fireEvent, screen, waitFor, within} from '@testing-library/react';
import {useState} from 'react';
import type {Attachment, AttachmentList, AttachmentTarget} from '../../api/attachments';
import {
  adminUser,
  calls,
  expectAnnounced,
  findAlert,
  json,
  mockApi,
  openDialog,
  renderWithApp,
  staffUser,
  withAnnouncements,
} from '../../test/render';
import {stubXhr} from '../../test/xhr';
import {HalfViewContext} from '../pages/halfView';
import {Attachments, NOT_SCANNED_NOTE} from './Attachments';
import {AttachmentsFold} from './AttachmentsFold';
import {useAttachmentsState} from './useAttachmentsState';

const patientId = '00000036-0000-0000-0000-000000000001';
const procedureId = '00000036-0000-0000-0000-000000000002';
const stageId = '00000036-0000-0000-0000-000000000003';
const MB = 1024 * 1024;
const LIST = '/api/attachments';
/** The API's refusal while the patient's record is restricted (`PatientRules.RestrictedMessage`). */
const RESTRICTED =
  "This patient's record is restricted, so it can't be changed until an admin lifts the restriction. Its procedures can still be blocked or cancelled, with a reason.";

let next = 0;
const attachment = (fields: Partial<Attachment> = {}): Attachment => ({
  id: `00000036-0000-0000-0001-${String(++next).padStart(12, '0')}`,
  patientId,
  attachedTo: 'Patient',
  procedureId: null,
  procedureTitle: null,
  stageId: null,
  stageName: null,
  fileName: 'scan.pdf',
  contentType: 'application/pdf',
  typeName: 'PDF',
  sizeBytes: 1258291,
  sha256: 'abc',
  scanStatus: 'NotScanned',
  hasThumbnail: false,
  uploadedByUsername: 'test.staff',
  uploadedAtUtc: '2026-11-03T14:05:00Z',
  canDelete: true,
  isDeleted: false,
  deletedAtUtc: null,
  deletedByUsername: null,
  ...fields,
});

const listOf = (items: Attachment[], fields: Partial<AttachmentList> = {}): AttachmentList => ({
  items,
  totalCount: items.length,
  maxFileBytes: 20 * MB,
  allowedExtensions: ['.pdf', '.png', '.jpg', '.jpeg', '.heic', '.heif', '.docx', '.txt'],
  ...fields,
});

const file = (name: string, size = 10) => new File([new Uint8Array(size)], name, {type: 'application/octet-stream'});

/** The add area's hidden file input (its button opens it). */
const fileInput = () => document.querySelector<HTMLInputElement>('input[type="file"]')!;

/** Answers the list with `items` (or a function of the request), and records the requests. */
function mockList(items: Attachment[] | ((request: Request) => AttachmentList), extra: Record<string, (request: Request) => Response> = {}) {
  return mockApi({
    [`GET ${LIST}`]: (request) => json(typeof items === 'function' ? items(request) : listOf(items)),
    ...extra,
  });
}

function render(target: AttachmentTarget = {patientId}, {canChange = true, user = staffUser, isNarrow = false, isLocked = false} = {}) {
  const ui = <Attachments target={target} canChange={canChange} isLocked={isLocked} />;
  return renderWithApp(isNarrow ? <HalfViewContext.Provider value={{isHalf: true}}>{ui}</HalfViewContext.Provider> : ui, {user});
}

describe('Attachments', () => {
  it("lists a patient's files: preview, name, type, size, what it's attached to, who and when, and the scan", async () => {
    const files = [
      attachment({fileName: 'photo.jpg', contentType: 'image/jpeg', typeName: 'JPEG image', hasThumbnail: true, scanStatus: 'Clean'}),
      attachment({
        fileName: 'referral.docx',
        typeName: 'Word document',
        contentType: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
        attachedTo: 'Procedure',
        procedureId,
        procedureTitle: 'Knee assessment',
        sizeBytes: 2048,
      }),
      attachment({fileName: 'notes.txt', attachedTo: 'Stage', procedureId, procedureTitle: 'Knee assessment', stageId, stageName: 'Consultation'}),
    ];
    const fetch = mockList(files);
    render();

    expect(await screen.findByText('photo.jpg')).toBeInTheDocument();
    expect(calls(fetch)).toEqual([`GET ${LIST}?PatientId=${patientId}`]);
    expect(screen.getByText('JPEG image · 1.2 MB')).toBeInTheDocument();
    expect(screen.getByText('Word document · 2 KB')).toBeInTheDocument();
    expect(screen.getAllByText('Added by test.staff, 3 Nov 2026, 14:05')).toHaveLength(3);
    expect(screen.getByText('Attached to the patient')).toBeInTheDocument();
    expect(screen.getAllByRole('link', {name: 'Knee assessment'}).map((link) => link.getAttribute('href'))).toEqual([
      `/procedures/${procedureId}`,
      `/procedures/${procedureId}`,
    ]);
    expect(screen.getByText(/Attached to Consultation in/)).toBeInTheDocument();
    expect(screen.getByText('Scanned')).toBeInTheDocument();
    expect(screen.getAllByText('Not scanned')).toHaveLength(2);
    expect(screen.getByText(NOT_SCANNED_NOTE)).toBeInTheDocument();
    // Only the picture has a thumbnail, and it's decorative: the name says what it is.
    const images = document.querySelectorAll('img');
    expect(images).toHaveLength(1);
    expect(images[0]).toHaveAttribute('src', `/api/attachments/${files[0].id}/thumbnail`);
    expect(images[0]).toHaveAttribute('alt', '');
    // Downloads download; nothing opens a file in the page.
    const download = screen.getByRole('link', {name: 'Download photo.jpg'});
    expect(download).toHaveAttribute('href', `/api/attachments/${files[0].id}/content`);
    expect(download).toHaveAttribute('download');
  });

  it("doesn't say what a procedure's or a stage's own files are attached to", async () => {
    mockList([attachment({attachedTo: 'Procedure', procedureId, procedureTitle: 'Knee assessment'})]);
    render({procedureId});

    expect(await screen.findByText('scan.pdf')).toBeInTheDocument();
    expect(screen.queryByText(/Attached to/)).not.toBeInTheDocument();
    expect(screen.queryByText(NOT_SCANNED_NOTE)).toBeInTheDocument();
  });

  it('says when there are more than it shows, and when a scan found malware', async () => {
    mockList(() => listOf([attachment({scanStatus: 'Infected'})], {totalCount: 250}));
    render();

    expect(await screen.findByText('Showing the newest 1 of 250 files.')).toBeInTheDocument();
    expect(screen.getByText('Malware found')).toBeInTheDocument();
    expect(screen.queryByText(NOT_SCANNED_NOTE)).not.toBeInTheDocument();
  });

  it('says when nothing is attached yet, or the files could not be loaded', async () => {
    mockList([]);
    const {unmount} = render();
    expect(await screen.findByText('No files attached yet.')).toBeInTheDocument();
    unmount();

    mockApi({[`GET ${LIST}`]: () => json({title: 'Down'}, 500)});
    render();
    expect(await findAlert('The files could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('copes with a file missing its optional details', async () => {
    mockList([
      attachment({
        id: undefined,
        fileName: undefined,
        typeName: null,
        sizeBytes: undefined,
        uploadedByUsername: null,
        uploadedAtUtc: undefined,
        attachedTo: 'Stage',
        procedureId,
        procedureTitle: 'Knee assessment',
        stageName: null,
        scanStatus: null,
        canDelete: undefined,
      }),
      attachment({fileName: 'gone.png', contentType: 'image/png', isDeleted: true, canDelete: false, deletedByUsername: null, deletedAtUtc: null}),
      attachment({id: undefined, fileName: 'photo.png', contentType: 'image/png', hasThumbnail: true, canDelete: false}),
    ]);
    render();

    expect(await screen.findByText('File · 0 bytes')).toBeInTheDocument();
    expect(screen.getByText(/Attached to a stage in/)).toBeInTheDocument();
    expect(screen.getByText('Added by unknown,')).toBeInTheDocument();
    expect(screen.getByText('Deleted by unknown,')).toBeInTheDocument();
    expect(document.querySelector('img')).toHaveAttribute('src', '/api/attachments//thumbnail');
    expect(screen.queryByRole('button', {name: /^Delete/})).not.toBeInTheDocument();
  });

  describe('deleting', () => {
    it("lets staff delete their own files where files can change, after confirming, and says so", async () => {
      withAnnouncements();
      const mine = attachment();
      let deleted = false;
      const fetch = mockList(() => listOf(deleted ? [] : [mine]), {
        [`DELETE ${LIST}/${mine.id}`]: () => {
          deleted = true;
          return new Response(null, {status: 204});
        },
      });
      const {user} = render();

      await user.click(await screen.findByRole('button', {name: 'Delete scan.pdf'}));
      const dialog = openDialog()!;
      expect(within(dialog).getByText('Delete scan.pdf?')).toBeInTheDocument();
      expect(
        within(dialog).getByText(
          "It's hidden at once and removed for good after the retention period. Admins can still see that it was attached.",
        ),
      ).toBeInTheDocument();
      await user.click(within(dialog).getByRole('button', {name: 'Delete File'}));

      await expectAnnounced('scan.pdf deleted');
      expect(await screen.findByText('No files attached yet.')).toBeInTheDocument();
      expect(calls(fetch)).toContain(`DELETE ${LIST}/${mine.id}`);
      expect(openDialog()).toBeNull();
    });

    it('asks again before deleting: Cancel keeps the file', async () => {
      const fetch = mockList([attachment()]);
      const {user} = render();

      await user.click(await screen.findByRole('button', {name: 'Delete scan.pdf'}));
      await user.click(within(openDialog()!).getByRole('button', {name: 'Cancel'}));

      expect(openDialog()).toBeNull();
      expect(calls(fetch).some((call) => call.startsWith('DELETE'))).toBe(false);
    });

    it("shows why a file couldn't be deleted, until dismissed", async () => {
      withAnnouncements();
      const mine = attachment();
      mockList([mine], {[`DELETE ${LIST}/${mine.id}`]: () => json({title: "Files can't be added to or removed from a procedure while it's cancelled."}, 409)});
      const {user} = render();

      await user.click(await screen.findByRole('button', {name: 'Delete scan.pdf'}));
      await user.click(within(openDialog()!).getByRole('button', {name: 'Delete File'}));

      const message = "scan.pdf could not be deleted. Files can't be added to or removed from a procedure while it's cancelled.";
      expect(await findAlert(message)).toBeInTheDocument();
      await expectAnnounced(message);
      await user.click(screen.getByRole('button', {name: 'Dismiss'}));
      expect(screen.queryByRole('button', {name: 'Dismiss'})).not.toBeInTheDocument();
      expect(screen.getAllByText(message).every((element) => element.closest('[data-testid="announcer"]'))).toBe(true);
    });

    it("hides Delete from staff for others' files, files elsewhere, and where files can't change", async () => {
      const files = [
        attachment({fileName: 'theirs.pdf', canDelete: false}),
        attachment({fileName: 'on-procedure.pdf', attachedTo: 'Procedure', procedureId, procedureTitle: 'Knee assessment'}),
      ];
      mockList(files);
      const {unmount} = render();

      expect(await screen.findByText('theirs.pdf')).toBeInTheDocument();
      expect(screen.queryByRole('button', {name: /^Delete/})).not.toBeInTheDocument();
      unmount();

      mockList([attachment({attachedTo: 'Stage', stageId})]);
      render({stageId}, {canChange: false});
      expect(await screen.findByText('scan.pdf')).toBeInTheDocument();
      expect(screen.queryByRole('button', {name: /^Delete/})).not.toBeInTheDocument();
      expect(screen.queryByRole('button', {name: 'Add files'})).not.toBeInTheDocument();
    });

    it('lets admins delete any file, anywhere, even where files no longer change', async () => {
      mockList([attachment({attachedTo: 'Stage', stageId, procedureId, procedureTitle: 'Knee assessment', stageName: 'Consultation'})]);
      render({patientId}, {canChange: false, user: adminUser});

      expect(await screen.findByRole('button', {name: 'Delete scan.pdf'})).toBeInTheDocument();
    });
  });

  it("offers nothing to add or delete, even to admins, while the patient's record is restricted or anonymised", async () => {
    mockList([attachment()]);
    render({patientId}, {user: adminUser, isLocked: true});

    expect(await screen.findByText('scan.pdf')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Add files'})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Delete scan.pdf'})).not.toBeInTheDocument();
    // Still listed, and downloadable.
    expect(screen.getByRole('link', {name: 'Download scan.pdf'})).toBeInTheDocument();
  });

  it("shows admins deleted files on request: who deleted them and when, with no download or thumbnail", async () => {
    const deleted = attachment({
      fileName: 'old.jpg',
      contentType: 'image/jpeg',
      hasThumbnail: true,
      isDeleted: true,
      canDelete: false,
      deletedByUsername: 'test.admin',
      deletedAtUtc: '2026-11-04T09:30:00Z',
    });
    const fetch = mockList((request) => listOf(new URL(request.url).searchParams.has('IncludeDeleted') ? [attachment(), deleted] : [attachment()]));
    const {user} = render({patientId}, {user: adminUser});

    expect(await screen.findByText('scan.pdf')).toBeInTheDocument();
    await user.click(screen.getByRole('switch', {name: 'Show deleted files'}));

    expect(await screen.findByText('old.jpg')).toBeInTheDocument();
    expect(calls(fetch)).toContain(`GET ${LIST}?PatientId=${patientId}&IncludeDeleted=true`);
    expect(screen.getByText('Deleted by test.admin, 4 Nov 2026, 09:30')).toBeInTheDocument();
    expect(screen.getByText('Deleted')).toBeInTheDocument();
    expect(screen.queryByRole('link', {name: 'Download old.jpg'})).not.toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Delete old.jpg'})).not.toBeInTheDocument();
    expect(document.querySelectorAll('img')).toHaveLength(0);
  });

  it("doesn't offer staff deleted files", async () => {
    mockList([attachment()]);
    render();
    expect(await screen.findByText('scan.pdf')).toBeInTheDocument();
    expect(screen.queryByRole('switch', {name: 'Show deleted files'})).not.toBeInTheDocument();
  });

  it('in a narrow window or half view, puts the buttons under the details', async () => {
    mockList([attachment()]);
    render({patientId}, {isNarrow: true});

    const download = await screen.findByRole('link', {name: 'Download scan.pdf'});
    // In the row's description, with the details, rather than at its end.
    expect(download.closest('li')).toHaveTextContent('PDF · 1.2 MB');
    expect(screen.getByText('PDF · 1.2 MB').parentElement).toContainElement(download);
  });

  describe('adding files', () => {
    it('has a button that opens the file picker, saying what can be attached', async () => {
      mockList([]);
      render();

      const add = await screen.findByRole('button', {name: 'Add files'});
      expect(add.tagName).toBe('BUTTON');
      expect(await screen.findByText('PDF, PNG, JPG, JPEG, HEIC, HEIF, DOCX or TXT, up to 20 MB each.')).toBeInTheDocument();
    });

    it('sends a picked file with its progress, then lists it and says so', async () => {
      withAnnouncements();
      const sent = stubXhr();
      const uploaded = attachment();
      let isUploaded = false;
      const fetch = mockList(() => listOf(isUploaded ? [uploaded] : []));
      const {user} = render();
      await screen.findByText('No files attached yet.');

      await user.upload(fileInput(), file('scan.pdf'));

      await waitFor(() => expect(sent).toHaveLength(1));
      expect(sent[0].url).toBe(`/api/attachments?patientId=${patientId}`);
      sent[0].progress(1, 4);
      const progress = await screen.findByRole('progressbar', {name: 'Attaching scan.pdf'});
      expect(progress).toHaveAttribute('aria-valuenow', '25');
      expect(screen.getByText('25%')).toBeInTheDocument();

      isUploaded = true;
      sent[0].respond(201, uploaded);

      await expectAnnounced('scan.pdf attached');
      expect(screen.getByText('scan.pdf')).toBeInTheDocument();
      expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
      expect(calls(fetch).filter((call) => call.startsWith('GET'))).toHaveLength(2);
    });

    it('takes files dropped on it', async () => {
      const sent = stubXhr();
      mockList([]);
      render({procedureId});
      await screen.findByText('No files attached yet.');

      const zone = fileInput().parentElement!;
      fireEvent.dragEnter(zone, {dataTransfer: {files: [], types: ['Files']}});
      fireEvent.drop(zone, {dataTransfer: {files: [file('scan.pdf')], types: ['Files']}});

      await waitFor(() => expect(sent).toHaveLength(1));
      expect(sent[0].url).toBe(`/api/attachments?procedureId=${procedureId}`);
    });

    it('sends several files one after another, each with its own progress, and cancels one still waiting', async () => {
      withAnnouncements();
      const sent = stubXhr();
      mockList([]);
      const {user} = render({stageId});
      await screen.findByText('No files attached yet.');

      await user.upload(fileInput(), [file('a.pdf'), file('b.png'), file('c.txt')]);

      await waitFor(() => expect(sent).toHaveLength(1));
      expect(screen.getByRole('progressbar', {name: 'Attaching a.pdf'})).toBeInTheDocument();
      expect(screen.getByText('b.png: waiting')).toBeInTheDocument();
      expect(screen.getByText('c.txt: waiting')).toBeInTheDocument();
      await expectAnnounced('Attaching 3 files');

      await user.click(screen.getByRole('button', {name: 'Cancel c.txt'}));
      expect(screen.queryByText('c.txt: waiting')).not.toBeInTheDocument();
      await expectAnnounced('c.txt not attached: cancelled');

      sent[0].respond(201, attachment({fileName: 'a.pdf'}));
      await waitFor(() => expect(sent).toHaveLength(2));
      expect(sent[1].headers['X-File-Name']).toBe('b.png');
      expect(await screen.findByRole('progressbar', {name: 'Attaching b.png'})).toBeInTheDocument();

      sent[1].respond(201, attachment({fileName: 'b.png'}));
      await waitFor(() => expect(screen.queryByRole('progressbar')).not.toBeInTheDocument());
      expect(sent).toHaveLength(2);
    });

    it('cancels the file being sent', async () => {
      withAnnouncements();
      const sent = stubXhr();
      mockList([]);
      const {user} = render();
      await screen.findByText('No files attached yet.');

      await user.upload(fileInput(), file('scan.pdf'));
      await waitFor(() => expect(sent).toHaveLength(1));
      await user.click(screen.getByRole('button', {name: 'Cancel scan.pdf'}));

      expect(sent[0].isAborted).toBe(true);
      await expectAnnounced('scan.pdf not attached: cancelled');
      expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
    });

    it("refuses a file of another type, or too large, in the browser, naming it, and sends the rest", async () => {
      withAnnouncements();
      const sent = stubXhr();
      // The list's own limit, not the default.
      mockList(() => listOf([], {maxFileBytes: MB}));
      const {user} = render();
      await screen.findByText('No files attached yet.');
      expect(screen.getByText('PDF, PNG, JPG, JPEG, HEIC, HEIF, DOCX or TXT, up to 1 MB each.')).toBeInTheDocument();

      await user.upload(fileInput(), [file('scan.tiff'), file('photo.jpg', 2 * MB), file('fine.pdf')]);

      const alert = await findAlert("2 files weren't attached");
      expect(alert).toHaveTextContent("scan.tiff: TIFF files can't be attached. Attach PDF, PNG, JPG, JPEG, HEIC, HEIF, DOCX or TXT.");
      expect(alert).toHaveTextContent('photo.jpg is 2 MB; the most is 1 MB.');
      await expectAnnounced(/scan\.tiff: TIFF files can't be attached.*photo\.jpg is 2 MB; the most is 1 MB\..*Attaching 1 file/);
      await waitFor(() => expect(sent).toHaveLength(1));
      expect(sent[0].headers['X-File-Name']).toBe('fine.pdf');

      await user.click(screen.getByRole('button', {name: 'Dismiss'}));
      expect(screen.queryByText("2 files weren't attached")).not.toBeInTheDocument();
    });

    it('refuses every file picked, sending none', async () => {
      withAnnouncements();
      const sent = stubXhr();
      mockList([]);
      const {user} = render();
      await screen.findByText('No files attached yet.');

      await user.upload(fileInput(), file('scan.svg'));

      expect(await findAlert("A file wasn't attached")).toHaveTextContent("scan.svg: SVG files can't be attached.");
      await expectAnnounced(/^scan\.svg: SVG files can't be attached\. Attach PDF, PNG, JPG, JPEG, HEIC, HEIF, DOCX or TXT\.$/);
      expect(sent).toHaveLength(0);
    });

    it('queues files added while another is being sent', async () => {
      const sent = stubXhr();
      mockList([]);
      const {user} = render();
      await screen.findByText('No files attached yet.');

      await user.upload(fileInput(), file('a.pdf'));
      await waitFor(() => expect(sent).toHaveLength(1));
      await user.upload(fileInput(), file('b.pdf'));

      expect(await screen.findByText('b.pdf: waiting')).toBeInTheDocument();
      expect(sent).toHaveLength(1);
      sent[0].respond(201, attachment({fileName: 'a.pdf'}));
      await waitFor(() => expect(sent).toHaveLength(2));
      expect(sent[1].headers['X-File-Name']).toBe('b.pdf');
    });

    it("shows the server's refusal when the record was restricted meanwhile", async () => {
      const sent = stubXhr();
      mockList([]);
      const {user} = render();
      await screen.findByText('No files attached yet.');

      await user.upload(fileInput(), file('scan.pdf'));
      await waitFor(() => expect(sent).toHaveLength(1));
      sent[0].respond(409, {title: RESTRICTED});

      expect(await findAlert("A file wasn't attached")).toHaveTextContent(`scan.pdf: ${RESTRICTED}`);
    });

    it("shows the server's reason a file wasn't attached", async () => {
      withAnnouncements();
      const sent = stubXhr();
      mockList([]);
      const {user} = render({stageId});
      await screen.findByText('No files attached yet.');

      await user.upload(fileInput(), file('scan.png'));
      await waitFor(() => expect(sent).toHaveLength(1));
      sent[0].respond(415, {title: "The file's name ends in .png, but it's a PDF. Name it with .pdf."});

      const alert = await findAlert("A file wasn't attached");
      expect(alert).toHaveTextContent("scan.png: The file's name ends in .png, but it's a PDF. Name it with .pdf.");
      await expectAnnounced("scan.png: The file's name ends in .png, but it's a PDF. Name it with .pdf.");
    });

    it('stops sending when the page is left', async () => {
      const sent = stubXhr();
      mockList([]);
      const {user, unmount} = render();
      await screen.findByText('No files attached yet.');

      await user.upload(fileInput(), [file('a.pdf'), file('b.pdf')]);
      await waitFor(() => expect(sent).toHaveLength(1));
      unmount();

      expect(sent[0].isAborted).toBe(true);
      expect(sent).toHaveLength(1);
    });

    it('ignores a pick of nothing', async () => {
      const sent = stubXhr();
      mockList([]);
      const {user} = render();
      await screen.findByText('No files attached yet.');

      fireEvent.change(fileInput(), {target: {files: []}});
      await user.click(screen.getByRole('button', {name: 'Add files'}));

      expect(sent).toHaveLength(0);
      expect(screen.queryByText(/wasn't attached/)).not.toBeInTheDocument();
    });
  });

  it('keeps uploads and Show deleted in state the page holds, so they carry on when it changes layout', async () => {
    const sent = stubXhr();
    mockList([]);
    function Page() {
      const state = useAttachmentsState();
      const [isWide, setIsWide] = useState(true);
      return (
        <>
          <button type="button" onClick={() => setIsWide((wide) => !wide)}>
            Switch layout
          </button>
          {isWide ? (
            <div key="wide">
              <Attachments target={{patientId}} canChange state={state} />
            </div>
          ) : (
            <section key="narrow">
              <Attachments target={{patientId}} canChange state={state} />
            </section>
          )}
        </>
      );
    }
    const {user} = renderWithApp(<Page />, {user: adminUser});
    await screen.findByText('No files attached yet.');
    await user.click(screen.getByRole('switch', {name: 'Show deleted files'}));
    await user.upload(fileInput(), file('scan.pdf'));
    await waitFor(() => expect(sent).toHaveLength(1));

    await user.click(screen.getByRole('button', {name: 'Switch layout'}));

    expect(screen.getByRole('progressbar', {name: 'Attaching scan.pdf'})).toBeInTheDocument();
    expect(screen.getByRole('switch', {name: 'Show deleted files'})).toBeChecked();
    expect(sent[0].isAborted).toBe(false);
  });
});

describe('AttachmentsFold', () => {
  function Fold() {
    const [isOpen, setIsOpen] = useState(false);
    return <AttachmentsFold target={{stageId}} canChange isOpen={isOpen} onOpenChange={setIsOpen} />;
  }

  it('fetches the files only once opened', async () => {
    const fetch = mockList([attachment({fileName: 'stage.pdf', attachedTo: 'Stage', stageId})]);
    const {user} = renderWithApp(<Fold />, {user: staffUser});

    const toggle = await screen.findByRole('button', {name: 'Files'});
    expect(calls(fetch)).toEqual([]);

    await user.click(toggle);

    expect(await screen.findByText('stage.pdf')).toBeInTheDocument();
    expect(calls(fetch)).toEqual([`GET ${LIST}?StageId=${stageId}`]);
  });
});
