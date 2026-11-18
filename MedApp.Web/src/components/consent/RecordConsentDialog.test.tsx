import {screen, waitFor, within} from '@testing-library/react';
import {json, mockApi, nurseUser, openDialog, renderWithApp} from '../../test/render';
import {CONSENT_FORM_ID, CONSENT_FORMS, CONSENT_PATHWAY_ID, CONSENT_STAGE_ID, consentPathway, SCAN_ID, testConsent} from '../../test/consent';
import {stubXhr} from '../../test/xhr';
import {RecordConsentDialog} from './RecordConsentDialog';

const OTHER_STAGE_ID = '00000061-0000-0000-0000-000000000008';
const RECORD = `POST /api/pathways/${CONSENT_PATHWAY_ID}/consent-records`;
const pathway = consentPathway();
const stages = [
  pathway.stages![0],
  {id: OTHER_STAGE_ID, name: 'Excision', stageOrder: 2, isCurrent: false, needsConsent: true, canRecordConsent: true},
];
const file = (name: string, size = 10) => new File([new Uint8Array(size)], name, {type: 'application/octet-stream'});

async function shownDialog() {
  await waitFor(() => expect(openDialog()).not.toBeNull());
  return within(openDialog()!);
}

async function fillIn(user: ReturnType<typeof renderWithApp>['user'], dialog: Awaited<ReturnType<typeof shownDialog>>) {
  await user.click(dialog.getByRole('combobox', {name: /^Stage/}));
  await user.click(await screen.findByRole('option', {name: 'Excision'}));
  await user.click(dialog.getByRole('combobox', {name: /Consent form/}));
  await user.click(await screen.findByRole('option', {name: 'Consent for a skin procedure (synthetic) (version 3)'}));
  await user.click(dialog.getByRole('radio', {name: 'A person with parental responsibility'}));
}

describe('RecordConsentDialog', () => {
  it('asks which stage when there are several, and shows a 400 on its fields and a refusal in the API words', async () => {
    let answer = () => json({title: 'One or more validation errors occurred.', status: 400, errors: {SignedOn: ['Synthetic date problem.']}}, 400);
    const fetch = mockApi({
      'GET /api/consent-forms': () => json(CONSENT_FORMS.filter((f) => !f.isArchived)),
      [RECORD]: () => answer(),
    });
    const onClose = vi.fn();
    const {user} = renderWithApp(<RecordConsentDialog isOpen pathway={pathway} stages={stages} onClose={onClose} />, {user: nurseUser});

    const dialog = await shownDialog();
    expect(dialog.getByRole('region', {name: 'Patient: Example, Ivy'})).toBeInTheDocument();
    expect(dialog.getByText(/Without a scan/)).toBeInTheDocument();
    const record = dialog.getByRole('button', {name: 'Record Consent'});
    expect(record).toHaveAttribute('aria-disabled', 'true');
    await fillIn(user, dialog);
    await user.click(record);
    expect((await dialog.findAllByText('Synthetic date problem.')).length).toBeGreaterThan(0);
    const sent = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!;
    expect(await sent.clone().json()).toEqual(
      expect.objectContaining({stageId: OTHER_STAGE_ID, consentFormId: CONSENT_FORM_ID, signedBy: 'PersonWithParentalResponsibility', scanAttachmentId: null}),
    );

    answer = () => json({title: 'Synthetic: the stage is finished.', status: 409}, 409);
    await user.click(record);
    expect(await dialog.findByText(/Synthetic: the stage is finished\./)).toBeInTheDocument();
    expect(onClose).not.toHaveBeenCalled();
  });

  it('refuses a scan of a type that cannot be attached', async () => {
    mockApi({'GET /api/consent-forms': () => json(CONSENT_FORMS)});
    const {user} = renderWithApp(<RecordConsentDialog isOpen pathway={pathway} stages={stages.slice(0, 1)} onClose={vi.fn()} />, {user: nurseUser});

    const dialog = await shownDialog();
    await user.upload(openDialog()!.querySelector<HTMLInputElement>('input[type="file"]')!, file('form.exe'));
    expect((await dialog.findAllByText(/EXE files can't be attached/)).length).toBeGreaterThan(0);
    expect(dialog.queryByText(/Without a scan/)).not.toBeInTheDocument();
  });

  it('attaches the scan to the pathway first, then records consent naming it', async () => {
    const xhrs = stubXhr();
    const fetch = mockApi({
      'GET /api/consent-forms': () => json(CONSENT_FORMS),
      [RECORD]: () => json(testConsent({scanAttachmentId: SCAN_ID}), 201),
    });
    const onClose = vi.fn();
    const {user} = renderWithApp(<RecordConsentDialog isOpen pathway={pathway} stages={stages.slice(0, 1)} onClose={onClose} />, {user: nurseUser});

    const dialog = await shownDialog();
    await user.click(dialog.getByRole('combobox', {name: /Consent form/}));
    await user.click(await screen.findByRole('option', {name: 'Consent for a skin procedure (synthetic) (version 3)'}));
    await user.click(dialog.getByRole('radio', {name: 'The patient'}));
    await user.upload(openDialog()!.querySelector<HTMLInputElement>('input[type="file"]')!, file('signed-form.pdf'));
    await user.click(dialog.getByRole('button', {name: 'Record Consent'}));

    await waitFor(() => expect(xhrs).toHaveLength(1));
    expect(xhrs[0].url).toContain(`pathwayId=${CONSENT_PATHWAY_ID}`);
    xhrs[0].respond(201, {id: SCAN_ID, fileName: 'signed-form.pdf'});
    await waitFor(() => expect(onClose).toHaveBeenCalled());
    const sent = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!;
    expect(await sent.clone().json()).toEqual(expect.objectContaining({stageId: CONSENT_STAGE_ID, scanAttachmentId: SCAN_ID, signedBy: 'Patient'}));
  });

  it('says when the consent forms could not be loaded', async () => {
    mockApi({'GET /api/consent-forms': () => json({title: 'Synthetic failure.', status: 500}, 500)});
    renderWithApp(<RecordConsentDialog isOpen pathway={{...pathway, patient: undefined}} stages={stages} onClose={vi.fn()} />, {user: nurseUser});

    const dialog = await shownDialog();
    expect(await dialog.findByText('The consent forms could not be loaded.')).toBeInTheDocument();
    expect(dialog.queryByRole('region', {name: /Patient:/})).not.toBeInTheDocument();
  });
});
