import {call, startLightDemo} from '../../test/lightDemo';
import {ADMIN} from '../accounts';
import {PATIENTS} from '../ids';
import {today} from '../time';

type Row = {id: string; number?: string; patientName?: string; status?: string};
const ids = (rows: Row[]) => rows.map((r) => r.id);
const sortedCopy = (values: string[], descending = false) => {
  const copy = [...values].sort((a, b) => a.localeCompare(b));
  return descending ? copy.reverse() : copy;
};

describe('the light demo’s searched and sorted lists', () => {
  it('searches the Lab page’s worklist by specimen number, in the body, and sorts it by a column', async () => {
    startLightDemo(ADMIN);
    const all = (await call('POST', '/api/specimens/search', {view: 'All', pageSize: 100})).body.items as Row[];
    expect(all.length).toBeGreaterThan(1);
    const target = all[0];

    const byNumber = (await call('POST', '/api/specimens/search', {view: 'All', search: ` ${target.number!.toLowerCase().replace('-', ' -')} `})).body;
    expect(ids(byNumber.items)).toEqual([target.id]);
    const byPatient = (await call('POST', '/api/specimens/search', {view: 'All', search: target.patientName!.split(' ')[0], pageSize: 100})).body.items as Row[];
    expect(byPatient.every((s) => s.patientName === target.patientName || s.patientName!.includes(target.patientName!.split(' ')[0]))).toBe(true);

    const byNumberSorted = (await call('POST', '/api/specimens/search', {view: 'All', sort: 'number', pageSize: 100})).body.items as Row[];
    expect(byNumberSorted.map((s) => s.number!)).toEqual(sortedCopy(all.map((s) => s.number!)));

    expect((await call('POST', '/api/specimens/search', {search: 'x'.repeat(201)})).status).toBe(400);
  });

  it('searches results by the patient’s name, and sorts them by a column', async () => {
    startLightDemo(ADMIN);
    const all = (await call('GET', '/api/results/search?View=All&PageSize=100')).body.items as Row[];
    const name = all[0].patientName!;

    const found = (await call('GET', `/api/results/search?View=All&Search=${encodeURIComponent(name.toUpperCase())}`)).body.items as Row[];
    expect(found.length).toBeGreaterThan(0);
    expect(found.every((r) => r.patientName === name)).toBe(true);

    const byTitle = (await call('GET', '/api/results/search?View=All&Sort=title&PageSize=100')).body.items as {title: string}[];
    expect(byTitle.map((r) => r.title.toLowerCase())).toEqual(sortedCopy(byTitle.map((r) => r.title.toLowerCase())));
    expect((await call('GET', '/api/results/search?View=All&Sort=status&Ascending=false')).status).toBe(200);
    expect((await call('GET', `/api/results/search?View=All&Search=${'x'.repeat(201)}`)).status).toBe(400);
  });

  it('lists every letter in All, searched by the patient’s name and sorted by a column', async () => {
    startLightDemo(ADMIN);
    const all = (await call('GET', '/api/letters/search?View=All&PageSize=100')).body.items as Row[];
    expect(all.length).toBeGreaterThan(1);
    const name = all[0].patientName!;

    const found = (await call('GET', `/api/letters/search?View=All&Search=${encodeURIComponent(name.split(' ')[1])}`)).body.items as Row[];
    expect(found.every((l) => l.patientName!.includes(name.split(' ')[1]))).toBe(true);

    const byStatus = (await call('GET', '/api/letters/search?View=All&Sort=status&PageSize=100')).body.items as Row[];
    const order = ['Draft', 'AwaitingApproval', 'Approved', 'Withdrawn'];
    expect(byStatus.map((l) => order.indexOf(l.status!))).toEqual([...byStatus.map((l) => order.indexOf(l.status!))].sort((a, b) => a - b));
    for (const sort of ['patient', 'letter', 'latest'])
      expect((await call('GET', `/api/letters/search?View=All&Sort=${sort}&Ascending=false`)).status).toBe(200);
    expect((await call('GET', `/api/letters/search?View=All&Search=${'x'.repeat(201)}`)).status).toBe(400);
  });

  it('searches data requests by the patient’s name, and sorts them by a column', async () => {
    startLightDemo(ADMIN);
    for (const patientId of [PATIENTS.ellisVarden, PATIENTS.priyaHolloway])
      expect((await call('POST', '/api/data-requests', {patientId, receivedOn: today(), requestedByPatient: true})).status).toBe(201);
    const all = (await call('GET', '/api/data-requests/search?PageSize=100')).body.items as Row[];
    expect(all.length).toBeGreaterThan(0);
    const name = all[0].patientName!;

    const found = (await call('GET', `/api/data-requests/search?Search=${encodeURIComponent(name.toLowerCase())}`)).body.items as Row[];
    expect(found.every((r) => r.patientName === name)).toBe(true);
    expect((await call('GET', '/api/data-requests/search?Search=nobody-at-all')).body.totalCount).toBe(0);

    const byReceived = (await call('GET', '/api/data-requests/search?Sort=received&Ascending=false&PageSize=100')).body.items as {receivedOn: string}[];
    expect(byReceived.map((r) => r.receivedOn)).toEqual(sortedCopy(byReceived.map((r) => r.receivedOn), true));
    for (const sort of ['patient', 'due', 'status']) expect((await call('GET', `/api/data-requests/search?Sort=${sort}`)).status).toBe(200);
    expect((await call('GET', `/api/data-requests/search?Search=${'x'.repeat(201)}`)).status).toBe(400);
  });
});
