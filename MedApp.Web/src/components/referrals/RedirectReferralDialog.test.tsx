import {screen, waitFor, within} from '@testing-library/react';
import {isDisabled, json, mockApi, openDialog, renderWithApp, userWithRoles} from '../../test/render';
import {REFERRAL_ID, SPECIALTIES, testReferral, UROLOGY_ID} from '../../test/referrals';
import {RedirectReferralDialog} from './RedirectReferralDialog';

const REDIRECT = `POST /api/referrals/${REFERRAL_ID}/redirect`;
const clinician = userWithRoles('d.clinician', 'Clinician');
const CARDIOLOGY = {id: 'cardio', name: 'cardiology', isArchived: false, version: 1};
const ARCHIVED = {id: 'old', name: 'Old clinic', isArchived: true, version: 1};
const redirected = testReferral({specialtyId: UROLOGY_ID, specialtyName: 'Urology'});

function render(handlers: Parameters<typeof mockApi>[0] = {}) {
  const fetch = mockApi({
    'GET /api/specialties': () => json([...SPECIALTIES, CARDIOLOGY, ARCHIVED]),
    [REDIRECT]: () => json(redirected),
    [`GET /api/referrals/${REFERRAL_ID}`]: () => json(testReferral()),
    ...handlers,
  });
  const onCancel = vi.fn();
  const onRedirected = vi.fn();
  const result = renderWithApp(
    <RedirectReferralDialog isOpen referral={testReferral({canTriage: true})} onCancel={onCancel} onRedirected={onRedirected} />,
    {user: clinician},
  );
  return {...result, fetch, onCancel, onRedirected};
}

const dialog = () => within(openDialog()!);
const posted = async (fetch: ReturnType<typeof mockApi>) => (fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === 'POST')!).json();

describe('RedirectReferralDialog', () => {
  it('starts with nowhere chosen, and offers the other specialties in use, by name', async () => {
    const {user} = render();

    expect(await screen.findByRole('heading', {name: 'Redirect Referral'})).toBeInTheDocument();
    expect(dialog().getByRole('region', {name: 'Patient: Ahmed, Freya'})).toBeInTheDocument();
    expect(dialog().getByRole('radio', {name: /Another specialty here/})).not.toBeChecked();
    expect(dialog().getByRole('radio', {name: /Elsewhere/})).not.toBeChecked();
    expect(dialog().queryByRole('combobox', {name: /Specialty/})).not.toBeInTheDocument();
    expect(isDisabled(dialog().getByRole('button', {name: 'Continue'}))).toBe(true);

    await user.click(dialog().getByRole('radio', {name: /Another specialty here/}));
    const specialty = dialog().getByRole('combobox', {name: /Specialty/});
    expect(specialty).toHaveTextContent('Choose a specialty');
    await waitFor(() => expect(isDisabled(specialty)).toBe(false));
    await user.click(specialty);
    expect((await screen.findAllByRole('option')).map((o) => o.textContent)).toEqual(['cardiology', 'Urology']);
  });

  it('redirects to another specialty here after checking, with why', async () => {
    const {user, fetch, onRedirected} = render();

    await screen.findByRole('heading', {name: 'Redirect Referral'});
    await user.click(dialog().getByRole('radio', {name: /Another specialty here/}));
    await user.click(dialog().getByRole('combobox', {name: /Specialty/}));
    await user.click(await screen.findByRole('option', {name: 'Urology'}));
    expect(isDisabled(dialog().getByRole('button', {name: 'Continue'}))).toBe(true);
    await user.type(dialog().getByRole('textbox', {name: /Why/}), 'Synthetic: a urology problem.');
    await user.click(dialog().getByRole('button', {name: 'Continue'}));

    expect(await screen.findByRole('heading', {name: 'Check and Confirm'})).toBeInTheDocument();
    expect(dialog().getByText(/It moves to Urology's referrals, still awaiting triage/)).toBeInTheDocument();
    expect(dialog().getByText('Urology, here')).toBeInTheDocument();
    expect(dialog().getByText('Synthetic: a urology problem.')).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Redirect Referral'}));

    await waitFor(() => expect(onRedirected).toHaveBeenCalledWith(redirected));
    expect(await posted(fetch)).toEqual({specialtyId: UROLOGY_ID, elsewhere: null, detail: 'Synthetic: a urology problem.', expectedVersion: 2});
  }, 15_000);

  it('redirects elsewhere, closing it', async () => {
    const {user, fetch} = render();

    await screen.findByRole('heading', {name: 'Redirect Referral'});
    await user.click(dialog().getByRole('radio', {name: /Elsewhere/}));
    await user.type(dialog().getByRole('textbox', {name: /Where to/}), 'Synthetic Community Service');
    await user.type(dialog().getByRole('textbox', {name: /Why/}), 'Seen closer to home.');
    await user.click(dialog().getByRole('button', {name: 'Continue'}));

    expect(await dialog().findByText(/closed as redirected elsewhere/)).toBeInTheDocument();
    expect(dialog().getByText('Synthetic Community Service')).toBeInTheDocument();
    // Back to the form, where to kept, and on again.
    await user.click(dialog().getByRole('button', {name: 'Back'}));
    expect(await dialog().findByRole('textbox', {name: /Where to/})).toHaveValue('Synthetic Community Service');
    await user.click(dialog().getByRole('button', {name: 'Continue'}));
    await user.click(await dialog().findByRole('button', {name: 'Redirect Referral'}));
    await waitFor(async () =>
      expect(await posted(fetch)).toEqual({specialtyId: null, elsewhere: 'Synthetic Community Service', detail: 'Seen closer to home.', expectedVersion: 2}),
    );
  }, 15_000);

  it("shows the API's errors: a 400 back on the form, a 409 in its words", async () => {
    let attempt = 0;
    const {user} = render({
      [REDIRECT]: () =>
        ++attempt === 1
          ? json({title: 'Validation failed', status: 400, errors: {Elsewhere: ['Say where it was sent.']}}, 400)
          : json({title: 'Someone changed this referral meanwhile.', status: 409}, 409),
    });

    await screen.findByRole('heading', {name: 'Redirect Referral'});
    await user.click(dialog().getByRole('radio', {name: /Elsewhere/}));
    await user.type(dialog().getByRole('textbox', {name: /Where to/}), 'Somewhere');
    await user.type(dialog().getByRole('textbox', {name: /Why/}), 'Because.');
    await user.click(dialog().getByRole('button', {name: 'Continue'}));
    await user.click(await screen.findByRole('button', {name: 'Redirect Referral'}));
    expect(await dialog().findByText('Say where it was sent.')).toBeInTheDocument();

    await user.click(dialog().getByRole('button', {name: 'Continue'}));
    await user.click(await screen.findByRole('button', {name: 'Redirect Referral'}));
    expect(await dialog().findByText('Someone changed this referral meanwhile.')).toBeInTheDocument();
  }, 15_000);

  it('says when there is no other specialty, and cancels', async () => {
    const {user, onCancel} = render({'GET /api/specialties': () => json([SPECIALTIES[0]])});

    await screen.findByRole('heading', {name: 'Redirect Referral'});
    await user.click(dialog().getByRole('radio', {name: /Another specialty here/}));
    expect(await dialog().findByText("There's no other specialty here to redirect it to.")).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));
    expect(onCancel).toHaveBeenCalled();
  });
});
