import {act, renderHook, waitFor} from '@testing-library/react';
import {addressNow, pinsNow, searchNow} from './beforePathways';
import {PINS_KEY, usePins} from '../components/navigation/pins';
import {parseActivitySearch, parseAuditSearch, parsePathwaysSearch, parseTasksSearch} from '../pages/listSearch';
import {adminUser, json, mockFetch, pageOf, renderApp, signedOutUser, staffUser} from '../test/render';

const ID = '0000000d-0000-4000-8000-000000000001';

describe('addressNow', () => {
  it.each([
    ['/procedures', '/pathways'],
    ['/procedures/', '/pathways/'],
    ['/procedures/new', '/pathways/new'],
    [`/procedures/${ID}`, `/pathways/${ID}`],
    [`/procedures/${ID}/edit`, `/pathways/${ID}/edit`],
    [`/patients/${ID}/procedures`, `/patients/${ID}/pathways`],
    ['/admin/templates', '/admin/pathway-templates'],
    ['/admin/templates/new', '/admin/pathway-templates/new'],
    [`/admin/templates/${ID}`, `/admin/pathway-templates/${ID}`],
  ])('gives %s its address now, %s', (old, now) => {
    expect(addressNow(old)).toBe(now);
  });

  it.each(['/', '/pathways', '/patients', `/patients/${ID}`, '/admin/calendar-templates', '/calendar/templates', '/procedures-old', '/admin/templates-old'])(
    'leaves %s alone',
    (path) => {
      expect(addressNow(path)).toBeUndefined();
    },
  );
});

describe('searchNow', () => {
  it('renames the procedure of an audit log search, a sort by the Procedure column and the Procedure viewed action', () => {
    expect(searchNow({procedureId: ID, sort: 'Procedure', action: '!ProcedureViewed,PatientViewed', page: 2})).toEqual({
      pathwayId: ID,
      sort: 'Pathway',
      action: '!PathwayViewed,PatientViewed',
      page: 2,
    });
  });

  it('keeps everything else as it was, a search for the word included', () => {
    const search = {title: 'Procedure', q: 'ProcedureViewed', sort: 'Due', pathwayId: ID, asc: false};

    expect(searchNow(search)).toEqual(search);
  });

  it('prefers the name used now when both are there', () => {
    expect(searchNow({pathwayId: 'now', procedureId: 'old'})).toEqual({pathwayId: 'now'});
  });

  it('is read by the lists that had those names', () => {
    expect(parsePathwaysSearch({sort: 'Procedure', asc: false})).toEqual({sort: 'Pathway', asc: false});
    expect(parseTasksSearch({sort: 'Procedure'})).toEqual({sort: 'Pathway'});
    expect(parseAuditSearch({procedureId: ID})).toEqual({pathwayId: ID});
    expect(parseActivitySearch({action: 'ProcedureViewed'})).toEqual({action: 'PathwayViewed'});
  });
});

describe('old addresses, in the app', () => {
  it('sends the old Procedures list to Pathways, with the same search, without keeping the old address in the history', async () => {
    mockFetch(() => json(pageOf([])));
    const {router} = renderApp({path: '/procedures?status=Blocked&sort=Procedure&mine=false', user: staffUser});

    await waitFor(() => expect(router.state.location.pathname).toBe('/pathways'));
    expect(router.state.location.search).toEqual({status: 'Blocked', sort: 'Pathway', mine: false});
    expect(router.history.length).toBe(1);
  });

  it('sends the old address of a procedure to its pathway, keeping the link to a note deletion request', async () => {
    mockFetch(() => json({}, 404));
    const request = '0000000d-0000-4000-8000-000000000002';
    const {router} = renderApp({path: `/procedures/${ID}?deletionRequest=${request}`, user: adminUser});

    await waitFor(() => expect(router.state.location.pathname).toBe(`/pathways/${ID}`));
    expect(router.state.location.search).toEqual({deletionRequest: request});
  });

  it('sends the old procedures page of a patient to their pathways', async () => {
    mockFetch(() => json(pageOf([])));
    const {router} = renderApp({path: `/patients/${ID}/procedures`, user: staffUser});

    await waitFor(() => expect(router.state.location.pathname).toBe(`/patients/${ID}/pathways`));
  });

  it('sends the old Templates pages to Pathway Templates', async () => {
    mockFetch(() => json([]));
    const {router} = renderApp({path: '/admin/templates/new', user: adminUser});

    await waitFor(() => expect(router.state.location.pathname).toBe('/admin/pathway-templates/new'));
  });

  it('keeps the part of the address after a hash', async () => {
    mockFetch(() => json(pageOf([])));
    const {router} = renderApp({path: '/procedures#list', user: staffUser});

    await waitFor(() => expect(router.state.location.pathname).toBe('/pathways'));
    expect(router.state.location.hash).toBe('list');
  });

  it('still asks a signed-out visitor to sign in', async () => {
    const {router} = renderApp({path: '/procedures', user: signedOutUser});

    await waitFor(() => expect(router.state.location.pathname).toBe('/login'));
  });
});

describe('pinsNow', () => {
  it('reads pathway pins saved when pathways were called procedures', () => {
    expect(pinsNow({patients: ['p1'], procedures: ['x1']})).toEqual({patients: ['p1'], pathways: ['x1']});
  });

  it('prefers the pins saved under the name used now', () => {
    expect(pinsNow({pathways: [], procedures: ['x1']})).toEqual({pathways: []});
  });

  it('gives nothing for anything that is not an object', () => {
    expect(pinsNow(null)).toBeNull();
    expect(pinsNow('pins')).toBeNull();
  });

  it('is how the side nav reads its pins, which it saves under the name used now', () => {
    localStorage.setItem(PINS_KEY, JSON.stringify({patients: ['p1'], procedures: ['x1', 'x2']}));

    const {result} = renderHook(() => usePins('pathways', 'test-user'));
    expect(result.current.ids).toEqual(['x1', 'x2']);

    act(() => result.current.unpin('x1'));
    expect(JSON.parse(localStorage.getItem(PINS_KEY)!)).toEqual({patients: ['p1'], pathways: ['x2']});
  });
});
