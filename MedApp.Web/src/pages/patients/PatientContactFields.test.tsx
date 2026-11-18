import {screen, waitFor} from '@testing-library/react';
import {useState} from 'react';
import type {PatientInput} from '../../api/patients';
import {adminUser, json, mockApi, renderWithApp} from '../../test/render';
import {CONTACT_MESSAGES, contactProblems, PatientContactFields, PatientPracticeField} from './PatientContactFields';
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
