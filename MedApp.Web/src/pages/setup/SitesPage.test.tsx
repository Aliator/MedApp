import {screen, waitFor, within} from '@testing-library/react';
import {adminUser, calls, expectAnnounced, isDisabled, json, mockApi, openDialog, renderWithApp, withAnnouncements} from '../../test/render';
import type {SetupListSearch} from '../listSearch';
import {roomsWords, siteForm, siteRequest, SitesPage} from './SitesPage';

/** Synthetic sites and rooms: a main site with a used room and an archived one, an unused clinic, and an archived annexe. */
const ROOM_1 = {id: '0000005a-0000-0000-0000-000000000001', siteId: 'main', siteName: 'Main site', name: 'Clinic room 1', isArchived: false, canBeDeleted: false};
const ROOM_2 = {id: '0000005a-0000-0000-0000-000000000002', siteId: 'main', siteName: 'Main site', name: 'Clinic room 2', isArchived: false, canBeDeleted: true};
const OLD_ROOM = {id: '0000005a-0000-0000-0000-000000000003', siteId: 'main', siteName: 'Main site', name: 'Old store', isArchived: true, canBeDeleted: false};
const MAIN = {
  id: 'main',
  name: 'Main site',
  addressLines: ['1 Example Street', 'Testville'],
  postcode: 'ZZ99 1AA',
  phone: '07700 900001',
  isArchived: false,
  canBeDeleted: false,
  rooms: [ROOM_1, ROOM_2, OLD_ROOM],
};
const NORTHGATE = {
  id: 'northgate',
  name: 'Northgate outreach clinic',
  addressLines: [],
  postcode: null,
  phone: null,
  isArchived: false,
  canBeDeleted: true,
  rooms: [],
};
const ANNEXE = {id: 'annexe', name: 'Old annexe', addressLines: [], postcode: null, phone: null, isArchived: true, canBeDeleted: false, rooms: []};

function renderPage(search: SetupListSearch = {}, extra: Parameters<typeof mockApi>[0] = {}) {
  const fetch = mockApi({'GET /api/sites': () => json([MAIN, NORTHGATE, ANNEXE]), ...extra});
  const onSearchChange = vi.fn();
  return {fetch, onSearchChange, ...renderWithApp(<SitesPage search={search} onSearchChange={onSearchChange} />, {user: adminUser})};
}

const rowOf = async (name: string) => (await screen.findByText(name)).closest('tr')!;
const dialog = () => within(openDialog()!);
const sent = (fetch: ReturnType<typeof mockApi>, method: string) =>
  fetch.mock.calls
    .map(([r]) => r as Request)
    .filter((r) => r.method === method)
    .at(-1)!
    .clone()
    .json();
const roomItem = (name: string) => dialog().getByText(name).closest('li')!;

// Its dialogs are typed into a lot, which is slow under coverage on CI's runners.
describe('SitesPage', {timeout: 15_000}, () => {
  it('lists the open sites by name, with their addresses, phones and rooms', async () => {
    const {fetch} = renderPage();

    const main = await rowOf('Main site');
    expect(within(main).getByText('1 Example Street, Testville')).toBeInTheDocument();
    expect(within(main).getByText('ZZ99 1AA')).toBeInTheDocument();
    expect(within(main).getByText('07700 900001')).toBeInTheDocument();
    expect(within(main).getByText('Clinic room 1, Clinic room 2 (1 archived)')).toBeInTheDocument();
    expect(within(main).getByText('Active')).toBeInTheDocument();
    expect(within(await rowOf('Northgate outreach clinic')).getByText('No rooms')).toBeInTheDocument();
    expect(screen.queryByText('Old annexe')).not.toBeInTheDocument();
    expect(screen.getByText('2 active sites')).toBeInTheDocument();
    expect(calls(fetch)).toEqual(['GET /api/sites?includeArchived=true']);
  });

  it('says when no site matches, offering archived ones too', async () => {
    const {user, onSearchChange} = renderPage({name: 'nowhere'});
    expect(await screen.findByText('No sites match your search')).toBeInTheDocument();
    await user.click(screen.getByRole('button', {name: 'Show Archived'}));
    expect(onSearchChange).toHaveBeenCalledWith({name: 'nowhere', page: undefined, status: 'any'});
  });

  it('says so when the sites could not be loaded', async () => {
    renderPage({}, {'GET /api/sites': () => json({title: 'Server error'}, 500)});
    expect(await screen.findByText('The sites could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it("adds a site, leaving out what isn't filled in, and shows the API's errors on the fields", async () => {
    withAnnouncements();
    let attempt = 0;
    const {user, fetch} = renderPage(
      {},
      {
        'POST /api/sites': () =>
          ++attempt === 1
            ? json({title: 'Validation failed', status: 400, errors: {Postcode: ['Postcode must be a UK postcode, such as ZZ99 3AA.']}}, 400)
            : json({...NORTHGATE, id: 'eastfield', name: 'Eastfield clinic'}, 201),
      },
    );

    await user.click(await screen.findByRole('button', {name: 'Add Site'}));
    expect(isDisabled(dialog().getByRole('button', {name: 'Add Site'}))).toBe(true);
    await user.type(dialog().getByRole('textbox', {name: /^Name/}), 'Eastfield clinic');
    await user.type(dialog().getByRole('textbox', {name: /Address line 1/}), '2 Eastfield Lane');
    await user.type(dialog().getByRole('textbox', {name: /Address line 3/}), 'Testville');
    await user.type(dialog().getByRole('textbox', {name: /Postcode/}), 'nope');
    await user.type(dialog().getByRole('textbox', {name: /Phone/}), '07700 900002');
    await user.click(dialog().getByRole('button', {name: 'Add Site'}));

    expect((await dialog().findAllByText('Postcode must be a UK postcode, such as ZZ99 3AA.')).length).toBeGreaterThan(0);
    expect(await sent(fetch, 'POST')).toEqual({
      name: 'Eastfield clinic',
      addressLines: ['2 Eastfield Lane', 'Testville'],
      postcode: 'nope',
      phone: '07700 900002',
    });
    await user.clear(dialog().getByRole('textbox', {name: /Postcode/}));
    await user.type(dialog().getByRole('textbox', {name: /Postcode/}), 'ZZ99 3AA');
    await user.click(dialog().getByRole('button', {name: 'Add Site'}));

    await waitFor(() => expect(openDialog()).toBeNull());
    await expectAnnounced('Site added');
  });

  it("edits a site from what it is, removing what's cleared, and shows a 409 in the API's words", async () => {
    let attempt = 0;
    const {user, fetch} = renderPage(
      {},
      {
        'PUT /api/sites/main': () =>
          ++attempt === 1 ? json({title: "A site called 'Northgate outreach clinic' already exists."}, 409) : json(MAIN),
      },
    );

    await user.click(within(await rowOf('Main site')).getByRole('button', {name: 'Edit site'}));
    expect(dialog().getByRole('textbox', {name: /^Name/})).toHaveValue('Main site');
    expect(dialog().getByRole('textbox', {name: /Address line 2/})).toHaveValue('Testville');
    expect(dialog().getByRole('textbox', {name: /Postcode/})).toHaveValue('ZZ99 1AA');
    await user.clear(dialog().getByRole('textbox', {name: /Phone/}));
    await user.click(dialog().getByRole('button', {name: 'Save'}));

    expect(await dialog().findByText("A site called 'Northgate outreach clinic' already exists.")).toBeInTheDocument();
    expect(await sent(fetch, 'PUT')).toEqual({name: 'Main site', addressLines: ['1 Example Street', 'Testville'], postcode: 'ZZ99 1AA', phone: null});
    await user.click(dialog().getByRole('button', {name: 'Save'}));
    await waitFor(() => expect(openDialog()).toBeNull());
  });

  it("archives a site after asking, and shows the API's reason when it can't", async () => {
    withAnnouncements();
    let attempt = 0;
    const {user, fetch} = renderPage(
      {},
      {
        'POST /api/sites/main/archive': () =>
          ++attempt === 1
            ? json({title: "Main site has rooms that aren't archived (Clinic room 1, Clinic room 2). Archive them first."}, 409)
            : json({...MAIN, isArchived: true}),
      },
    );

    await user.click(within(await rowOf('Main site')).getByRole('button', {name: 'Archive site'}));
    expect(dialog().getByText('1 Example Street, Testville, ZZ99 1AA')).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Archive'}));
    expect(await screen.findByText("Main site has rooms that aren't archived (Clinic room 1, Clinic room 2). Archive them first.")).toBeInTheDocument();
    expect(openDialog()).toBeNull();

    await user.click(within(await rowOf('Main site')).getByRole('button', {name: 'Archive site'}));
    await user.click(dialog().getByRole('button', {name: 'Archive'}));
    await waitFor(() => expect(openDialog()).toBeNull());
    await expectAnnounced('Site archived');
    expect(calls(fetch).filter((c) => c === 'POST /api/sites/main/archive')).toHaveLength(2);
  });

  it('restores an archived site', async () => {
    withAnnouncements();
    const {user, fetch} = renderPage({status: 'any'}, {'POST /api/sites/annexe/restore': () => json({...ANNEXE, isArchived: false})});

    await user.click(within(await rowOf('Old annexe')).getByRole('button', {name: 'Restore site'}));

    await waitFor(() => expect(calls(fetch)).toContain('POST /api/sites/annexe/restore'));
    await expectAnnounced('Site restored');
  });

  it('deletes a site nothing has used, after asking, but never one that has been used', async () => {
    withAnnouncements();
    const {user, fetch} = renderPage({}, {'DELETE /api/sites/northgate': () => new Response(null, {status: 204})});

    const used = within(await rowOf('Main site')).getByRole('button', {name: "Has rooms or closed days, or is the only open site, so it can't be deleted"});
    expect(isDisabled(used)).toBe(true);
    await user.click(within(await rowOf('Northgate outreach clinic')).getByRole('button', {name: 'Delete site'}));
    await user.click(dialog().getByRole('button', {name: 'Delete Site'}));

    await waitFor(() => expect(calls(fetch)).toContain('DELETE /api/sites/northgate'));
    await waitFor(() => expect(openDialog()).toBeNull());
    await expectAnnounced('Site deleted');
  });

  it("lists a site's rooms, and adds and renames one, going back to the rooms each time", async () => {
    withAnnouncements();
    const {user, fetch} = renderPage(
      {},
      {
        'POST /api/rooms': () => json({...ROOM_1, id: 'new', name: 'Clinic room 3'}, 201),
        [`PUT /api/rooms/${ROOM_2.id}`]: () => json({...ROOM_2, name: 'Treatment room'}),
      },
    );

    await user.click(within(await rowOf('Main site')).getByRole('button', {name: 'Rooms'}));
    expect(dialog().getByRole('heading', {name: 'Rooms at Main site'})).toBeInTheDocument();
    expect(within(roomItem('Clinic room 1')).getByText('Active')).toBeInTheDocument();
    expect(within(roomItem('Old store')).getByText('Archived')).toBeInTheDocument();

    await user.click(dialog().getByRole('button', {name: 'Add Room'}));
    expect(dialog().getByText('At Main site. A room stays at its site.')).toBeInTheDocument();
    await user.type(dialog().getByRole('textbox', {name: /Name/}), 'Clinic room 3');
    await user.click(dialog().getByRole('button', {name: 'Add Room'}));
    expect(await dialog().findByRole('heading', {name: 'Rooms at Main site'})).toBeInTheDocument();
    expect(await sent(fetch, 'POST')).toEqual({siteId: 'main', name: 'Clinic room 3'});
    await expectAnnounced('Room added');

    await user.click(dialog().getByRole('button', {name: 'Rename Clinic room 2'}));
    expect(dialog().getByRole('textbox', {name: /Name/})).toHaveValue('Clinic room 2');
    await user.clear(dialog().getByRole('textbox', {name: /Name/}));
    await user.type(dialog().getByRole('textbox', {name: /Name/}), 'Treatment room');
    await user.click(dialog().getByRole('button', {name: 'Save'}));
    expect(await dialog().findByRole('heading', {name: 'Rooms at Main site'})).toBeInTheDocument();
    expect(await sent(fetch, 'PUT')).toEqual({name: 'Treatment room'});
    await expectAnnounced('Room renamed');

    await user.click(dialog().getByRole('button', {name: 'Done'}));
    await waitFor(() => expect(openDialog()).toBeNull());
  });

  it("shows a room's 409 in the API's words, and goes back to the rooms when a change is called off", async () => {
    const {user, fetch} = renderPage({}, {'POST /api/rooms': () => json({title: "Main site already has a room called 'Clinic room 1'."}, 409)});

    await user.click(within(await rowOf('Main site')).getByRole('button', {name: 'Rooms'}));
    await user.click(dialog().getByRole('button', {name: 'Add Room'}));
    await user.type(dialog().getByRole('textbox', {name: /Name/}), 'Clinic room 1');
    await user.click(dialog().getByRole('button', {name: 'Add Room'}));
    expect(await dialog().findByText("Main site already has a room called 'Clinic room 1'.")).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Cancel'}));

    expect(await dialog().findByRole('heading', {name: 'Rooms at Main site'})).toBeInTheDocument();
    expect(calls(fetch).filter((c) => c.startsWith('POST'))).toHaveLength(1);
  });

  it("archives and restores rooms at once, showing the API's reason when it can't", async () => {
    withAnnouncements();
    const {user} = renderPage(
      {},
      {
        [`POST /api/rooms/${ROOM_1.id}/archive`]: () => json({...ROOM_1, isArchived: true}),
        [`POST /api/rooms/${OLD_ROOM.id}/restore`]: () => json({title: 'Main site is archived. Restore it first.'}, 409),
      },
    );

    await user.click(within(await rowOf('Main site')).getByRole('button', {name: 'Rooms'}));
    await user.click(dialog().getByRole('button', {name: 'Archive Clinic room 1'}));
    await expectAnnounced('Clinic room 1 archived');
    await user.click(dialog().getByRole('button', {name: 'Restore Old store'}));
    expect(await dialog().findByText('Main site is archived. Restore it first.')).toBeInTheDocument();
  });

  it('deletes a room nothing has used, after asking, and never one that has been used', async () => {
    withAnnouncements();
    const {user, fetch} = renderPage({}, {[`DELETE /api/rooms/${ROOM_2.id}`]: () => new Response(null, {status: 204})});

    await user.click(within(await rowOf('Main site')).getByRole('button', {name: 'Rooms'}));
    expect(isDisabled(dialog().getByRole('button', {name: 'Clinic room 1 has been used, so it can be archived but not deleted'}))).toBe(true);
    await user.click(dialog().getByRole('button', {name: 'Delete Clinic room 2'}));
    expect(dialog().getByText('Clinic room 2')).toBeInTheDocument();
    await user.click(dialog().getByRole('button', {name: 'Delete Room'}));

    await waitFor(() => expect(calls(fetch)).toContain(`DELETE /api/rooms/${ROOM_2.id}`));
    expect(await dialog().findByRole('heading', {name: 'Rooms at Main site'})).toBeInTheDocument();
    await expectAnnounced('Clinic room 2 deleted');
  });

  it("goes back to the rooms with the API's reason when a room can't be deleted", async () => {
    const {user} = renderPage({}, {[`DELETE /api/rooms/${ROOM_2.id}`]: () => json({title: "Clinic room 2 has been used, so it can't be deleted. Archive it instead."}, 409)});

    await user.click(within(await rowOf('Main site')).getByRole('button', {name: 'Rooms'}));
    await user.click(dialog().getByRole('button', {name: 'Delete Clinic room 2'}));
    await user.click(dialog().getByRole('button', {name: 'Delete Room'}));

    expect(await dialog().findByText("Clinic room 2 has been used, so it can't be deleted. Archive it instead.")).toBeInTheDocument();
    expect(dialog().getByRole('heading', {name: 'Rooms at Main site'})).toBeInTheDocument();
  });

  it("can't add rooms to an archived site, and says why; a site with none says so", async () => {
    const {user} = renderPage({status: 'any'});

    await user.click(within(await rowOf('Old annexe')).getByRole('button', {name: 'Rooms'}));

    expect(dialog().getByText('No rooms yet.')).toBeInTheDocument();
    expect(dialog().getByText('The site is archived: restore it to add rooms or restore its rooms.')).toBeInTheDocument();
    expect(isDisabled(dialog().getByRole('button', {name: 'Add Room'}))).toBe(true);
  });
});

describe('site form and words', () => {
  it("starts from a site's details, three address lines whatever it has", () => {
    expect(siteForm(null)).toEqual({name: '', lines: ['', '', ''], postcode: '', phone: ''});
    expect(siteForm(MAIN)).toEqual({name: 'Main site', lines: ['1 Example Street', 'Testville', ''], postcode: 'ZZ99 1AA', phone: '07700 900001'});
    expect(siteForm({})).toEqual({name: '', lines: ['', '', ''], postcode: '', phone: ''});
  });

  it('sends the details trimmed, blank lines left out, and nothing as none', () => {
    expect(siteRequest({name: ' Eastfield ', lines: [' ', '2 Lane ', ''], postcode: ' ', phone: ''})).toEqual({
      name: 'Eastfield',
      addressLines: ['2 Lane'],
      postcode: null,
      phone: null,
    });
  });

  it("puts a site's rooms in words", () => {
    expect(roomsWords(MAIN)).toBe('Clinic room 1, Clinic room 2 (1 archived)');
    expect(roomsWords({rooms: [OLD_ROOM]})).toBe('No rooms (1 archived)');
    expect(roomsWords({})).toBe('No rooms');
  });

  describe('Opening hours', () => {
    const HOURS = {siteId: 'northgate', hasOwnHours: false, since: null, days: ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'].map((dayOfWeek) => ({dayOfWeek, opens: '08:00:00', closes: '18:00:00'}))};

    it("sets a site's evening and Saturday hours from the default", async () => {
      withAnnouncements();
      let saved: unknown;
      const {user} = renderPage(
        {},
        {
          'GET /api/sites/northgate/hours': () => json(HOURS),
          'PUT /api/sites/northgate/hours': async (request) => {
            saved = await request.json();
            return json({...HOURS, hasOwnHours: true});
          },
        },
      );

      await user.click(within(await rowOf('Northgate outreach clinic')).getByRole('button', {name: 'Opening hours'}));
      expect(dialog().getByRole('heading', {name: 'Opening Hours'})).toBeInTheDocument();
      const useDefault = await dialog().findByRole('switch', {name: 'Use the default hours: Monday to Friday, 08:00 to 18:00'});
      await waitFor(() => expect(useDefault).not.toBeDisabled());
      expect(useDefault).toBeChecked();
      await user.click(useDefault);
      const tuesday = within(dialog().getByRole('group', {name: 'Tuesday'}));
      await user.click(tuesday.getByRole('combobox', {name: 'Tuesday to'}));
      await user.click(screen.getByRole('option', {name: '20:00'}));
      await user.click(within(dialog().getByRole('group', {name: 'Saturday'})).getByRole('switch', {name: 'Open on Saturday'}));
      await user.click(dialog().getByRole('button', {name: 'Save'}));

      await expectAnnounced('Opening hours saved');
      expect(saved).toEqual({
        days: [
          {dayOfWeek: 'Monday', opens: '08:00:00', closes: '18:00:00'},
          {dayOfWeek: 'Tuesday', opens: '08:00:00', closes: '20:00:00'},
          {dayOfWeek: 'Wednesday', opens: '08:00:00', closes: '18:00:00'},
          {dayOfWeek: 'Thursday', opens: '08:00:00', closes: '18:00:00'},
          {dayOfWeek: 'Friday', opens: '08:00:00', closes: '18:00:00'},
          {dayOfWeek: 'Saturday', opens: '08:00:00', closes: '18:00:00'},
        ],
      });
    });

    it("shows the API's refusal (staff working there), and since when its own hours apply", async () => {
      let saved: unknown;
      const {user} = renderPage(
        {},
        {
          'GET /api/sites/main/hours': () =>
            json({siteId: 'main', hasOwnHours: true, since: '2026-11-02', days: [{dayOfWeek: 'Saturday', opens: '09:00:00', closes: '13:00:00'}]}),
          'PUT /api/sites/main/hours': async (request) => {
            saved = await request.json();
            return json({title: 'test-user works at Main site on Saturdays outside those hours. Change their hours first.'}, 409);
          },
        },
      );

      await user.click(within(await rowOf('Main site')).getByRole('button', {name: 'Opening hours'}));
      expect(await dialog().findByText('These hours since 2 November 2026.')).toBeInTheDocument();
      await user.click(dialog().getByRole('switch', {name: 'Use the default hours: Monday to Friday, 08:00 to 18:00'}));
      await user.click(dialog().getByRole('button', {name: 'Save'}));

      expect(await dialog().findByText('test-user works at Main site on Saturdays outside those hours. Change their hours first.')).toBeInTheDocument();
      expect(saved).toEqual({days: []});
    });

    it('says so when the hours could not be loaded, and offers none for an archived site', async () => {
      const {user} = renderPage({status: 'any'}, {'GET /api/sites/main/hours': () => json({title: 'Down'}, 500)});

      expect(within(await rowOf('Old annexe')).queryByRole('button', {name: 'Opening hours'})).not.toBeInTheDocument();
      await user.click(within(await rowOf('Main site')).getByRole('button', {name: 'Opening hours'}));
      expect(await dialog().findByText('The opening hours could not be loaded. Please try again.')).toBeInTheDocument();
      expect(isDisabled(dialog().getByRole('button', {name: 'Save'}))).toBe(true);
    });
  });
});
