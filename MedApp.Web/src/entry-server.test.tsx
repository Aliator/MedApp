// @vitest-environment node
import {render, type RouterManifest} from './entry-server';
import {STAFF_ME, STAFF_OTHER, testAppointment, testCalendar, testMeeting} from './test/calendar';
import {insights, templates} from './test/insights';
import {ROLE_PERMISSIONS} from './test/roles';

/**
 * Server rendering, end to end, as the Node server runs it (in Node, with no browser): the page's route guards and
 * loaders run with the request's cookie, and its HTML comes back with its data in it.
 */

const API = 'https://api.test';
const MANIFEST: RouterManifest = {
  routes: {__root__: {css: ['/assets/app.css'], scripts: [{attrs: {type: 'module', src: '/assets/app.js'}}]}},
};

const json = (body: unknown) => new Response(JSON.stringify(body), {headers: {'content-type': 'application/json'}});
// What whoami says, permissions and all (the standard roles' own: `ROLE_PERMISSIONS`).
const signedIn = {isAuthenticated: true, name: 'test-user', roles: ['All staff (before roles)'], permissions: ROLE_PERMISSIONS['All staff (before roles)']};
const signedInAdmin = {isAuthenticated: true, name: 'test-admin', roles: ['Admin', 'All staff (before roles)'], permissions: ROLE_PERMISSIONS.Admin};
/** A synthetic attached file, as the API lists it. */
const fileList = (fields: Record<string, unknown>) => ({
  items: [
    {
      id: '0000000d-0000-0000-0000-000000000009',
      patientId: '0000000d-0000-0000-0000-000000000001',
      attachedTo: 'Patient',
      fileName: 'referral-letter.pdf',
      contentType: 'application/pdf',
      typeName: 'PDF',
      sizeBytes: 2048,
      scanStatus: 'NotScanned',
      hasThumbnail: false,
      uploadedByUsername: 'test.staff',
      uploadedAtUtc: '2026-06-15T09:00:00Z',
      canDelete: true,
      isDeleted: false,
      ...fields,
    },
  ],
  totalCount: 1,
  maxFileBytes: 20 * 1024 * 1024,
  allowedExtensions: ['.pdf'],
});
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

const page = (path: string, cookie?: string, cspNonce?: string) =>
  render(new Request(`http://medapp.test${path}`, {headers: cookie ? {cookie} : {}}), {
    manifest: MANIFEST,
    context: {apiUrl: API, cookie, ssrKey: 'test-key', cspNonce},
  });

/** Every <script> tag in the page, opening tag only. */
const scriptTags = (html: string) => [...html.matchAll(/<script\b[^>]*>/g)].map(([tag]) => tag);

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

  it('puts the page’s nonce on every script it writes, and in the head for the browser’s router', async () => {
    api({
      '/api/auth/whoami': signedIn,
      '/api/auth/me/settings': {settings: null},
      '/api/notifications': {items: [], unreadCount: 0},
      '/api/patients/search': {items: [patient], page: 1, pageSize: 10, totalCount: 1, totalPages: 1},
    });

    const html = await (await page('/patients', 'medapp_session=abc', 'dGVzdC1ub25jZS0xMjM0NQ==')).text();

    const scripts = scriptTags(html);
    // The router's hydration data is written inline, so the policy must allow it, by nonce.
    expect(scripts.some((tag) => !tag.includes(' src='))).toBe(true);
    expect(scripts.every((tag) => tag.includes('nonce="dGVzdC1ub25jZS0xMjM0NQ=="'))).toBe(true);
    expect(html).toContain('<meta property="csp-nonce" content="dGVzdC1ub25jZS0xMjM0NQ=="');
  });

  it('writes no nonce when the page has none', async () => {
    api({'/api/auth/whoami': {isAuthenticated: false}});

    const html = await (await page('/login')).text();

    expect(html).not.toContain('nonce');
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

  it('offers "Forgot your password?" on the login page only when the server can send email', async () => {
    api({'/api/auth/whoami': {isAuthenticated: false}, '/api/auth/features': {canSendEmail: true}});
    expect(await (await page('/login')).text()).toContain('Forgot your password?');

    api({'/api/auth/whoami': {isAuthenticated: false}, '/api/auth/features': {canSendEmail: false}});
    expect(await (await page('/login')).text()).not.toContain('Forgot your password?');
  });

  it('renders the forgot-password page with whether email can be sent', async () => {
    api({'/api/auth/features': {canSendEmail: true}});
    expect(await (await page('/forgot-password')).text()).toContain('Send reset link');

    api({'/api/auth/features': {canSendEmail: false}});
    expect(await (await page('/forgot-password')).text()).toContain('Password reset by email isn&#x27;t available here.');
  });

  it.each([
    ['/reset-password', 'Choose a new password'],
    ['/confirm-email', 'Confirm your email address'],
  ])("renders %s waiting to read the link, which the server never sees", async (path, title) => {
    const requests = api({});

    const response = await page(path);
    const html = await response.text();

    expect(response.status).toBe(200);
    expect(html).toContain(title);
    expect(html).toContain('Reading the link');
    expect(html).not.toContain('Change Password');
    expect(html).not.toContain('Confirm Email Address');
    expect(requests).toEqual([]);
  });

  it('sends an admin who must set up two-factor to do that, without asking for what the API would refuse', async () => {
    const requests = api({'/api/auth/whoami': {...signedInAdmin, mustSetUpTwoFactor: true}});

    const response = await page('/patients', 'medapp_session=abc');

    expect(response.status).toBe(307);
    expect(response.headers.get('location')).toBe('/set-up-two-factor');
    expect(requests.map((r) => new URL(r.url).pathname)).toEqual(['/api/auth/whoami']);
  });

  it("renders the two-factor set-up page in the account's settings, making no key on the server", async () => {
    const requests = api({
      '/api/auth/whoami': {...signedInAdmin, mustSetUpTwoFactor: true},
      '/api/auth/me/settings': {settings: JSON.stringify({'medapp.colorMode': 'light'})},
      '/api/auth/me/two-factor': {enabled: true, method: 'Email', recoveryCodesLeft: 10, required: true},
    });

    const response = await page('/set-up-two-factor', 'medapp_session=abc');
    const html = await response.text();

    expect(response.status).toBe(200);
    expect(html).toMatch(/^<!DOCTYPE html><html lang="en-GB" data-theme="light"/);
    expect(html).toContain('Set up two-factor authentication');
    // Why codes by email aren't enough, rendered with the page.
    expect(html).toContain('You sign in with codes by email');
    expect(requests.map((r) => new URL(r.url).pathname).sort()).toEqual(['/api/auth/me/settings', '/api/auth/me/two-factor', '/api/auth/whoami']);
  });

  it('renders Account Settings, Security with its two-factor state', async () => {
    api({
      '/api/auth/whoami': signedIn,
      '/api/auth/me/settings': {settings: null},
      '/api/notifications': {items: [], unreadCount: 0},
      '/api/auth/me/two-factor': {enabled: true, recoveryCodesLeft: 7, required: false},
    });

    const html = await (await page('/account/security', 'medapp_session=abc')).text();

    expect(html).toContain('Recovery codes left');
    expect(html).toContain('New Recovery Codes');
  });

  it("renders the notepad's address on its own as just a spinner, in the account's settings, without reading the notepad", async () => {
    const requests = api({
      '/api/auth/whoami': signedIn,
      '/api/auth/me/settings': {settings: JSON.stringify({'medapp.colorMode': 'light'})},
    });

    const response = await page('/notepad', 'medapp_session=abc');
    const html = await response.text();

    // The browser then goes to the start page with the notepad docked (it can't be known here).
    expect(response.status).toBe(200);
    expect(html).toMatch(/^<!DOCTYPE html><html lang="en-GB" data-theme="light"/);
    expect(html).toContain('Opening the notepad...');
    expect(html).not.toContain('aria-label="Notepad"');
    expect(html).not.toContain('My Worklist');
    expect(requests.map((r) => new URL(r.url).pathname)).not.toContain('/api/me/notepad');
  });

  it("sends someone who must change their password from the notepad's address to do that", async () => {
    api({'/api/auth/whoami': {...signedIn, mustChangePassword: true}});

    const response = await page('/notepad', 'medapp_session=abc');

    expect(response.status).toBe(307);
    expect(response.headers.get('location')).toBe('/change-password');
  });

  it('renders Account Settings, Email Address with the address', async () => {
    api({
      '/api/auth/whoami': signedIn,
      '/api/auth/me/settings': {settings: null},
      '/api/notifications': {items: [], unreadCount: 0},
      '/api/auth/me/email': {email: 'test.user@example.org', pendingEmail: null},
      '/api/auth/features': {canSendEmail: true},
    });

    const html = await (await page('/account/email', 'medapp_session=abc')).text();

    expect(html).toContain('test.user@example.org');
    expect(html).toContain('Send confirmation link');
  });

  it('renders Admin Settings for an admin with the settings', async () => {
    api({
      '/api/auth/whoami': signedInAdmin,
      '/api/auth/me/settings': {settings: null},
      '/api/notifications': {items: [], unreadCount: 0},
      '/api/auth/security-settings': {requireTwoFactorForAdmins: true, adminsWithoutTwoFactor: 3},
    });

    const html = await (await page('/admin/settings', 'medapp_session=abc')).text();

    expect(html).toContain("3 admin accounts haven&#x27;t set up an authenticator app. They&#x27;ll be asked to the next time they use MedApp.");
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

  it("renders a patient's export for an admin with it in the page, asking for it (and so recording it) once", async () => {
    const requests = api({
      '/api/auth/whoami': signedInAdmin,
      '/api/auth/me/settings': {settings: null},
      '/api/notifications': {items: [], unreadCount: 0},
      [`/api/patients/${patient.id}/export`]: {exportedAtUtc: '2026-11-16T09:30:00Z', exportedBy: 'test-admin', patient},
    });

    const response = await page(`/patients/${patient.id}/export`, 'medapp_session=abc');
    const html = await response.text();

    expect(response.status).toBe(200);
    expect(html).toContain('Patient Record Export');
    expect(html).toContain('test.patient@example.org');
    expect(requests.filter((r) => new URL(r.url).pathname.endsWith('/export'))).toHaveLength(1);
  });

  it("renders a patient's page for an admin with their access report, and for staff without asking for it", async () => {
    const routes = {
      '/api/auth/me/settings': {settings: null},
      '/api/notifications': {items: [], unreadCount: 0},
      [`/api/patients/${patient.id}`]: patient,
      '/api/activity/search': {
        items: [{id: 'e1', action: 'PatientViewed', username: 'access.checker', atUtc: '2026-11-15T10:00:00Z', details: '{}'}],
        page: 1,
        pageSize: 10,
        totalCount: 1,
        totalPages: 1,
      },
    };
    api({...routes, '/api/auth/whoami': signedInAdmin});
    expect(await (await page(`/patients/${patient.id}`, 'medapp_session=abc')).text()).toContain('access.checker');

    const requests = api({...routes, '/api/auth/whoami': signedIn});
    const html = await (await page(`/patients/${patient.id}`, 'medapp_session=abc')).text();
    expect(html).toContain('test.patient@example.org');
    expect(html).not.toContain('access.checker');
    expect(requests.some((r) => new URL(r.url).pathname === '/api/activity/search')).toBe(false);
  });

  it('renders the activity log for an admin', async () => {
    api({
      '/api/auth/whoami': signedInAdmin,
      '/api/auth/me/settings': {settings: null},
      '/api/notifications': {items: [], unreadCount: 0},
      '/api/activity/search': {
        items: [{id: 'e1', action: 'SignInFailed', username: 'synthetic.tryer', ipAddress: '192.0.2.44', atUtc: '2026-11-15T10:00:00Z', details: '{}'}],
        page: 1,
        pageSize: 10,
        totalCount: 1,
        totalPages: 1,
      },
    });

    const html = await (await page('/admin/activity', 'medapp_session=abc')).text();

    expect(html).toContain('Activity Log');
    expect(html).toContain('192.0.2.44');
  });

  it("renders a patient's page with their files, the first page of their timeline and what's due", async () => {
    const requests = api({
      '/api/attachments': fileList({}),
      '/api/auth/whoami': signedIn,
      '/api/auth/me/settings': {settings: null},
      '/api/notifications': {items: [], unreadCount: 0},
      [`/api/patients/${patient.id}`]: patient,
      [`/api/patients/${patient.id}/timeline/overview`]: {
        upcoming: [{pathwayId: '0000000d-0000-0000-0000-000000000002', title: 'Knee assessment', status: 'Assigned', dueDate: '2026-06-30', isOverdue: false}],
        openCount: 1,
        pathways: [],
      },
      [`/api/patients/${patient.id}/timeline`]: {
        items: [
          {
            id: '0000000d-0000-0000-0000-000000000003',
            kind: 'Stages',
            action: 'StageFinished',
            atUtc: '2026-06-15T09:00:00Z',
            username: 'test.staff',
            pathwayId: '0000000d-0000-0000-0000-000000000002',
            pathwayTitle: 'Knee assessment',
            stageName: 'Consultation',
            changes: [],
          },
          {
            id: '0000000d-0000-0000-0000-000000000004',
            kind: 'Appointments',
            action: 'AppointmentBooked',
            atUtc: '2026-06-15T08:00:00Z',
            username: 'test.staff',
            pathwayId: null,
            pathwayTitle: null,
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
    expect(html).toContain('referral-letter.pdf');
    expect(html).toContain('Added by test.staff, 15 Jun 2026, 10:00');
    const files = requests.find((r) => new URL(r.url).pathname === '/api/attachments')!;
    expect(new URL(files.url).search).toBe(`?PatientId=${patient.id}`);
    const feed = requests.find((r) => new URL(r.url).pathname === `/api/patients/${patient.id}/timeline`)!;
    expect(new URL(feed.url).search).toBe('?PageSize=20');
  });

  it("renders someone's calendar templates on the tab in the URL, with their data", async () => {
    const owner = '0000000d-0000-0000-0000-0000000000b2';
    const requests = api({
      '/api/auth/whoami': signedInAdmin,
      '/api/auth/me/settings': {settings: null},
      '/api/notifications': {items: [], unreadCount: 0},
      '/api/calendar/staff': [{userId: '0000000d-0000-0000-0000-0000000000a9', username: 'test-admin'}, {userId: owner, username: 'b.staff'}],
      [`/api/calendar/staff/${owner}/week-templates`]: [
        {
          id: '0000000d-0000-0000-0000-000000000010',
          ownerUserId: owner,
          name: 'Full time',
          isArchived: false,
          staffCount: 1,
          canEdit: true,
          days: ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'].map((dayOfWeek) => ({dayOfWeek, dayTemplateId: 'd', dayTemplateName: 'Standard day'})),
        },
      ],
    });

    const html = await (await page(`/calendar/templates?tab=week&staff=${owner}`, 'medapp_session=abc')).text();

    expect(html).toContain("b.staff&#x27;s calendar templates");
    expect(html).toContain('Full time');
    expect(html).toContain('Mon to Fri: Standard day');
    expect(html).toContain('1 active week template');
    expect(requests.some((r) => new URL(r.url).pathname.endsWith('/day-templates'))).toBe(false);
  });

  it('renders your own calendar templates, finding your ID in the staff list', async () => {
    const me = '0000000d-0000-0000-0000-0000000000a1';
    api({
      '/api/auth/whoami': signedIn,
      '/api/auth/me/settings': {settings: null},
      '/api/notifications': {items: [], unreadCount: 0},
      '/api/calendar/staff': [{userId: me, username: 'test-user'}],
      [`/api/calendar/staff/${me}/day-templates`]: [{id: '0000000d-0000-0000-0000-000000000011', ownerUserId: me, name: 'Clinic day', canEdit: true, blocks: []}],
    });

    const html = await (await page('/calendar/templates', 'medapp_session=abc')).text();

    expect(html).toContain('My calendar templates');
    expect(html).toContain('Clinic day');
  });

  it("renders a week template with its owner's day templates", async () => {
    const owner = '0000000d-0000-0000-0000-0000000000b2';
    const id = '0000000d-0000-0000-0000-000000000010';
    const day = '0000000d-0000-0000-0000-000000000011';
    const requests = api({
      '/api/auth/whoami': signedInAdmin,
      '/api/auth/me/settings': {settings: null},
      '/api/notifications': {items: [], unreadCount: 0},
      '/api/calendar/staff': [{userId: '0000000d-0000-0000-0000-0000000000a9', username: 'test-admin'}, {userId: owner, username: 'b.staff'}],
      [`/api/calendar/week-templates/${id}`]: {
        id,
        ownerUserId: owner,
        name: 'Full time',
        canEdit: true,
        days: [{dayOfWeek: 'Monday', dayTemplateId: day, dayTemplateName: 'Clinic day'}],
      },
      [`/api/calendar/staff/${owner}/day-templates`]: [
        {id: day, ownerUserId: owner, name: 'Clinic day', blocks: [{id: 'b', activityName: 'Lunch', startTime: '12:00:00', endTime: '13:00:00'}]},
      ],
    });

    const html = await (await page(`/calendar/templates/week/${id}`, 'medapp_session=abc')).text();

    expect(html).toContain('Edit Week Template');
    expect(html).toContain('Lunch 12:00 to 13:00');
    expect(requests.some((r) => new URL(r.url).pathname === `/api/calendar/staff/${owner}/day-templates`)).toBe(true);
  });

  it('sends the old admin Calendar Templates page to calendar templates, on the same tab', async () => {
    api({
      '/api/auth/whoami': signedInAdmin,
      '/api/auth/me/settings': {settings: null},
      '/api/notifications': {items: [], unreadCount: 0},
    });

    const response = await page('/admin/calendar-templates?tab=staff', 'medapp_session=abc');

    expect(response.status).toBe(307);
    expect(response.headers.get('location')).toBe('/calendar/templates?tab=default');
    const plain = await page('/admin/calendar-templates', 'medapp_session=abc');
    expect(plain.headers.get('location')).toBe('/calendar/templates');
  });

  it("sends Requires Appointment's old address on to its new one, keeping its search", async () => {
    api({
      '/api/auth/whoami': signedIn,
      '/api/auth/me/settings': {settings: null},
      '/api/notifications': {items: [], unreadCount: 0},
    });

    const response = await page('/calendar/needs-booking?page=2&patient=Synthetic', 'medapp_session=abc');

    expect(response.status).toBe(307);
    expect(response.headers.get('location')).toBe('/calendar/requires-appointment?page=2&patient=Synthetic');
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
    expect(html).toContain('Requires Appointment (2)');
    // Apply Template, on a calendar they can change; its day templates are read only in the browser.
    expect(html).toMatch(/<button(?:(?!<\/button>).)*Apply Template/);
    expect(requests.some((r) => new URL(r.url).pathname.endsWith('/day-templates'))).toBe(false);
    expect(html).not.toContain('class="mc-now"');
    const calendar = requests.find((r) => new URL(r.url).pathname === '/api/calendar')!;
    expect(new URL(calendar.url).search).toBe('?From=2026-11-23&To=2026-11-29');
  });

  it("renders an admin's team view: everyone free first, then each person's day", async () => {
    vi.useFakeTimers({toFake: ['Date'], now: new Date('2026-11-23T07:00:00Z')});
    const member = (staff: {userId: string; username: string}) =>
      testCalendar({from: '2026-11-24', dayCount: 1, today: '2026-11-23', staff, meetings: [testMeeting({date: '2026-11-24'})]});
    const requests = api({
      '/api/auth/whoami': signedInAdmin,
      '/api/auth/me/settings': {settings: null},
      '/api/notifications': {items: [], unreadCount: 0},
      '/api/calendar/staff': [{userId: '0000000d-0000-0000-0000-0000000000a9', username: 'test-admin'}, STAFF_ME, STAFF_OTHER],
      '/api/appointments/search': {items: [], totalCount: 0},
      '/api/calendar/team': {
        date: '2026-11-24',
        day: member(STAFF_ME).days![0],
        members: [member(STAFF_OTHER), member(STAFF_ME)],
        everyoneFree: [{startUtc: '2026-11-24T10:00:00Z', endUtc: '2026-11-24T11:00:00Z'}],
      },
    });

    const html = await (await page('/calendar?view=team&date=2026-11-24', 'medapp_session=abc')).text();

    expect(html).toContain('Everyone free');
    expect(html).toContain(STAFF_ME.username);
    expect(html).toContain(STAFF_OTHER.username);
    expect(html).toContain('Team huddle');
    expect(html).not.toContain('Apply Template');
    const team = requests.find((r) => new URL(r.url).pathname === '/api/calendar/team')!;
    expect(new URL(team.url).searchParams.get('Date')).toBe('2026-11-24');
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

  it("renders a pathway's page with its own files", async () => {
    const pathwayId = '0000000d-0000-0000-0000-000000000002';
    const requests = api({
      '/api/auth/whoami': signedIn,
      '/api/auth/me/settings': {settings: null},
      '/api/notifications': {items: [], unreadCount: 0},
      [`/api/pathways/patient-pathways/${pathwayId}`]: {
        id: pathwayId,
        patientId: patient.id,
        patientName: 'Test Patient',
        title: 'Knee assessment',
        status: 'InProgress',
        priority: 'Low',
        dueDate: '2026-06-30',
        canEditDetails: true,
        canWorkOnStages: true,
        stages: [],
        assignments: [],
      },
      '/api/attachments': fileList({attachedTo: 'Pathway', pathwayId, pathwayTitle: 'Knee assessment', fileName: 'consent-form.pdf'}),
    });

    const html = await (await page(`/pathways/${pathwayId}`, 'medapp_session=abc')).text();

    expect(html).toContain('Knee assessment');
    expect(html).toContain('consent-form.pdf');
    const files = requests.find((r) => new URL(r.url).pathname === '/api/attachments')!;
    expect(new URL(files.url).search).toBe(`?PathwayId=${pathwayId}`);
  });

  it("renders the patient's banner on the server, with their NHS number", async () => {
    api({
      '/api/auth/whoami': signedIn,
      '/api/auth/me/settings': {settings: null},
      '/api/notifications': {items: [], unreadCount: 0},
      // A synthetic test number (the 999 range is never issued to real patients).
      [`/api/patients/${patient.id}`]: {...patient, nhsNumber: '9990000018'},
    });

    const html = await (await page(`/patients/${patient.id}`, 'medapp_session=abc')).text();

    // Named for the patient, by its hidden "Patient:" and the name, whose family name is in capitals by CSS only.
    expect(html).toMatch(/role="region" aria-labelledby="[^"]+ [^"]+"/);
    expect(html).toContain('>Patient:<');
    expect(html).toMatch(/text-transform:uppercase[^>]*>Patient<\/span>, Test/);
    expect(html).toMatch(/NHS No\. (<!-- -->)?999 000 0018/);
  });

  it("renders a restricted record's short label on the server, with the whole message for screen readers, closed", async () => {
    api({
      '/api/auth/whoami': signedIn,
      '/api/auth/me/settings': {settings: null},
      '/api/notifications': {items: [], unreadCount: 0},
      [`/api/patients/${patient.id}`]: {...patient, isRestricted: true, restrictionReason: 'Synthetic accuracy check'},
    });

    const html = await (await page(`/patients/${patient.id}`, 'medapp_session=abc')).text();

    expect(html).toContain('aria-label="Restricted record: changes paused, show details"');
    expect(html).toContain('aria-expanded="false"');
    // The whole message is in the page (visually hidden), but its overlay isn't open.
    expect(html).toMatch(/Restricted record: nothing about this patient[^<]*Reason: Synthetic accuracy check/);
    // Only that message is the alert, not the label or the overlay.
    expect(html).toMatch(/role="alert"[^>]*>Restricted record: nothing about this patient/);
    expect(html).not.toContain('Restricted record: details');
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

  it("renders Insights for an admin with its numbers and summaries; its charts draw once it's in a browser", async () => {
    api({
      '/api/auth/whoami': signedInAdmin,
      '/api/auth/me/settings': {settings: null},
      '/api/notifications': {items: [], unreadCount: 0},
      '/api/pathways/insights': insights,
      '/api/pathways/patient-pathway-stage/templates': templates,
    });

    const response = await page('/admin/insights', 'medapp_session=abc');
    const html = await response.text();

    expect(response.status).toBe(200);
    expect(html).toContain('Completed this week');
    expect(html).toContain('5 pathways are overdue. k.ashford has the most (3). 2 have nobody assigned.');
    expect(html).toContain('role="img" aria-label="Bar chart. Of 5 overdue, 1 is urgent and 2 high priority."');
    expect(html).not.toContain('recharts-surface');
  });

  it('renders Closed Days for an admin with the year’s list in it', async () => {
    const requests = api({
      '/api/auth/whoami': signedInAdmin,
      '/api/auth/me/settings': {settings: null},
      '/api/notifications': {items: [], unreadCount: 0},
      '/api/calendar/closed-days': [{id: '0000003c-0000-0000-0000-000000000001', date: '2027-12-27', name: 'Christmas Day', isPast: false}],
    });

    const response = await page('/admin/closed-days?year=2027', 'medapp_session=abc');
    const html = await response.text();

    expect(response.status).toBe(200);
    expect(html).toContain('Christmas Day');
    expect(html).toContain('Mon 27 Dec 2027');
    expect(requests.map((r) => new URL(r.url).search).filter((q) => q.includes('From'))).toEqual(['?From=2027-01-01&To=2027-12-31']);
  });

  it('renders Sites and Rooms for an admin with the sites in it', async () => {
    const requests = api({
      '/api/auth/whoami': signedInAdmin,
      '/api/auth/me/settings': {settings: null},
      '/api/notifications': {items: [], unreadCount: 0},
      '/api/sites': [
        {
          id: '5173e000-0000-4000-8000-000000000002',
          name: 'Northgate outreach clinic',
          addressLines: ['1 Northgate Road'],
          postcode: 'ZZ99 3AA',
          phone: '07700 900123',
          isArchived: false,
          canBeDeleted: false,
          rooms: [{id: '0000005a-0000-0000-0000-000000000001', name: 'Clinic room 1', isArchived: false}],
        },
      ],
    });

    const response = await page('/admin/sites', 'medapp_session=abc');
    const html = await response.text();

    expect(response.status).toBe(200);
    expect(html).toContain('Northgate outreach clinic');
    expect(html).toContain('ZZ99 3AA');
    expect(html).toContain('Clinic room 1');
    expect(requests.map((r) => new URL(r.url).search).filter((q) => q.includes('includeArchived'))).toEqual(['?includeArchived=true']);
  });
});
