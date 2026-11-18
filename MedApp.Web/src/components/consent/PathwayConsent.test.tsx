import {screen, waitFor, within} from '@testing-library/react';
import {json, mockApi, nurseUser, openDialog, renderWithApp, staffUser} from '../../test/render';
import {CONSENT_FORMS, CONSENT_PATHWAY_ID, CONSENT_RECORD_ID, consentPathway, SCAN_ID, testConsent} from '../../test/consent';
import {PathwayConsent} from './PathwayConsent';

function renderSection(records = [testConsent()], user = nurseUser, canRecordConsent = true) {
  const fetch = mockApi({
    [`GET /api/pathways/${CONSENT_PATHWAY_ID}/consent-records`]: () => json(records),
    'GET /api/consent-forms': () => json(CONSENT_FORMS.filter((f) => !f.isArchived)),
    [`POST /api/pathways/${CONSENT_PATHWAY_ID}/consent-records`]: () => json(testConsent(), 201),
    [`POST /api/consent-records/${CONSENT_RECORD_ID}/withdraw`]: () => json(testConsent({status: 'Withdrawn'})),
    [`POST /api/consent-records/${CONSENT_RECORD_ID}/entered-in-error`]: () => json(testConsent({status: 'EnteredInError'})),
  });
  return {...renderWithApp(<PathwayConsent pathway={consentPathway(canRecordConsent)} />, {user}), fetch};
}

describe('PathwayConsent', () => {
  it('lists the records with the form, who signed, the status and No scan', async () => {
    renderSection();
    const list = within(await screen.findByRole('list', {name: 'Consent records'}));
    expect(list.getByText('Biopsy: Consent for a skin procedure (synthetic) (version 3)')).toBeInTheDocument();
    expect(list.getByText(/The patient · recorded by test-nurse/)).toBeInTheDocument();
    expect(list.getByText('Valid')).toBeInTheDocument();
    expect(list.getByText('No scan')).toBeInTheDocument();
    expect(screen.getByText('Needs consent recorded: Biopsy.')).toBeInTheDocument();
  });

  it('flags a withdrawal after the stage was finished, and links a scan', async () => {
    renderSection([
      testConsent({status: 'Withdrawn', withdrawnOn: '2026-11-03', statusDetail: 'Synthetic: changed their mind.', withdrawnAfterStageFinished: true, scanAttachmentId: SCAN_ID, canWithdraw: false}),
    ]);
    expect(await screen.findByText('Withdrawn after the stage was finished')).toBeInTheDocument();
    expect(screen.getByText(/Synthetic: changed their mind\./)).toBeInTheDocument();
    expect(screen.getByRole('link', {name: 'Download the scanned form'})).toHaveAttribute('href', `/api/attachments/${SCAN_ID}/content`);
    expect(screen.queryByRole('button', {name: 'Withdraw'})).not.toBeInTheDocument();
  });

  it('records consent from the dialog', async () => {
    const {user, fetch} = renderSection([]);
    expect(await screen.findByText('No consent recorded.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Record Consent'}));
    const dialog = within(await waitFor(() => openDialog()!));
    await user.click(dialog.getByRole('combobox', {name: /Consent form/}));
    await user.click(await screen.findByRole('option', {name: 'Consent for a skin procedure (synthetic) (version 3)'}));
    await user.click(dialog.getByRole('radio', {name: 'The patient'}));
    await user.click(dialog.getByRole('button', {name: 'Record Consent'}));

    await waitFor(() =>
      expect(fetch.mock.calls.some(([r]) => (r as Request).method === 'POST' && (r as Request).url.endsWith('/consent-records'))).toBe(true),
    );
  });

  it('withdraws consent with a detail', async () => {
    const {user, fetch} = renderSection();
    await user.click(await screen.findByRole('button', {name: 'Withdraw'}));
    const dialog = within(await waitFor(() => openDialog()!));
    await user.type(dialog.getByRole('textbox', {name: /What was said/}), 'Synthetic withdrawal.');
    await user.click(dialog.getByRole('button', {name: 'Withdraw Consent'}));

    await waitFor(() => expect(fetch.mock.calls.some(([r]) => (r as Request).url.endsWith('/withdraw'))).toBe(true));
  });

  it('offers Record Consent only to holders of RecordConsent', async () => {
    renderSection([], staffUser);
    expect(await screen.findByText('No consent recorded.')).toBeInTheDocument();
    expect(screen.queryByRole('button', {name: 'Record Consent'})).not.toBeInTheDocument();
  });

  it("says when the records couldn't be loaded", async () => {
    mockApi({[`GET /api/pathways/${CONSENT_PATHWAY_ID}/consent-records`]: () => json({}, 500)});
    renderWithApp(<PathwayConsent pathway={consentPathway()} />, {user: nurseUser});
    expect(await screen.findByText('The consent records could not be loaded. Please try again.')).toBeInTheDocument();
  });
});
