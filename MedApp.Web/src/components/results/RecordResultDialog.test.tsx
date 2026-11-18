import {screen, waitFor, within} from '@testing-library/react';
import type {Attachment} from '../../api/attachments';
import {json, mockApi, openDialog, renderWithApp, staffUser} from '../../test/render';
import {RESULT_PATHWAY_ID, RESULT_PATIENT, SENIOR_USER_ID, SIGN_OFF_HOLDERS} from '../../test/results';
import {RecordResultDialog} from './RecordResultDialog';

const report = (fields: Partial<Attachment> = {}) =>
  ({id: '00000061-0000-0000-0001-000000000001', fileName: 'ct-report.pdf', pathwayId: RESULT_PATHWAY_ID, pathwayTitle: 'Knee assessment', ...fields}) as Attachment;

async function shownDialog() {
  await waitFor(() => expect(openDialog()).not.toBeNull());
  return within(openDialog()!);
}

async function fillIn(user: ReturnType<typeof renderWithApp>['user'], dialog: Awaited<ReturnType<typeof shownDialog>>) {
  await user.type(dialog.getByRole('textbox', {name: /^Title/}), 'Synthetic CT report');
  await user.click(dialog.getByRole('combobox', {name: /^Responsible clinician/}));
  await user.click(await screen.findByRole('option', {name: 'test-senior'}));
}

describe('RecordResultDialog', () => {
  it('shows the patient and the file, and waits for a title and who signs it off', async () => {
    mockApi({'GET /api/calendar/staff': () => json(SIGN_OFF_HOLDERS)});
    const {user} = renderWithApp(<RecordResultDialog attachment={report()} patient={RESULT_PATIENT} onClose={vi.fn()} />, {user: staffUser});

    const dialog = await shownDialog();
    expect(dialog.getByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
    expect(dialog.getByText('ct-report.pdf')).toBeInTheDocument();
    expect(dialog.getByText('Knee assessment')).toBeInTheDocument();
    const record = dialog.getByRole('button', {name: 'Record as Result'});
    expect(record).toHaveAttribute('aria-disabled', 'true');

    await fillIn(user, dialog);
    expect(record).not.toHaveAttribute('aria-disabled', 'true');
  });

  it('shows a 400 on its fields, and any other refusal in the API words', async () => {
    let status = 400;
    const fetch = mockApi({
      'GET /api/calendar/staff': () => json(SIGN_OFF_HOLDERS),
      'POST /api/results/external': () =>
        status === 400
          ? json({title: 'One or more validation errors occurred.', status: 400, errors: {Title: ['Synthetic title problem.']}}, 400)
          : json({title: 'Synthetic refusal.', status: 409}, 409),
    });
    const onClose = vi.fn();
    const {user} = renderWithApp(<RecordResultDialog attachment={report({pathwayId: null})} onClose={onClose} />, {user: staffUser});

    const dialog = await shownDialog();
    expect(dialog.getByText('Not on a pathway')).toBeInTheDocument();
    await fillIn(user, dialog);
    await user.click(dialog.getByRole('button', {name: 'Record as Result'}));
    expect((await dialog.findAllByText('Synthetic title problem.')).length).toBeGreaterThan(0);
    const sent = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!;
    expect(await sent.clone().json()).toEqual({
      attachmentId: report().id,
      title: 'Synthetic CT report',
      pathwayId: null,
      responsibleUserId: SENIOR_USER_ID,
    });

    status = 409;
    await user.click(dialog.getByRole('button', {name: 'Record as Result'}));
    expect(await dialog.findByText('Synthetic refusal.')).toBeInTheDocument();
    expect(onClose).not.toHaveBeenCalled();
  });

  it('says when the staff could not be loaded', async () => {
    mockApi({'GET /api/calendar/staff': () => json({title: 'Synthetic failure.', status: 500}, 500)});
    renderWithApp(<RecordResultDialog attachment={report()} onClose={vi.fn()} />, {user: staffUser});

    const dialog = await shownDialog();
    expect(await dialog.findByText('The staff could not be loaded.')).toBeInTheDocument();
  });
});
