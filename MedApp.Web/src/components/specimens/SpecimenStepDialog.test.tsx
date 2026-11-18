import {screen, within} from '@testing-library/react';
import type {Specimen} from '../../api/specimens';
import {expectAnnounced, isDisabled, json, mockApi, nurseUser, openDialog, renderWithApp, withAnnouncements} from '../../test/render';
import {SPECIMEN_ID, testSpecimen} from '../../test/specimens';
import {SpecimenStepDialog, type SpecimenStep} from './SpecimenStepDialog';

function renderDialog(step: SpecimenStep | null, handlers: Record<string, (request: Request) => Response> = {}, specimen: Specimen = testSpecimen()) {
  const onClose = vi.fn();
  const fetch = mockApi(handlers);
  const result = renderWithApp(<SpecimenStepDialog step={step} specimen={specimen} onClose={onClose} />, {user: nurseUser});
  return {...result, fetch, onClose};
}

const dialog = () => within(openDialog()!);
const bodyOf = (fetch: ReturnType<typeof mockApi>) => (fetch.mock.calls[0][0] as Request).clone().json();

describe('SpecimenStepDialog', () => {
  beforeEach(() => {
    vi.useFakeTimers({toFake: ['Date'], now: new Date('2026-11-18T14:05:00Z')});
  });

  it('marks it sent now, with the patient and specimen shown and the courier reference, from the version shown', async () => {
    withAnnouncements();
    const {user, fetch, onClose} = renderDialog('sent', {[`POST /api/specimens/${SPECIMEN_ID}/sent`]: () => json(testSpecimen({status: 'Sent'}))});

    expect(await screen.findByRole('heading', {name: 'Mark Sent'})).toBeInTheDocument();
    expect(dialog().getByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
    expect(dialog().getByText('MA26-000123')).toBeInTheDocument();
    expect(dialog().getByText('Skin biopsy (punch)')).toBeInTheDocument();
    expect(dialog().getByText('Northgate pathology')).toBeInTheDocument();
    expect(dialog().getByRole('combobox', {name: /Sent on/})).toHaveValue('18 November 2026');
    await user.type(dialog().getByRole('textbox', {name: /Courier or batch reference/}), 'CR-42');
    await user.click(dialog().getByRole('button', {name: 'Mark Sent'}));

    await expectAnnounced('Specimen marked sent');
    expect(await bodyOf(fetch)).toEqual({atUtc: null, reference: 'CR-42', expectedVersion: 3});
    expect(onClose).toHaveBeenCalled();
  });

  it("marks it received at a time typed, with the lab's reference", async () => {
    const {user, fetch} = renderDialog('received', {[`POST /api/specimens/${SPECIMEN_ID}/received`]: () => json(testSpecimen({status: 'Received'}))});

    await screen.findByRole('heading', {name: 'Mark Received'});
    const time = dialog().getByRole('textbox', {name: /Time received/});
    await user.clear(time);
    await user.type(time, '10:15');
    await user.tab();
    await user.type(dialog().getByRole('textbox', {name: /Lab's reference/}), 'NP-77');
    await user.click(dialog().getByRole('button', {name: 'Mark Received'}));

    await vi.waitFor(() => expect(fetch).toHaveBeenCalled());
    expect(await bodyOf(fetch)).toEqual({atUtc: '2026-11-18T10:15:00.000Z', reference: 'NP-77', expectedVersion: 3});
  });

  it('rejects it with a reason, none chosen to start with, and asks for a detail for Other', async () => {
    const {user, fetch} = renderDialog('rejected', {[`POST /api/specimens/${SPECIMEN_ID}/rejected`]: () => json(testSpecimen({status: 'RejectedByLab'}))});

    await screen.findByRole('heading', {name: 'Reject Specimen'});
    const reasons = dialog().getAllByRole('radio');
    expect(reasons.map((r) => (r as HTMLInputElement).checked)).toEqual([false, false, false, false, false, false]);
    expect(isDisabled(dialog().getByRole('button', {name: 'Reject Specimen'}))).toBe(true);

    await user.click(dialog().getByRole('radio', {name: 'Other'}));
    expect(isDisabled(dialog().getByRole('button', {name: 'Reject Specimen'}))).toBe(true);
    await user.click(dialog().getByRole('radio', {name: 'Insufficient sample'}));
    expect(dialog().getByRole('textbox', {name: /Detail \(optional\)/})).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Reject Specimen'}));

    await vi.waitFor(() => expect(fetch).toHaveBeenCalled());
    expect(await bodyOf(fetch)).toEqual({reason: 'InsufficientSample', detail: null, expectedVersion: 3});
  });

  it("marks it lost with what happened, showing the API's 409 in its words", async () => {
    const {user} = renderDialog('lost', {
      [`POST /api/specimens/${SPECIMEN_ID}/lost`]: () => json({title: 'Someone else changed this specimen while you had it open.'}, 409),
    });

    await screen.findByRole('heading', {name: 'Mark Lost'});
    expect(isDisabled(dialog().getByRole('button', {name: 'Mark Lost'}))).toBe(true);
    await user.type(dialog().getByRole('textbox', {name: /What happened/}), 'Not in the batch');
    await user.click(dialog().getByRole('button', {name: 'Mark Lost'}));

    expect(await dialog().findByText('Someone else changed this specimen while you had it open.')).toBeInTheDocument();
  });

  it("marks it entered in error, showing a 400 on the detail", async () => {
    const {user, fetch} = renderDialog('enteredInError', {
      [`POST /api/specimens/${SPECIMEN_ID}/entered-in-error`]: () => json({title: 'Invalid', errors: {Detail: ['Say what was wrong.']}}, 400),
    });

    expect(await screen.findByRole('heading', {name: 'Mark Entered in Error?'})).toBeInTheDocument();
    await user.type(dialog().getByRole('textbox', {name: /What happened/}), 'On the wrong patient');
    await user.click(dialog().getByRole('button', {name: 'Mark Entered in Error'}));

    expect(await dialog().findByText('Say what was wrong.')).toBeInTheDocument();
    expect(await bodyOf(fetch)).toEqual({detail: 'On the wrong patient', expectedVersion: 3});
  });

  it('is shut without a step', () => {
    renderDialog(null);
    expect(openDialog()).toBeNull();
  });
});
