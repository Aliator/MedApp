// @vitest-environment node
import {render, type RouterManifest} from './entry-server';
import {STAFF_ME, testAppointment, testCalendar} from './test/calendar';

/**
 * Server rendering, end to end, as the Node server runs it (in Node, with no browser): the page's route guards and
 * loaders run with the request's cookie, and its HTML comes back with its data in it.
 */

const API = 'https://api.test';
const MANIFEST: RouterManifest = {
  routes: {__root__: {css: ['/assets/app.css'], scripts: [{attrs: {type: 'module', src: '/assets/app.js'}}]}},
};

const json = (body: unknown) => new Response(JSON.stringify(body), {headers: {'content-type': 'application/json'}});
const signedIn = {isAuthenticated: true, name: 'test-user', roles: ['User']};
const patient = {
  id: '0000000d-0000-0000-0000-000000000001',
  firstName: 'Test',
  lastName: 'Patient',
  dateOfBirth: '1980-01-01',
  email: 'test.patient@example.org',
};

/** Answers the API by path; records each request. */
function api(routes: Record<string, unknown>) {
  const requests: Request[] = [];
  vi.stubGlobal('fetch', async (request: Request) => {
    requests.push(request);
    const {pathname} = new URL(request.url);
    return pathname in routes ? json(routes[pathname]) : new Response(null, {status: 404});
  });
  return requests;
}

const page = (path: string, cookie?: string) =>
  render(new Request(`http://medapp.test${path}`, {headers: cookie ? {cookie} : {}}), {
    manifest: MANIFEST,
    context: {apiUrl: API, cookie, ssrKey: 'test-key'},
  });

describe('server rendering', () => {
  it('renders the whole page, with the build’s stylesheets and scripts', async () => {
    api({'/api/auth/whoami': {isAuthenticated: false}});

    const response = await page('/login');
    const html = await response.text();

    expect(response.status).toBe(200);
    expect(html).toMatch(/^<!DOCTYPE html><html lang="en-GB" data-theme="dark"/);
    expect(html).toContain('<link rel="stylesheet" href="/assets/app.css"');
    expect(html).toContain('<script type="module" src="/assets/app.js"');
    expect(html).toContain('Sign in');
  });

  it('sends someone signed out to sign in', async () => {
    api({'/api/auth/whoami': {isAuthenticated: false}});

    const response = await page('/patients');

    expect(response.status).toBe(307);
    expect(response.headers.get('location')).toBe('/login');
  });

  it('sends someone who must change their password to do that, without asking for what the API would refuse', async () => {
    const requests = api({'/api/auth/whoami': {...signedIn, mustChangePassword: true}});

    const response = await page('/patients', 'medapp_session=abc');

    expect(response.status).toBe(307);
    expect(response.headers.get('location')).toBe('/change-password');
    expect(requests.map((r) => new URL(r.url).pathname)).toEqual(['/api/auth/whoami']);
  });

  it("renders the change-password page in the account's settings", async () => {
    api({
      '/api/auth/whoami': {...signedIn, mustChangePassword: true},
      '/api/auth/me/settings': {settings: JSON.stringify({'medapp.colorMode': 'light'})},
    });

    const response = await page('/change-password', 'medapp_session=abc');
    const html = await response.text();

    expect(response.status).toBe(200);
    expect(html).toMatch(/^<!DOCTYPE html><html lang="en-GB" data-theme="light"/);
    expect(html).toContain('Choose your password');
    expect(html).toContain('test-user');
  });

  it('sends someone who needn’t change their password from the change-password page home', async () => {
    api({'/api/auth/whoami': signedIn});

    const response = await page('/change-password', 'medapp_session=abc');

    expect(response.status).toBe(307);
    expect(response.headers.get('location')).toBe('/');
  });

  it("renders a signed-in page with its data, fetched as the user with their cookie, in the account's settings", async () => {
    const requests = api({
      '/api/auth/whoami': signedIn,
      '/api/auth/me/settings': {settings: JSON.stringify({'medapp.colorMode': 'light', 'medapp.pageSize': '25'})},
      '/api/notifications': {items: [], unreadCount: 0},
      '/api/patients/search': {items: [patient], page: 1, pageSize: 25, totalCount: 1, totalPages: 1},
    });

    const response = await page('/patients', 'medapp_session=abc');
    const html = await response.text();

    expect(response.status).toBe(200);
    // The account's light mode, from its saved settings, from the first paint.
    expect(html).toMatch(/^<!DOCTYPE html><html lang="en-GB" data-theme="light"/);
    expect(html).toContain('Patient');
    expect(html).toContain('test.patient@example.org');
    // Every call went to the API itself, as the user, with the server's key, for the account's page size.
    expect(requests.every((r) => new URL(r.url).origin === API)).toBe(true);
    expect(requests.every((r) => r.headers.get('cookie') === 'medapp_session=abc')).toBe(true);
    expect(requests.every((r) => r.headers.get('x-medapp-ssr-key') === 'test-key')).toBe(true);
    const search = requests.find((r) => new URL(r.url).pathname === '/api/patients/search')!;
    expect(new URL(search.url).searchParams.get('PageSize')).toBe('25');
  });

  it("renders a patient's page with the first page of their timeline and what's due", async () => {
    const requests = api({
      '/api/auth/whoami': signedIn,
      '/api/auth/me/settings': {settings: null},
      '/api/notifications': {items: [], unreadCount: 0},
      [`/api/patients/${patient.id}`]: patient,
      [`/api/patients/${patient.id}/timeline/overview`]: {
        upcoming: [{procedureId: '0000000d-0000-0000-0000-000000000002', title: 'Knee assessment', status: 'Assigned', dueDate: '2026-06-30', isOverdue: false}],
        openCount: 1,
        procedures: [],
      },
      [`/api/patients/${patient.id}/timeline`]: {
        items: [
          {
            id: '0000000d-0000-0000-0000-000000000003',
            kind: 'Stages',
            action: 'StageFinished',
            atUtc: '2026-06-15T09:00:00Z',
            username: 'test.staff',
            procedureId: '0000000d-0000-0000-0000-000000000002',
            procedureTitle: 'Knee assessment',
            stageName: 'Consultation',
            changes: [],
          },
          {
            id: '0000000d-0000-0000-0000-000000000004',
            kind: 'Appointments',
            action: 'AppointmentBooked',
            atUtc: '2026-06-15T08:00:00Z',
            username: 'test.staff',
            procedureId: null,
            procedureTitle: null,
            appointmentStartUtc: '2026-06-22T09:00:00Z',
            appointmentEndUtc: '2026-06-22T09:30:00Z',
            appointmentStaffUsername: 'test.doctor',
          },
        ],
        nextCursor: 'older',
      },
    });

    const html = await (await page(`/patients/${patient.id}`, 'medapp_session=abc')).text();

    expect(html).toContain('test.patient@example.org');
    expect(html).toContain('Finished Consultation');
    // An appointment's time in UK time, the same on the server as in the browser.
    expect(html).toContain('Mon 22 Jun 2026, 10:00 to 10:30 with test.doctor');
    expect(html).toContain('Monday 15 June 2026');
    expect(html).toContain('Due 30 Jun 2026');
    expect(html).toContain('Load More');
    const feed = requests.find((r) => new URL(r.url).pathname === `/api/patients/${patient.id}/timeline`)!;
    expect(new URL(feed.url).search).toBe('?PageSize=20');
  });

  it("renders an admin's calendar templates on the tab in the URL, with its data", async () => {
    const requests = api({
      '/api/auth/whoami': {...signedIn, roles: ['Admin', 'User']},
      '/api/auth/me/settings': {settings: null},
      '/api/notifications': {items: [], unreadCount: 0},
      '/api/calendar/week-templates': [
        {
          id: '0000000d-0000-0000-0000-000000000010',
          name: 'Full time',
          isArchived: false,
          staffCount: 2,
          days: ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'].map((dayOfWeek) => ({dayOfWeek, dayTemplateId: 'd', dayTemplateName: 'Standard day'})),
        },
      ],
    });

    const html = await (await page('/admin/calendar-templates?tab=week', 'medapp_session=abc')).text();

    expect(html).toContain('Full time');
    expect(html).toContain('Mon to Fri: Standard day');
    expect(html).toContain('1 active week template');
    expect(requests.some((r) => new URL(r.url).pathname === '/api/calendar/day-templates')).toBe(false);
  });

  it("renders the calendar's week with its appointments, and leaves the time now to the browser", async () => {
    vi.useFakeTimers({toFake: ['Date'], now: new Date('2026-11-23T07:00:00Z')});
    const requests = api({
      '/api/auth/whoami': signedIn,
      '/api/auth/me/settings': {settings: null},
      '/api/notifications': {items: [], unreadCount: 0},
      '/api/calendar/staff': [STAFF_ME],
      '/api/appointments/search': {items: [], totalCount: 2},
      '/api/calendar': testCalendar({from: '2026-11-23', today: '2026-11-23', appointments: [testAppointment({date: '2026-11-24'})]}),
    });

    const html = await (await page('/calendar', 'medapp_session=abc')).text();

    expect(html).toContain('My calendar · 23 to 29 November 2026');
    expect(html).toContain('Test Patient, Knee review: Assessment, 09:00 to 09:30, Booked');
    expect(html).toContain('Needs Booking (2)');
    expect(html).not.toContain('class="mc-now"');
    const calendar = requests.find((r) => new URL(r.url).pathname === '/api/calendar')!;
    expect(new URL(calendar.url).search).toBe('?From=2026-11-23&To=2026-11-29');
  });

  it("renders Home with the user's day", async () => {
    vi.useFakeTimers({toFake: ['Date'], now: new Date('2026-11-23T07:00:00Z')});
    api({
      '/api/auth/whoami': signedIn,
      '/api/auth/me/settings': {settings: null},
      '/api/notifications': {items: [], unreadCount: 0},
      '/api/calendar': testCalendar({from: '2026-11-23', dayCount: 1, today: '2026-11-23', appointments: [testAppointment({date: '2026-11-23'})]}),
    });

    const html = await (await page('/', 'medapp_session=abc')).text();

    expect(html).toContain('Welcome, test-user');
    expect(html).toContain('Today, Monday 23 November');
    expect(html).toContain('Test Patient, Knee review: Assessment, 09:00 to 09:30, Booked');
  });

  it('still renders the page when its data fails, for it to show its error', async () => {
    api({
      '/api/auth/whoami': signedIn,
      '/api/auth/me/settings': {settings: null},
      '/api/notifications': {items: [], unreadCount: 0},
    });

    const response = await page('/patients', 'medapp_session=abc');

    expect(response.status).toBe(200);
    expect(await response.text()).toContain('Patients');
  });

  it("sizes the page's text as the account chose, from the first paint, leaving Standard to the browser", async () => {
    const at = async (textSize: string) => {
      api({
        '/api/auth/whoami': signedIn,
        '/api/auth/me/settings': {settings: JSON.stringify({'medapp.textSize': textSize})},
        '/api/notifications': {items: [], unreadCount: 0},
        '/api/patients/search': {items: [], page: 1, pageSize: 10, totalCount: 0, totalPages: 0},
      });
      return (await page('/patients', 'medapp_session=abc')).text();
    };

    expect(await at('large')).toMatch(/^<!DOCTYPE html><html lang="en-GB" data-theme="dark" style="font-size:112.5%"/);
    expect(await at('standard')).toMatch(/^<!DOCTYPE html><html lang="en-GB" data-theme="dark">/);
  });
});
