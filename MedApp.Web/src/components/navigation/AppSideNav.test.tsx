import {screen, waitFor} from '@testing-library/react';
import {adminUser, json, mockApi, mockFetch, renderWithApp, staffUser} from '../../test/render';
import {AppSideNav, isCurrentSection, openPatientId} from './AppSideNav';

describe('isCurrentSection', () => {
  it.each([
    ['/', '/', true],
    ['/patients', '/', false],
    ['/patients', '/patients', true],
    ['/patients/add', '/patients', true],
    ['/patients-archive', '/patients', false],
    ['/admin/users', '/admin/users', true],
  ])('pathname %s is under %s: %s', (pathname, path, expected) => {
    expect(isCurrentSection(pathname, path)).toBe(expected);
  });
});

describe('AppSideNav', () => {
  it('shows the admin pages and role to an admin', async () => {
    renderWithApp(<AppSideNav />, {user: adminUser});

    expect(await screen.findByRole('link', {name: /users/i})).toHaveAttribute('href', '/admin/users');
    expect(screen.getByText('test-admin')).toBeInTheDocument();
    expect(screen.getByText('Administrator')).toBeInTheDocument();
  });

  it('hides the admin pages from other users', async () => {
    renderWithApp(<AppSideNav />, {user: staffUser});

    expect(await screen.findByRole('link', {name: /patients/i})).toHaveAttribute('href', '/patients');
    expect(screen.queryByRole('link', {name: /^users$/i})).not.toBeInTheDocument();
    expect(screen.getByText('User')).toBeInTheDocument();
  });

  it('marks the current section', async () => {
    renderWithApp(<AppSideNav />, {user: adminUser, path: '/patients/add'});

    expect(await screen.findByRole('link', {name: /patients/i})).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('link', {name: /home/i})).not.toHaveAttribute('aria-current');
  });

  it('navigates on the client when a link is clicked', async () => {
    const {user, router} = renderWithApp(<AppSideNav />, {user: adminUser});

    await user.click(await screen.findByRole('link', {name: /patients/i}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/patients'));
  });

  it('signs out, clears cached data and goes to the login page', async () => {
    const fetch = mockFetch(() => new Response(null, {status: 204}));
    const {user, router, queryClient} = renderWithApp(<AppSideNav />, {user: adminUser});
    queryClient.setQueryData(['patients'], [{id: 'synthetic-patient'}]);

    await user.click(await screen.findByRole('button', {name: /logout/i}));

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'));
    expect(new URL((fetch.mock.calls[0][0] as Request).url).pathname).toBe('/api/auth/logout');
    await waitFor(() => expect(queryClient.getQueryData(['patients'])).toBeUndefined());
  });

  it('switches between dark and light mode and remembers the choice', async () => {
    const {user} = renderWithApp(<AppSideNav />, {user: adminUser});

    await user.click(await screen.findByRole('button', {name: /switch to light mode/i}));

    expect(screen.getByRole('button', {name: /switch to dark mode/i})).toBeInTheDocument();
    expect(localStorage.getItem('medapp.colorMode')).toBe('light');
  });
});

const patientId = 'f184a7b8-2585-4437-bcd4-e1700be38287';

describe('openPatientId', () => {
  it.each([
    [`/patients/${patientId}`, patientId],
    [`/patients/${patientId}/edit`, patientId],
    [`/patients/${patientId}/procedures`, patientId],
    ['/patients', null],
    ['/patients/add', null],
    ['/procedures', null],
  ])('pathname %s has open patient %s', (pathname, expected) => {
    expect(openPatientId(pathname)).toBe(expected);
  });
});

describe('AppSideNav with a patient open', () => {
  function renderAt(path: string) {
    mockApi({
      [`GET /api/patients/${patientId}`]: () =>
        json({id: patientId, firstName: 'Freya', lastName: 'Ahmed', dateOfBirth: '1976-12-01', email: 'f@example.com'}),
    });
    return renderWithApp(<AppSideNav />, {user: staffUser, path});
  }

  it("shows the patient under Patients, marked as the current page, on any of their pages", async () => {
    renderAt(`/patients/${patientId}/edit`);

    const patient = await screen.findByRole('link', {name: 'Freya Ahmed'});
    expect(patient).toHaveAttribute('href', `/patients/${patientId}`);
    expect(patient).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('link', {name: /^patients$/i})).toHaveAttribute('href', '/patients');
    expect(screen.getByRole('link', {name: /^patients$/i})).not.toHaveAttribute('aria-current');
    expect(screen.getByRole('button', {expanded: true})).toBeInTheDocument();
  });

  it('shows no patient on the patients list', async () => {
    renderAt('/patients');

    expect(await screen.findByRole('link', {name: /^patients$/i})).toHaveAttribute('aria-current', 'page');
    expect(screen.queryByRole('link', {name: 'Freya Ahmed'})).not.toBeInTheDocument();
  });
});
