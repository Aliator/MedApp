import {screen, waitFor} from '@testing-library/react';
import {stubXhr} from '../../test/xhr';
import {isDisabled, json, mockApi, nurseUser, renderWithApp} from '../../test/render';
import {CONSENT_PATHWAY_ID, CONSENT_PATIENT, CONSENT_RECORD_ID, SCAN_ID, testConsent} from '../../test/consent';
import {ConsentActionDialog} from './ConsentActionDialog';

describe('ConsentActionDialog', () => {
  it('marks a record entered in error, saying its scan goes with it', async () => {
    const fetch = mockApi({[`POST /api/consent-records/${CONSENT_RECORD_ID}/entered-in-error`]: () => json(testConsent({status: 'EnteredInError'}))});
    const onClose = vi.fn();
    const {user} = renderWithApp(<ConsentActionDialog action="enteredInError" record={testConsent()} patient={CONSENT_PATIENT} onClose={onClose} />, {
      user: nurseUser,
    });

    expect(await screen.findByText(/its scan is deleted/)).toBeInTheDocument();
    expect(isDisabled(screen.getByRole('button', {name: 'Mark Entered in Error'}))).toBe(true);
    await user.type(screen.getByRole('textbox', {name: /What was wrong/}), 'Synthetic: wrong patient.');
    await user.click(screen.getByRole('button', {name: 'Mark Entered in Error'}));

    await vi.waitFor(() => expect(onClose).toHaveBeenCalled());
    const body = await (fetch.mock.calls.find(([r]) => (r as Request).url.endsWith('/entered-in-error'))![0] as Request).json();
    expect(body).toEqual({detail: 'Synthetic: wrong patient.', expectedVersion: 1});
  });

  it("shows the API's refusal in its words", async () => {
    mockApi({[`POST /api/consent-records/${CONSENT_RECORD_ID}/withdraw`]: () => json({title: 'Someone else changed this consent record.'}, 409)});
    const {user} = renderWithApp(<ConsentActionDialog action="withdraw" record={testConsent()} onClose={vi.fn()} />, {user: nurseUser});

    await user.type(await screen.findByRole('textbox', {name: /What was said/}), 'Synthetic.');
    await user.click(screen.getByRole('button', {name: 'Withdraw Consent'}));

    expect(await screen.findByText('Someone else changed this consent record.')).toBeInTheDocument();
  });

  it('asks for a file to add a scan', async () => {
    renderWithApp(<ConsentActionDialog action="attachScan" record={testConsent()} onClose={vi.fn()} />, {user: nurseUser});
    expect(isDisabled(await screen.findByRole('button', {name: 'Add Scan'}))).toBe(true);
  });
});

describe('ConsentActionDialog, more', () => {
  const file = (name: string) => new File([new Uint8Array(10)], name, {type: 'application/octet-stream'});

  it('withdraws from the date and what was said, closing once done', async () => {
    const fetch = mockApi({[`POST /api/consent-records/${CONSENT_RECORD_ID}/withdraw`]: () => json(testConsent({status: 'Withdrawn'}))});
    const onClose = vi.fn();
    const {user} = renderWithApp(<ConsentActionDialog action="withdraw" record={testConsent({version: 4})} onClose={onClose} />, {user: nurseUser});

    await user.type(await screen.findByRole('textbox', {name: /What was said/}), ' Synthetic: changed their mind. ');
    await user.click(screen.getByRole('button', {name: 'Withdraw Consent'}));

    await waitFor(() => expect(onClose).toHaveBeenCalled());
    const body = await (fetch.mock.calls[0][0] as Request).clone().json();
    expect(body).toEqual(expect.objectContaining({detail: 'Synthetic: changed their mind.', expectedVersion: 4}));
  });

  it('shows a 400 on its field', async () => {
    mockApi({
      [`POST /api/consent-records/${CONSENT_RECORD_ID}/entered-in-error`]: () =>
        json({title: 'One or more validation errors occurred.', status: 400, errors: {Detail: ['Synthetic detail problem.']}}, 400),
    });
    const {user} = renderWithApp(<ConsentActionDialog action="enteredInError" record={testConsent({version: undefined})} onClose={vi.fn()} />, {user: nurseUser});

    await user.type(await screen.findByRole('textbox', {name: /What was wrong/}), 'Synthetic.');
    await user.click(screen.getByRole('button', {name: 'Mark Entered in Error'}));
    expect((await screen.findAllByText('Synthetic detail problem.')).length).toBeGreaterThan(0);
  });

  it('refuses a file of a type that cannot be attached, and adds a scan to the pathway, then to the record', async () => {
    const xhrs = stubXhr();
    const fetch = mockApi({[`POST /api/consent-records/${CONSENT_RECORD_ID}/scan`]: () => json(testConsent({scanAttachmentId: SCAN_ID}))});
    const onClose = vi.fn();
    const {user} = renderWithApp(<ConsentActionDialog action="attachScan" record={testConsent()} onClose={onClose} />, {user: nurseUser});

    const input = await waitFor(() => {
      const found = document.querySelector<HTMLInputElement>('dialog[open] input[type="file"]');
      expect(found).not.toBeNull();
      return found!;
    });
    await user.upload(input, file('form.exe'));
    expect((await screen.findAllByText(/EXE files can't be attached/)).length).toBeGreaterThan(0);
    expect(isDisabled(screen.getByRole('button', {name: 'Add Scan'}))).toBe(true);

    await user.upload(input, file('signed-form.pdf'));
    await user.click(screen.getByRole('button', {name: 'Add Scan'}));
    await waitFor(() => expect(xhrs).toHaveLength(1));
    expect(xhrs[0].url).toContain(`pathwayId=${CONSENT_PATHWAY_ID}`);
    xhrs[0].respond(201, {id: SCAN_ID, fileName: 'signed-form.pdf'});

    await waitFor(() => expect(onClose).toHaveBeenCalled());
    const body = await (fetch.mock.calls[0][0] as Request).clone().json();
    expect(body).toEqual({attachmentId: SCAN_ID, expectedVersion: 1});
  });

  it('shows the refusal when the record changed meanwhile', async () => {
    mockApi({[`POST /api/consent-records/${CONSENT_RECORD_ID}/entered-in-error`]: () => json({title: 'Synthetic: changed meanwhile.', status: 409}, 409)});
    const {user} = renderWithApp(<ConsentActionDialog action="enteredInError" record={testConsent()} onClose={vi.fn()} />, {user: nurseUser});

    await user.type(await screen.findByRole('textbox', {name: /What was wrong/}), 'Synthetic.');
    await user.click(screen.getByRole('button', {name: 'Mark Entered in Error'}));
    expect(await screen.findByText('Synthetic: changed meanwhile.')).toBeInTheDocument();

  });
  it('shows nothing while shut', async () => {
    renderWithApp(<ConsentActionDialog action={null} record={testConsent()} onClose={vi.fn()} />, {user: nurseUser});
    await waitFor(() => expect(document.querySelector('dialog[open]')).toBeNull());
    expect(screen.queryByRole('textbox')).not.toBeInTheDocument();
  });
});
