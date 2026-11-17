import {QueryClient} from '@tanstack/react-query';
import {screen, waitFor} from '@testing-library/react';
import {staffQuery} from '../../api/calendar';
import {whoamiQuery} from '../../auth/auth';
import {adminUser, json, mockApi, renderWithApp, staffUser} from '../../test/render';
import {templatesOwnerId, useTemplatesOwner} from './calendarTemplatesOwner';

const ME = {userId: '00000000-0000-0000-0000-0000000000a1', username: 'test-user'};
const OTHER = {userId: '00000000-0000-0000-0000-0000000000b2', username: 'b.staff'};

function signedIn() {
  const queryClient = new QueryClient({defaultOptions: {queries: {retry: false}}});
  queryClient.setQueryData(whoamiQuery.queryKey, staffUser);
  return queryClient;
}

describe('templatesOwnerId', () => {
  afterEach(() => vi.unstubAllGlobals());

  it("is the URL's staff when there is one", async () => {
    const fetch = mockApi({});
    expect(await templatesOwnerId(signedIn(), OTHER.userId)).toBe(OTHER.userId);
    expect(fetch).not.toHaveBeenCalled();
  });

  it('in the browser, finds your own ID in the staff list only if it is already loaded', async () => {
    mockApi({});
    const queryClient = signedIn();
    expect(await templatesOwnerId(queryClient, undefined)).toBeUndefined();

    queryClient.setQueryData(staffQuery.queryKey, [OTHER, ME]);
    expect(await templatesOwnerId(queryClient, undefined)).toBe(ME.userId);
  });

  it('rendering on the server, waits for the staff list to find your own ID', async () => {
    vi.stubGlobal('window', undefined);
    mockApi({'GET /api/calendar/staff': () => json([OTHER, ME])});
    expect(await templatesOwnerId(signedIn(), undefined)).toBe(ME.userId);
  });

  it("rendering on the server, finds no one when the staff list can't be loaded", async () => {
    vi.stubGlobal('window', undefined);
    mockApi({'GET /api/calendar/staff': () => json({title: 'Down'}, 500)});
    expect(await templatesOwnerId(signedIn(), undefined)).toBeUndefined();
  });
});

function Owner({staff}: {staff?: string}) {
  const owner = useTemplatesOwner(staff);
  return <output>{[owner.title, owner.ownerId ?? 'none', owner.isOwn, owner.canEdit, owner.staffParam ?? 'mine', owner.ownerName].join(' | ')}</output>;
}

describe('useTemplatesOwner', () => {
  beforeEach(() => {
    mockApi({'GET /api/calendar/staff': () => json([ME, OTHER])});
  });

  it('is your own without a staff ID, or with your own', async () => {
    renderWithApp(<Owner />, {user: staffUser});
    expect(await screen.findByText(`My calendar templates | ${ME.userId} | true | true | mine | test-user`)).toBeInTheDocument();

    renderWithApp(<Owner staff={ME.userId} />, {user: staffUser});
    await waitFor(() => expect(screen.getAllByText(`My calendar templates | ${ME.userId} | true | true | mine | test-user`)).toHaveLength(2));
  });

  it("is someone else's, for staff to read and admins to change", async () => {
    renderWithApp(<Owner staff={OTHER.userId} />, {user: staffUser});
    expect(await screen.findByText(`b.staff's calendar templates | ${OTHER.userId} | false | false | ${OTHER.userId} | b.staff`)).toBeInTheDocument();

    renderWithApp(<Owner staff={OTHER.userId} />, {user: adminUser});
    expect(await screen.findByText(`b.staff's calendar templates | ${OTHER.userId} | false | true | ${OTHER.userId} | b.staff`)).toBeInTheDocument();
  });
});
