// @vitest-environment node
import {render, type RouterManifest} from './entry-server';
import {insights, templates} from './test/insights';

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
const signedInAdmin = {isAuthenticated: true, name: 'test-admin', roles: ['Admin', 'User']};
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
    expect(html).not.toContain('Change password');
    expect(html).not.toContain('Confirm email address');
    expect(requests).toEqual([]);
  });

  it('sends an admin who must set up two-factor to do that, without asking for what the API would refuse', async () => {
    const requests = api({'/api/auth/whoami': {...signedIn, roles: ['Admin', 'User'], mustSetUpTwoFactor: true}});

    const response = await page('/patients', 'medapp_session=abc');

    expect(response.status).toBe(307);
    expect(response.headers.get('location')).toBe('/set-up-two-factor');
    expect(requests.map((r) => new URL(r.url).pathname)).toEqual(['/api/auth/whoami']);
  });

  it("renders the two-factor set-up page in the account's settings, making no key on the server", async () => {
    const requests = api({
      '/api/auth/whoami': {...signedIn, roles: ['Admin', 'User'], mustSetUpTwoFactor: true},
      '/api/auth/me/settings': {settings: JSON.stringify({'medapp.colorMode': 'light'})},
    });

    const response = await page('/set-up-two-factor', 'medapp_session=abc');
    const html = await response.text();

    expect(response.status).toBe(200);
    expect(html).toMatch(/^<!DOCTYPE html><html lang="en-GB" data-theme="light"/);
    expect(html).toContain('Set up two-factor authentication');
    expect(requests.map((r) => new URL(r.url).pathname)).toEqual(['/api/auth/whoami', '/api/auth/me/settings']);
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
      '/api/auth/whoami': {isAuthenticated: true, name: 'test-admin', roles: ['Admin', 'User']},
      '/api/auth/me/settings': {settings: null},
      '/api/notifications': {items: [], unreadCount: 0},
      '/api/auth/security-settings': {requireTwoFactorForAdmins: true, adminsWithoutTwoFactor: 3},
    });

    const html = await (await page('/admin/settings', 'medapp_session=abc')).text();

    expect(html).toContain("3 admin accounts haven&#x27;t set it up; they&#x27;ll be asked to at their next request.");
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
        ],
        nextCursor: 'older',
      },
    });

    const html = await (await page(`/patients/${patient.id}`, 'medapp_session=abc')).text();

    expect(html).toContain('test.patient@example.org');
    expect(html).toContain('Finished Consultation');
    expect(html).toContain('Monday 15 June 2026');
    expect(html).toContain('Due 30 Jun 2026');
    expect(html).toContain('Load More');
    const feed = requests.find((r) => new URL(r.url).pathname === `/api/patients/${patient.id}/timeline`)!;
    expect(new URL(feed.url).search).toBe('?PageSize=20');
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
      '/api/auth/whoami': {isAuthenticated: true, name: 'test-admin', roles: ['Admin', 'User']},
      '/api/auth/me/settings': {settings: null},
      '/api/notifications': {items: [], unreadCount: 0},
      '/api/procedures/insights': insights,
      '/api/procedures/patient-procedure-stage/templates': templates,
    });

    const response = await page('/admin/insights', 'medapp_session=abc');
    const html = await response.text();

    expect(response.status).toBe(200);
    expect(html).toContain('Completed this week');
    expect(html).toContain('5 procedures are overdue. k.ashford has the most (3). 2 have nobody assigned.');
    expect(html).toContain('role="img" aria-label="Bar chart. Of 5 overdue, 1 is urgent and 2 high priority."');
    expect(html).not.toContain('recharts-surface');
  });
});
