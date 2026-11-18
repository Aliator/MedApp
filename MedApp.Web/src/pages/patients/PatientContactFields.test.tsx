import {screen, waitFor} from '@testing-library/react';
import {useState} from 'react';
import type {PatientInput} from '../../api/patients';
import {adminUser, json, mockApi, renderWithApp} from '../../test/render';
import {CONTACT_MESSAGES, contactProblems, PatientContactFields, PatientPracticeField, PatientReminderField, REMINDER_HINT, reminderContactGap} from './PatientContactFields';
import {EMPTY_PATIENT} from './PatientFormFields';

function Harness({start = EMPTY_PATIENT, current}: {start?: PatientInput; current?: {id: string; name: string; isArchived: boolean}}) {
  const [value, setValue] = useState(start);
  return (
    <>
      <PatientContactFields value={value} onChange={setValue} />
      <PatientPracticeField value={value} onChange={setValue} currentPractice={current} />
    </>
  );
}

describe('contactProblems', () => {
  it('finds a postcode or mobile in the wrong format, and nothing wrong with blank ones', () => {
    expect(contactProblems({postcode: '', mobilePhone: ' '})).toBeNull();
    expect(contactProblems({postcode: 'zz1 1zz', mobilePhone: '07700 900123'})).toBeNull();
    expect(contactProblems({postcode: 'ZZ1', mobilePhone: '01632 960123'})?.fields).toEqual({
      postcode: [CONTACT_MESSAGES.postcode],
      mobilePhone: [CONTACT_MESSAGES.mobilePhone],
    });
  });
});

describe('PatientContactFields and PatientPracticeField', () => {
  it("says a mobile or postcode is wrong only once it's left, and not while it's typed again", async () => {
    mockApi({'GET /api/practices': () => json([])});
    const {user} = renderWithApp(<Harness />, {user: adminUser});

    const postcode = await screen.findByRole('textbox', {name: /^Postcode/});
    await user.type(postcode, 'ZZ1');
    expect(postcode).not.toHaveAccessibleDescription(/Enter a UK postcode/);
    await user.tab();
    expect(postcode).toHaveAccessibleDescription(/Enter a UK postcode/);
    await user.type(postcode, ' 1ZZ');
    expect(postcode).not.toHaveAccessibleDescription(/Enter a UK postcode/);
    await user.tab();
    expect(postcode).not.toHaveAccessibleDescription(/Enter a UK postcode/);
  });

  it("offers the patient's own practice, archived since, beside the open ones, with its address", async () => {
    mockApi({'GET /api/practices': () => json([{id: 'p-test', name: 'Testbrook Surgery', isArchived: false}])});
    const {user} = renderWithApp(
      <Harness start={{...EMPTY_PATIENT, practiceId: 'p-old'}} current={{id: 'p-old', name: 'Oldfield Practice', isArchived: true}} />,
      {user: adminUser},
    );

    const practice = await screen.findByRole('combobox', {name: /^Practice/});
    await waitFor(() => expect(practice).toHaveTextContent('Oldfield Practice (archived)'));
    expect(practice).toHaveAccessibleDescription(/No address in the directory/);
    await user.click(practice);
    expect((await screen.findAllByRole('option')).map((o) => o.textContent)).toEqual(['None', 'Oldfield Practice (archived)', 'Testbrook Surgery']);

    // Once another is chosen, theirs is still offered, so the choice can be undone.
    await user.click(screen.getByRole('option', {name: 'Testbrook Surgery'}));
    await user.click(practice);
    expect((await screen.findAllByRole('option')).map((o) => o.textContent)).toEqual(['None', 'Oldfield Practice (archived)', 'Testbrook Surgery']);
  });
});

describe('PatientReminderField', () => {
  it('says when the choice needs a mobile or an email the patient has not given, without blocking', () => {
    const none = {mobilePhone: '', email: ''};
    expect(reminderContactGap({...none, reminderChannel: 'None'})).toBeNull();
    expect(reminderContactGap({...none, reminderChannel: 'Text'})).toBe('Texts can’t be sent without a mobile number.');
    expect(reminderContactGap({...none, reminderChannel: 'Email'})).toBe('Emails can’t be sent without an email address.');
    expect(reminderContactGap({...none, reminderChannel: 'TextAndEmail'})).toBe('Nothing can be sent without a mobile or an email address.');
    expect(reminderContactGap({mobilePhone: '07700 900123', email: 'a@example.org', reminderChannel: 'TextAndEmail'})).toBeNull();
  });

  it('offers the choices in words with the hint, and warns when texts are chosen without a mobile', async () => {
    function ReminderHarness() {
      const [value, setValue] = useState<PatientInput>({...EMPTY_PATIENT, email: 'test.patient@example.org'});
      return <PatientReminderField value={value} onChange={setValue} />;
    }
    const {user} = renderWithApp(<ReminderHarness />, {user: adminUser});

    const reminders = await screen.findByRole('combobox', {name: /^Reminders/});
    expect(reminders).toHaveTextContent('No reminders');
    expect(reminders).toHaveAccessibleDescription(new RegExp(REMINDER_HINT.slice(0, 20)));
    await user.click(reminders);
    expect((await screen.findAllByRole('option')).map((o) => o.textContent)).toEqual(['No reminders', 'Text', 'Email', 'Text and email']);
    await user.click(screen.getByRole('option', {name: 'Text'}));
    expect(reminders).toHaveAccessibleDescription(/Texts can’t be sent without a mobile number/);
  });
});
