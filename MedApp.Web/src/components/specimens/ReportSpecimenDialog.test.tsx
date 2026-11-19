import {screen, waitFor, within} from '@testing-library/react';
import {expectAnnounced, isDisabled, openDialog, renderWithApp, staffUser, withAnnouncements} from '../../test/render';
import {SPECIMEN_ID, testSpecimen} from '../../test/specimens';
import {stubXhr} from '../../test/xhr';
import {ReportSpecimenDialog} from './ReportSpecimenDialog';

const received = testSpecimen({status: 'Received', receivedAtUtc: '2026-11-03T10:00:00Z', labReference: 'NP-77', canReport: true, version: 5});
const file = (name: string, size = 10) => new File([new Uint8Array(size)], name, {type: 'application/octet-stream'});
const fileInput = () => document.querySelector<HTMLInputElement>('dialog[open] input[type="file"]')!;
const dialog = () => within(openDialog()!);

function renderDialog() {
  const onClose = vi.fn();
  const result = renderWithApp(<ReportSpecimenDialog isOpen specimen={received} onClose={onClose} />, {user: staffUser});
  return {...result, onClose};
}

describe('ReportSpecimenDialog', () => {
  it('attaches the report from the version shown, showing its progress, with the patient and sample shown', async () => {
    withAnnouncements();
    const sent = stubXhr();
    const {user, onClose} = renderDialog();

    expect(await screen.findByRole('heading', {name: 'Attach Report'})).toBeInTheDocument();
    expect(dialog().getByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
    expect(dialog().getByText('MA26-000123')).toBeInTheDocument();
    expect(dialog().getByText('NP-77')).toBeInTheDocument();
    expect(isDisabled(dialog().getByRole('button', {name: 'Attach Report'}))).toBe(true);

    await user.upload(fileInput(), file('report.pdf'));
    await user.click(dialog().getByRole('button', {name: 'Attach Report'}));

    await waitFor(() => expect(sent).toHaveLength(1));
    expect(sent[0].url).toBe(`/api/specimens/${SPECIMEN_ID}/report?expectedVersion=5`);
    expect(sent[0].headers['X-File-Name']).toBe('report.pdf');
    sent[0].progress(5, 10);
    expect(await dialog().findByRole('progressbar', {name: 'Sending report.pdf'})).toBeInTheDocument();
    sent[0].respond(200, testSpecimen({status: 'Reported'}));

    await expectAnnounced('Report attached');
    expect(onClose).toHaveBeenCalled();
  });

  it("checks the file's type before sending, and shows the server's refusal", async () => {
    const sent = stubXhr();
    const {user} = renderDialog();

    await screen.findByRole('heading', {name: 'Attach Report'});
    await user.upload(fileInput(), file('report.svg'));
    expect(await dialog().findByText(/SVG files can't be attached/)).toBeInTheDocument();
    expect(isDisabled(dialog().getByRole('button', {name: 'Attach Report'}))).toBe(true);

    await user.upload(fileInput(), file('report.pdf'));
    await user.click(dialog().getByRole('button', {name: 'Attach Report'}));
    await waitFor(() => expect(sent).toHaveLength(1));
    sent[0].respond(409, {title: 'Someone else changed this sample while you had it open.'});
    expect(await dialog().findByText('Someone else changed this sample while you had it open.')).toBeInTheDocument();
  });

  it('stops sending when asked, staying open', async () => {
    const sent = stubXhr();
    const {user, onClose} = renderDialog();

    await screen.findByRole('heading', {name: 'Attach Report'});
    await user.upload(fileInput(), file('report.pdf'));
    await user.click(dialog().getByRole('button', {name: 'Attach Report'}));
    await waitFor(() => expect(sent).toHaveLength(1));
    await user.click(await dialog().findByRole('button', {name: 'Stop Sending'}));

    expect(sent[0].isAborted).toBe(true);
    await waitFor(() => expect(dialog().queryByRole('progressbar')).not.toBeInTheDocument());
    expect(onClose).not.toHaveBeenCalled();
    expect(isDisabled(dialog().getByRole('button', {name: 'Cancel'}))).toBe(false);
  });
});
