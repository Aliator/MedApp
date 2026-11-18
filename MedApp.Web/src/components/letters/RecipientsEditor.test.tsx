import {screen, within} from '@testing-library/react';
import {useState} from 'react';
import {emptyRecipient, MAX_RECIPIENTS, PATIENTS_UNLISTED_PRACTICE, recipientInput, type RecipientInput} from '../../api/letters';
import {isDisabled, json, mockApi, renderWithApp, staffUser} from '../../test/render';
import {LETTER_PRACTICE_ID, patientRecipient, practiceRecipient} from '../../test/letters';
import {RecipientsEditor, UNLISTED_PRACTICE_LABEL} from './RecipientsEditor';

const OTHER_PRACTICE_ID = '00000061-0000-0000-0000-000000000099';

const practices = [
  {id: LETTER_PRACTICE_ID, name: 'Testbrook Surgery', odsCode: 'ZZZ123', isArchived: false},
  {id: OTHER_PRACTICE_ID, name: 'Example Lane Practice', odsCode: null, isArchived: false},
];

/** The editor holding its recipients, as the letter page does; `changes` sees each change. */
function Holder({initial, hasReferral, isDisabled, changes}: {initial: RecipientInput[]; hasReferral: boolean; isDisabled?: boolean; changes: (r: RecipientInput[]) => void}) {
  const [recipients, setRecipients] = useState(initial);
  return (
    <RecipientsEditor
      recipients={recipients}
      hasReferral={hasReferral}
      isDisabled={isDisabled}
      onChange={(next) => {
        changes(next);
        setRecipients(next);
      }}
    />
  );
}

function render(initial: RecipientInput[], {hasReferral = false, isDisabled = false} = {}) {
  mockApi({'GET /api/practices': () => json(practices)});
  const changes = vi.fn();
  const result = renderWithApp(<Holder initial={initial} hasReferral={hasReferral} isDisabled={isDisabled} changes={changes} />, {user: staffUser});
  const last = () => changes.mock.calls.at(-1)![0] as RecipientInput[];
  return {...result, changes, last};
}

const group = (name: RegExp) => screen.getByRole('group', {name});

describe('RecipientsEditor', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('shows each recipient, the first addressed to and the rest as copies, and moves and removes them', async () => {
    const {user, last} = render([recipientInput(practiceRecipient()), recipientInput(patientRecipient())]);

    expect(await screen.findByRole('group', {name: 'Recipient 1, Addressed to: GP practice'})).toBeInTheDocument();
    expect(group(/Recipient 2, Copy: The patient/)).toHaveTextContent('Freya Ahmed, at their address on record.');
    expect(isDisabled(screen.getByRole('button', {name: 'Move recipient 1 up'}))).toBe(true);
    expect(isDisabled(screen.getByRole('button', {name: 'Move recipient 2 down'}))).toBe(true);

    await user.click(screen.getByRole('button', {name: 'Move recipient 2 up'}));
    expect(last().map((r) => r.kind)).toEqual(['Patient', 'GpPractice']);
    await user.click(screen.getByRole('button', {name: 'Move recipient 1 down'}));
    expect(last().map((r) => r.kind)).toEqual(['GpPractice', 'Patient']);

    await user.click(screen.getByRole('button', {name: 'Remove recipient 1'}));
    expect(last().map((r) => r.kind)).toEqual(['Patient']);
  });

  it('chooses a GP practice from the directory', async () => {
    const {user, last} = render([emptyRecipient('GpPractice')]);

    const practice = await within(await screen.findByRole('group', {name: /Recipient 1/})).findByRole('combobox', {name: /Practice/});
    await user.click(practice);
    await user.click(await screen.findByRole('option', {name: 'Example Lane Practice'}));
    expect(last()[0].practiceId).toBe(OTHER_PRACTICE_ID);

    // Or the patient's own practice that isn't in the directory, filled from their record by the API.
    await user.click(practice);
    await user.click(await screen.findByRole('option', {name: UNLISTED_PRACTICE_LABEL}));
    expect(last()[0].practiceId).toBe(PATIENTS_UNLISTED_PRACTICE);
  });

  it("adds someone else, typing their name, address, postcode and secure email; the referrer only for a referral's letter", async () => {
    const {user, last} = render([]);

    expect(await screen.findByText(/No recipients yet/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Add Recipient'}));
    expect(screen.queryByRole('menuitem', {name: 'The referrer'})).not.toBeInTheDocument();
    await user.click(await screen.findByRole('menuitem', {name: 'Someone else'}));

    const other = within(group(/Recipient 1, Addressed to: Someone else/));
    await user.type(other.getByRole('textbox', {name: /Name/}), 'Dr Example');
    await user.type(other.getByRole('textbox', {name: /Address/}), '1 Street');
    await user.type(other.getByRole('textbox', {name: /Postcode/}), 'ZZ1 1ZZ');
    await user.type(other.getByRole('textbox', {name: /Secure email/}), 'dr@nhs.net');
    expect(last()[0]).toMatchObject({kind: 'Other', name: 'Dr Example', addressLines: '1 Street', postcode: 'ZZ1 1ZZ', email: 'dr@nhs.net'});
  });

  it("adds the referrer: from the directory as it is, or with an address typed when the referral names no practice", async () => {
    const {user, last} = render([{...emptyRecipient('Referrer'), practiceId: LETTER_PRACTICE_ID, name: 'Testbrook Surgery'}], {hasReferral: true});

    expect(await screen.findByText('Testbrook Surgery, from the directory.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Add Recipient'}));
    await user.click(await screen.findByRole('menuitem', {name: 'The referrer'}));
    const typed = within(group(/Recipient 2, Copy: The referrer/));
    expect(typed.getByText(/names no practice from the directory/)).toBeInTheDocument();
    await user.type(typed.getByRole('textbox', {name: /Address/}), '9 Lane');
    expect(last()[1]).toMatchObject({kind: 'Referrer', addressLines: '9 Lane'});
  });

  it('says what an unnamed patient or referrer gets', async () => {
    render([emptyRecipient('Patient'), emptyRecipient('Referrer'), {...emptyRecipient('Referrer'), practiceId: 'p'}], {hasReferral: true});
    expect(await screen.findByText('The patient, at their address on record.')).toBeInTheDocument();
    expect(screen.getByText("The referral's practice, from the directory.")).toBeInTheDocument();
  });

  it(`stops at ${MAX_RECIPIENTS} recipients, and does nothing while disabled`, async () => {
    const full = Array.from({length: MAX_RECIPIENTS}, () => emptyRecipient('Patient'));
    const {unmount} = render(full);
    expect(await screen.findByText(`A letter can have at most ${MAX_RECIPIENTS} recipients.`)).toBeInTheDocument();
    expect(isDisabled(screen.getByRole('button', {name: 'Add Recipient'}))).toBe(true);
    unmount();

    render([emptyRecipient('Patient')], {isDisabled: true});
    expect(isDisabled(await screen.findByRole('button', {name: 'Remove recipient 1'}))).toBe(true);
  });
});
