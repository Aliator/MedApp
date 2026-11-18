import {screen, waitFor, within} from '@testing-library/react';
import {isDisabled, json, mockApi, openDialog, renderWithApp, seniorClinicianUser} from '../../test/render';
import {DATA_REQUEST_ID, DATA_REQUEST_PATIENT, FILE_ID, NOTE_ID, readyDataRequest, testDataRequest, testReviewItems} from '../../test/dataRequests';
import {DataRequestReviewDialog, startingChoices, withheldOf} from './DataRequestReviewDialog';

const BASE = `/api/data-requests/${DATA_REQUEST_ID}`;

const dialogOpen = () =>
  waitFor(() => {
    const dialog = openDialog();
    expect(dialog).not.toBeNull();
    return dialog!;
  });

function render(handlers: Parameters<typeof mockApi>[0] = {}, onDone = vi.fn()) {
  const fetch = mockApi({[`GET ${BASE}/review-items`]: () => json(testReviewItems()), ...handlers});
  const request = testDataRequest({canReview: true});
  return {
    fetch,
    onDone,
    ...renderWithApp(<DataRequestReviewDialog request={request} patient={DATA_REQUEST_PATIENT} onCancel={vi.fn()} onDone={onDone} />, {
      user: seniorClinicianUser,
    }),
  };
}

describe('DataRequestReviewDialog', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('starts each item as the API says, and sends only the ticked ones with their reasons', () => {
    const items = testReviewItems();
    const choices = startingChoices(items);
    expect(withheldOf(items, choices)).toEqual([{kind: 'Note', itemId: NOTE_ID, reason: 'About another patient'}]);
  });

  it('lists the items with the serious harm test, the note about another patient ticked with its reason', async () => {
    render();
    const dialog = within(await dialogOpen());

    expect(dialog.getByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
    expect(dialog.getByText(/likely to cause serious harm to the physical or mental health of the patient or another person/)).toBeInTheDocument();
    const note = within(await dialog.findByRole('group', {name: /^Note: Skin check/}));
    expect(note.getByRole('checkbox', {name: 'Withhold this note'})).toBeChecked();
    expect(note.getByRole('textbox', {name: /Why it's withheld/})).toHaveValue('About another patient');
    expect(note.getByText(/by k\.ashford/)).toBeInTheDocument();

    const file = within(dialog.getByRole('group', {name: /^File: scan\.pdf/}));
    expect(file.getByText('Deleted')).toBeInTheDocument();
    expect(file.getByText('No text held.')).toBeInTheDocument();
    expect(file.getByRole('checkbox', {name: 'Withhold this file'})).not.toBeChecked();
    expect(dialog.getByText('1 withheld')).toBeInTheDocument();
  });

  it('asks for a reason before saving a newly withheld item, then saves the review from the version shown', async () => {
    const {user, fetch, onDone} = render({[`PUT ${BASE}/review`]: () => json(readyDataRequest())});
    const dialog = within(await dialogOpen());
    const file = within(await dialog.findByRole('group', {name: /^File: scan\.pdf/}));

    await user.click(file.getByRole('checkbox', {name: 'Withhold this file'}));
    const save = dialog.getByRole('button', {name: 'Save Review'});
    expect(isDisabled(save)).toBe(true);
    await user.type(file.getByRole('textbox', {name: /Why it's withheld/}), 'Would cause serious harm');
    await user.click(save);

    await waitFor(() => expect(onDone).toHaveBeenCalled());
    const put = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'PUT')!;
    expect(await put.json()).toEqual({
      withheld: [
        {kind: 'Note', itemId: NOTE_ID, reason: 'About another patient'},
        {kind: 'File', itemId: FILE_ID, reason: 'Would cause serious harm'},
      ],
      expectedVersion: 3,
    });
  });

  it('withholds nothing when every box is cleared, and shows a 409 in the API words', async () => {
    const {user, fetch} = render({
      [`PUT ${BASE}/review`]: () => json({title: 'This data request is closed, so it can’t be changed.'}, 409),
      [`GET ${BASE}`]: () => json(testDataRequest()),
    });
    const dialog = within(await dialogOpen());
    await user.click(await dialog.findByRole('checkbox', {name: 'Withhold this note'}));
    expect(dialog.getByText('Nothing withheld')).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Save Review'}));

    expect(await dialog.findByText('This data request is closed, so it can’t be changed.')).toBeInTheDocument();
    const put = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'PUT')!;
    expect((await put.json()).withheld).toEqual([]);
  });

  it("says when the record has nothing to review, or the items can't be loaded", async () => {
    const {unmount} = render({[`GET ${BASE}/review-items`]: () => json([])});
    expect(await screen.findByText(/Nothing in the record could be withheld/)).toBeInTheDocument();
    unmount();

    render({[`GET ${BASE}/review-items`]: () => json({title: 'Boom'}, 500)});
    expect(await screen.findByText("The record's items could not be loaded. Please try again.")).toBeInTheDocument();
  });

  it("shows a 400's messages, and an item with no date or author by its title only", async () => {
    const {user} = render({
      [`GET ${BASE}/review-items`]: () => json([{...testReviewItems()[0], atUtc: null, by: null}]),
      [`PUT ${BASE}/review`]: () => json({title: 'Invalid', status: 400, errors: {'Withheld[0].ItemId': ["That item isn't in this patient's record."]}}, 400),
    });
    const dialog = within(await dialogOpen());

    expect(await dialog.findByRole('group', {name: 'Note: Skin check'})).toBeInTheDocument();
    await user.click(dialog.getByRole('button', {name: 'Save Review'}));
    expect(await dialog.findByText("That item isn't in this patient's record.")).toBeInTheDocument();
  });
});

