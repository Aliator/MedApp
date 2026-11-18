import {screen, waitFor} from '@testing-library/react';
import {expectAnnounced, findAlert, isDisabled, json, mockApi, renderWithApp, staffUser, withAnnouncements} from '../../test/render';
import {StaffDetailsCard} from './StaffDetailsCard';

const DETAILS = '/api/auth/me/details';

describe('StaffDetailsCard', () => {
  it('starts from your details, and saves them, blank as none', async () => {
    withAnnouncements();
    const fetch = mockApi({
      [`GET ${DETAILS}`]: () => json({fullName: 'Sam Test', jobTitle: 'Nurse'}),
      [`PUT ${DETAILS}`]: () => json({fullName: 'Dr Sam Test', jobTitle: null}),
    });
    const {user} = renderWithApp(<StaffDetailsCard />, {user: staffUser});

    expect(await screen.findByRole('heading', {name: 'Your details for letters'})).toBeInTheDocument();
    const name = await screen.findByRole('textbox', {name: /^Full name/});
    expect(screen.getByText('Used on letters you write or approve.')).toBeInTheDocument();
    expect(name).toHaveValue('Sam Test');
    const save = screen.getByRole('button', {name: 'Save Details'});
    expect(isDisabled(save)).toBe(true);
    await user.clear(name);
    await user.type(name, 'Dr Sam Test');
    await user.clear(screen.getByRole('textbox', {name: /^Job title/}));
    await user.click(save);

    await expectAnnounced('Your details for letters saved');
    const request = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'PUT')!;
    expect(await request.json()).toEqual({fullName: 'Dr Sam Test', jobTitle: null});
    await waitFor(() => expect(isDisabled(screen.getByRole('button', {name: 'Save Details'}))).toBe(true));
  });

  it("shows the API's 400 on its field, and other failures in words", async () => {
    let answer = 400;
    mockApi({
      [`GET ${DETAILS}`]: () => json({fullName: null, jobTitle: null}),
      [`PUT ${DETAILS}`]: () =>
        answer === 400
          ? json({title: 'Invalid', status: 400, errors: {FullName: ['The full name must be 100 characters or fewer.']}}, 400)
          : json({title: 'Server error'}, 500),
    });
    const {user} = renderWithApp(<StaffDetailsCard />, {user: staffUser});

    await user.type(await screen.findByRole('textbox', {name: /^Full name/}), 'A name');
    await user.click(screen.getByRole('button', {name: 'Save Details'}));
    await waitFor(() => expect(screen.getByRole('textbox', {name: /^Full name/})).toHaveAccessibleDescription(/100 characters or fewer/));

    answer = 500;
    await user.click(screen.getByRole('button', {name: 'Save Details'}));
    expect(await findAlert(/could not be saved|server error/i)).toBeInTheDocument();
  });

  it('says when they could not be loaded', async () => {
    mockApi({[`GET ${DETAILS}`]: () => json({title: 'Server error'}, 500)});
    renderWithApp(<StaffDetailsCard />, {user: staffUser});

    expect(await findAlert('Your details for letters could not be loaded.')).toBeInTheDocument();
  });
});
