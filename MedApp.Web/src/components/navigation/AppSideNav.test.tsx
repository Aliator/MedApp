import {screen, waitFor} from '@testing-library/react';
import {adminUser, isDisabled, json, mockApi, mockFetch, renderWithApp, staffUser} from '../../test/render';
import {AppSideNav, isCurrentSection, openPatientId, openProcedureId} from './AppSideNav';
import {pinsKey} from './pins';

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

describe('AppSideNav pinned patients', () => {
  const patients: Record<string, [string, string]> = {
    [patientId]: ['Freya', 'Ahmed'],
    'a0000000-0000-4000-8000-000000000002': ['Priya', 'Ali'],
  };

  function renderAt(path: string, pinned: string[] = []) {
    if (pinned.length) localStorage.setItem(pinsKey('patients', staffUser.username), JSON.stringify(pinned));
    mockApi({
      'GET /api/patients/*': (request) => {
        const id = new URL(request.url).pathname.split('/').pop()!;
        const name = patients[id];
        return name
          ? json({id, firstName: name[0], lastName: name[1], dateOfBirth: '1976-12-01', email: 'p@example.com'})
          : json({title: 'Not found'}, 404);
      },
    });
    return renderWithApp(<AppSideNav />, {user: staffUser, path});
  }

  it('pins the open patient', async () => {
    const {user} = renderAt(`/patients/${patientId}`);

    await user.click(await screen.findByRole('button', {name: 'Pin Freya Ahmed'}));

    expect(screen.getByRole('button', {name: 'Unpin Freya Ahmed'})).toBeInTheDocument();
    expect(JSON.parse(localStorage.getItem(pinsKey('patients', staffUser.username))!)).toEqual([patientId]);
  });

  it('keeps pinned patients under Patients on other pages', async () => {
    renderAt('/procedures', [patientId, 'a0000000-0000-4000-8000-000000000002']);

    const freya = await screen.findByRole('link', {name: 'Freya Ahmed'});
    expect(freya).toHaveAttribute('href', `/patients/${patientId}`);
    expect(freya).not.toHaveAttribute('aria-current');
    expect(await screen.findByRole('link', {name: 'Priya Ali'})).toBeInTheDocument();
  });

  it('marks a pinned patient as current while their pages are open', async () => {
    renderAt(`/patients/${patientId}/procedures`, [patientId]);

    expect(await screen.findByRole('link', {name: 'Freya Ahmed'})).toHaveAttribute('aria-current', 'page');
    expect(screen.getAllByRole('link', {name: 'Freya Ahmed'})).toHaveLength(1);
  });

  it('unpins a patient, who then goes when their pages are not open', async () => {
    const {user} = renderAt('/procedures', [patientId]);

    await user.click(await screen.findByRole('button', {name: 'Unpin Freya Ahmed'}));

    expect(screen.queryByRole('link', {name: 'Freya Ahmed'})).not.toBeInTheDocument();
    expect(JSON.parse(localStorage.getItem(pinsKey('patients', staffUser.username))!)).toEqual([]);
  });

  it('stops pinning at five, saying why', async () => {
    const five = ['p1', 'p2', 'p3', 'p4', 'p5'].map((n) => `${n}000000-0000-4000-8000-000000000000`.slice(0, 36));
    renderAt(`/patients/${patientId}`, five);

    const pin = await screen.findByRole('button', {name: /you can pin up to 5 patients/i});
    expect(isDisabled(pin)).toBe(true);
  });

  it('drops a pinned patient who no longer exists', async () => {
    const gone = 'b0000000-0000-4000-8000-000000000009';
    renderAt('/procedures', [patientId, gone]);

    await screen.findByRole('link', {name: 'Freya Ahmed'});
    await waitFor(() => expect(JSON.parse(localStorage.getItem(pinsKey('patients', staffUser.username))!)).toEqual([patientId]));
  });
});

describe('openProcedureId', () => {
  it.each([
    [`/procedures/${patientId}`, patientId],
    ['/procedures', null],
    [`/patients/${patientId}/procedures`, null],
  ])('pathname %s has open procedure %s', (pathname, expected) => {
    expect(openProcedureId(pathname)).toBe(expected);
  });
});

describe('AppSideNav pinned procedures', () => {
  const knee = 'c0000000-0000-4000-8000-000000000001';
  const scope = 'c0000000-0000-4000-8000-000000000002';
  const procedures: Record<string, {title: string; patientName: string}> = {
    [knee]: {title: 'Total knee replacement', patientName: 'Freya Ahmed'},
    [scope]: {title: 'Bronchoscopy', patientName: 'Priya Ali'},
  };

  function renderAt(path: string, pinned: string[] = []) {
    if (pinned.length) localStorage.setItem(pinsKey('procedures', staffUser.username), JSON.stringify(pinned));
    mockApi({
      'GET /api/procedures/patient-procedures/*': (request) => {
        const id = new URL(request.url).pathname.split('/').pop()!;
        return procedures[id] ? json({id, ...procedures[id]}) : json({title: 'Not found'}, 404);
      },
    });
    return renderWithApp(<AppSideNav />, {user: staffUser, path});
  }

  it("labels the open procedure with the patient's initials and the procedure's acronym, and pins it", async () => {
    const {user} = renderAt(`/procedures/${knee}`);

    const item = await screen.findByRole('link', {name: 'FA TKR'});
    expect(item).toHaveAttribute('href', `/procedures/${knee}`);
    expect(item).toHaveAttribute('aria-current', 'page');

    // The full name shows on hover.
    await user.hover(item);
    expect(await screen.findByRole('tooltip')).toHaveTextContent('Total knee replacement for Freya Ahmed');

    await user.click(screen.getByRole('button', {name: 'Pin Total knee replacement for Freya Ahmed'}));

    expect(JSON.parse(localStorage.getItem(pinsKey('procedures', staffUser.username))!)).toEqual([knee]);
  });

  it('keeps pinned procedures under Procedures on other pages, apart from pinned patients', async () => {
    renderAt('/patients', [knee, scope]);

    expect(await screen.findByRole('link', {name: 'FA TKR'})).not.toHaveAttribute('aria-current');
    expect(await screen.findByRole('link', {name: 'PA B'})).toBeInTheDocument();
    expect(localStorage.getItem(pinsKey('patients', staffUser.username))).toBeNull();
  });

  it('drops a pinned procedure that no longer exists', async () => {
    const gone = 'c0000000-0000-4000-8000-000000000009';
    renderAt('/', [knee, gone]);

    await screen.findByRole('link', {name: 'FA TKR'});
    await waitFor(() => expect(JSON.parse(localStorage.getItem(pinsKey('procedures', staffUser.username))!)).toEqual([knee]));
  });
});
