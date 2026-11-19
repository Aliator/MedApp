import {fireEvent, screen, waitFor, within} from '@testing-library/react';
import {useState} from 'react';
import {STAFF_ME, STAFF_OTHER, testCalendar} from '../../test/calendar';
import {calls, expectAnnounced, isDisabled, json, mockApi, openDialog, renderWithApp, staffUser, withAnnouncements} from '../../test/render';
import {ApplyTemplateDialog, type ApplyTemplateDialogProps} from './ApplyTemplateDialog';

const MONDAY = '2026-11-23';
/** Today, in these tests: Wednesday, so Monday and Tuesday have passed. */
const WEDNESDAY = '2026-11-25';
const DAY_TEMPLATES = [
  {id: 'day-clinic', name: 'Clinic day', isArchived: false},
  {id: 'day-short', name: 'Short day', isArchived: false},
  {id: 'day-old', name: 'Old day', isArchived: true},
];

/**
 * The week: Monday on Clinic day from the week, Thursday changed to Short day, Friday changed to no template (unless
 * `cleared` names them: back to the week's).
 */
function week(cleared: string[] = []) {
  const calendar = testCalendar({from: MONDAY, today: WEDNESDAY});
  calendar.days = calendar.days!.map((d) =>
    cleared.includes(d.date ?? '')
      ? d
      : d.date === MONDAY
        ? {...d, dayTemplateId: 'day-clinic', dayTemplateName: 'Clinic day'}
        : d.date === '2026-11-26'
          ? {...d, dayTemplateId: 'day-short', dayTemplateName: 'Short day', isDayTemplateOverridden: true}
          : d.date === '2026-11-27'
            ? {...d, isDayTemplateOverridden: true}
            : d,
  );
  return calendar;
}

/** The API's 404 for clearing a day that has no day template of its own. */
const NOTHING_TO_CLEAR = () => json({title: 'This day has no day template of its own.'}, 404);

const DAY = (date: string) => `/api/calendar/staff/${STAFF_ME.userId}/days/${date}/day-template`;

function renderDialog(props: Partial<ApplyTemplateDialogProps> = {}, handlers: Parameters<typeof mockApi>[0] = {}) {
  const onClose = vi.fn();
  const fetch = mockApi({
    'GET /api/calendar': () => json(week()),
    [`GET /api/calendar/staff/${STAFF_ME.userId}/day-templates`]: () => json(DAY_TEMPLATES),
    ...handlers,
  });
  const result = renderWithApp(
    <ApplyTemplateDialog isOpen staffUserId={STAFF_ME.userId} date={WEDNESDAY} startDate={WEDNESDAY} onClose={onClose} {...props} />,
    {user: staffUser},
  );
  return {...result, fetch, onClose};
}

/** The open dialog, or the whole page until one opens (for `findBy` queries, which wait). */
const dialog = () => within(openDialog() ?? document.body);
const day = (name: RegExp | string) => dialog().getByRole('checkbox', {name});
const bodyOf = async (fetch: ReturnType<typeof mockApi>, method: string, path: string) => {
  const request = fetch.mock.calls.map(([r]) => r as Request).find((r) => r.method === method && new URL(r.url).pathname === path)!;
  return request.clone().json();
};

async function choose(user: ReturnType<typeof renderDialog>['user'], template: string) {
  await user.click(dialog().getByRole('combobox', {name: /Template/}));
  await user.click(await screen.findByRole('option', {name: new RegExp(`^${template}`)}));
}

// Slow under coverage on a loaded machine (CI): give it room.
describe('ApplyTemplateDialog', {timeout: 30_000}, () => {
  beforeEach(() => {
    vi.useFakeTimers({toFake: ['Date'], now: new Date('2026-11-25T07:00:00Z')});
  });

  it("lists the week's working days with what applies now, past days disabled, and the start day ticked", async () => {
    const {user, fetch} = renderDialog();

    expect(await screen.findByRole('heading', {name: 'Apply Template'})).toBeInTheDocument();
    const days = await dialog().findByRole('group', {name: /Days/});
    expect(within(days).getAllByRole('checkbox')).toHaveLength(5);
    expect(isDisabled(day(/^Mon 23: Clinic day/))).toBe(true);
    expect(dialog().getAllByText("Past days can't change")).toHaveLength(2);
    expect(isDisabled(day(/^Tue 24: No template/))).toBe(true);
    expect(isDisabled(day(/^Wed 25/))).toBe(false);
    expect(day(/^Wed 25: No template/)).toBeChecked();
    expect(day(/^Thu 26: Short day/)).not.toBeChecked();
    expect(dialog().getAllByText('Changed for this day')).toHaveLength(2);
    // A past day can't be ticked.
    await user.click(day(/^Tue 24/));
    expect(day(/^Tue 24/)).not.toBeChecked();
    // Apply waits for a template.
    expect(isDisabled(dialog().getByRole('button', {name: 'Apply'}))).toBe(true);
    // Your own week (no staff named), without its appointments, so it names no patients and records no reads.
    expect(calls(fetch)).toContain('GET /api/calendar?From=2026-11-23&To=2026-11-29&WithoutAppointments=true');
  });

  it("shows a closed day, saying so, but won't tick it, even as the start day", async () => {
    const calendar = week();
    calendar.days = calendar.days!.map((d) => (d.date === WEDNESDAY ? {...d, isWorkingDay: false, closedDayName: 'Staff training day'} : d));
    const {user} = renderDialog({}, {'GET /api/calendar': () => json(calendar)});

    const days = await dialog().findByRole('group', {name: /Days/});
    expect(within(days).getAllByRole('checkbox')).toHaveLength(5);
    expect(dialog().getByText('Closed: Staff training day')).toBeInTheDocument();
    expect(isDisabled(day(/^Wed 25/))).toBe(true);
    expect(day(/^Wed 25/)).not.toBeChecked();
    await user.click(day(/^Wed 25/));
    expect(day(/^Wed 25/)).not.toBeChecked();
  });

  it('reads the week again each time it opens', async () => {
    const fetch = mockApi({
      'GET /api/calendar': () => json(week()),
      [`GET /api/calendar/staff/${STAFF_ME.userId}/day-templates`]: () => json(DAY_TEMPLATES),
    });
    function Toggled() {
      const [isOpen, setIsOpen] = useState(true);
      return (
        <>
          <button type="button" onClick={() => setIsOpen((open) => !open)}>
            Toggle
          </button>
          <ApplyTemplateDialog isOpen={isOpen} staffUserId={STAFF_ME.userId} date={WEDNESDAY} startDate={WEDNESDAY} onClose={() => setIsOpen(false)} />
        </>
      );
    }
    renderWithApp(<Toggled />, {user: staffUser});
    await dialog().findByRole('group', {name: /Days/});
    const weekReads = () => calls(fetch).filter((c) => c.startsWith('GET /api/calendar?')).length;
    expect(weekReads()).toBe(1);

    fireEvent.click(screen.getByRole('button', {name: 'Toggle', hidden: true}));
    await waitFor(() => expect(openDialog()).toBeNull());
    fireEvent.click(screen.getByRole('button', {name: 'Toggle'}));
    await waitFor(() => expect(weekReads()).toBe(2));
  });

  it('applies one template to two days, a call for each, and announces it', async () => {
    withAnnouncements();
    const {user, fetch, onClose} = renderDialog(
      {},
      {[`PUT ${DAY('2026-11-25')}`]: () => json({}), [`PUT ${DAY('2026-11-27')}`]: () => json({})},
    );

    await dialog().findByRole('group', {name: /Days/});
    await user.click(dialog().getByRole('combobox', {name: /Template/}));
    // Only active templates, then an empty day and the default week's.
    expect(screen.queryByRole('option', {name: /^Old day/})).not.toBeInTheDocument();
    expect(screen.getByRole('option', {name: /^No template/})).toBeInTheDocument();
    await user.click(screen.getByRole('option', {name: /^Clinic day/}));
    await user.click(day(/^Fri 27/));
    await user.click(dialog().getByRole('button', {name: 'Apply'}));

    await expectAnnounced('Clinic day applied to Wed 25 and Fri 27');
    expect(onClose).toHaveBeenCalled();
    expect(await bodyOf(fetch, 'PUT', DAY('2026-11-25'))).toEqual({dayTemplateId: 'day-clinic'});
    expect(await bodyOf(fetch, 'PUT', DAY('2026-11-27'))).toEqual({dayTemplateId: 'day-clinic'});
  });

  it('makes a day empty with No template', async () => {
    withAnnouncements();
    const {user, fetch} = renderDialog({}, {[`PUT ${DAY('2026-11-25')}`]: () => json({})});

    await dialog().findByRole('group', {name: /Days/});
    await choose(user, 'No template');
    await user.click(dialog().getByRole('button', {name: 'Apply'}));

    await expectAnnounced('No template on Wed 25');
    expect(await bodyOf(fetch, 'PUT', DAY('2026-11-25'))).toEqual({dayTemplateId: null});
  });

  it("goes back to the default week's on every day ticked, a day with nothing of its own already done", async () => {
    withAnnouncements();
    const cleared: string[] = [];
    const clear = (date: string) => () => {
      cleared.push(date);
      return new Response(null, {status: 204});
    };
    const {user, fetch, onClose} = renderDialog(
      {},
      {
        'GET /api/calendar': () => json(week(cleared)),
        // Wednesday follows the week already (or someone put it back meanwhile): the API's 404 means it's as wanted.
        [`DELETE ${DAY('2026-11-25')}`]: NOTHING_TO_CLEAR,
        [`DELETE ${DAY('2026-11-26')}`]: clear('2026-11-26'),
        [`DELETE ${DAY('2026-11-27')}`]: clear('2026-11-27'),
      },
    );

    await dialog().findByRole('group', {name: /Days/});
    await choose(user, "Default week's template");
    await user.click(day(/^Thu 26/));
    await user.click(day(/^Fri 27/));
    await user.click(dialog().getByRole('button', {name: 'Apply'}));

    await expectAnnounced("Back to the default week's template on Wed 25, Thu 26 and Fri 27");
    expect(onClose).toHaveBeenCalled();
    // Every day ticked is asked, whatever the dialog's copy said, and the week read again to check.
    expect(calls(fetch).filter((c) => c.startsWith('DELETE'))).toEqual([`DELETE ${DAY('2026-11-25')}`, `DELETE ${DAY('2026-11-26')}`, `DELETE ${DAY('2026-11-27')}`]);
    const made = calls(fetch);
    expect(made.findLastIndex((c) => c.startsWith('GET /api/calendar?'))).toBeGreaterThan(made.findLastIndex((c) => c.startsWith('DELETE')));
    expect(calls(fetch).some((c) => c.startsWith('PUT'))).toBe(false);
  });

  it("names a day still with a template of its own after going back to the default week's", async () => {
    withAnnouncements();
    const {user, onClose} = renderDialog(
      {},
      {
        // Wednesday's copy says it follows the week; it was given a template meanwhile, which is still there after.
        'GET /api/calendar': () => {
          const calendar = week();
          calendar.days = calendar.days!.map((d) => (d.date === WEDNESDAY ? {...d, isDayTemplateOverridden: true} : d));
          return json(calendar);
        },
        [`DELETE ${DAY('2026-11-25')}`]: () => new Response(null, {status: 204}),
      },
    );

    await dialog().findByRole('group', {name: /Days/});
    await choose(user, "Default week's template");
    await user.click(dialog().getByRole('button', {name: 'Apply'}));

    expect(await dialog().findByText("One day couldn't be changed")).toBeInTheDocument();
    expect(dialog().getByText(/^Wed 25: Someone gave this day its own template/)).toBeInTheDocument();
    expect(onClose).not.toHaveBeenCalled();
    expect(day(/^Wed 25/)).toBeChecked();
  });

  it("names a day the default week's couldn't go back to, and one still with a template of its own", async () => {
    withAnnouncements();
    const {user, onClose} = renderDialog(
      {},
      {
        // Wednesday was given a template meanwhile, which is still there after; Thursday's can't be cleared.
        'GET /api/calendar': () => {
          const calendar = week();
          calendar.days = calendar.days!.map((d) => (d.date === WEDNESDAY ? {...d, isDayTemplateOverridden: true} : d));
          return json(calendar);
        },
        [`DELETE ${DAY('2026-11-25')}`]: () => new Response(null, {status: 204}),
        [`DELETE ${DAY('2026-11-26')}`]: () => json({title: 'Something went wrong.'}, 500),
      },
    );

    await dialog().findByRole('group', {name: /Days/});
    await choose(user, "Default week's template");
    await user.click(day(/^Thu 26/));
    await user.click(dialog().getByRole('button', {name: 'Apply'}));

    expect(await dialog().findByText(/^Wed 25: Someone gave this day its own template/)).toBeInTheDocument();
    // Thursday is named for why it failed, not again for still having its own template.
    expect(dialog().getByText("2 days couldn't be changed")).toBeInTheDocument();
    expect(dialog().queryByText(/^Thu 26: Someone gave this day its own template/)).not.toBeInTheDocument();
    expect(onClose).not.toHaveBeenCalled();
  });

  it("closes once every day went back to the default week's, even if the week can't be read again to check", async () => {
    withAnnouncements();
    let reads = 0;
    const {user, onClose} = renderDialog(
      {},
      {
        'GET /api/calendar': () => (++reads === 1 ? json(week()) : json({title: 'Down'}, 500)),
        [`DELETE ${DAY('2026-11-25')}`]: NOTHING_TO_CLEAR,
      },
    );

    await dialog().findByRole('group', {name: /Days/});
    await choose(user, "Default week's template");
    await user.click(dialog().getByRole('button', {name: 'Apply'}));

    await expectAnnounced("Back to the default week's template on Wed 25");
    expect(onClose).toHaveBeenCalled();
  });

  it('keeps the dialog open and names the days that failed, leaving only those ticked', async () => {
    withAnnouncements();
    const {user, onClose} = renderDialog(
      {},
      {
        [`PUT ${DAY('2026-11-25')}`]: () => json({}),
        [`PUT ${DAY('2026-11-26')}`]: () => json({title: 'That day has passed.'}, 409),
      },
    );

    await dialog().findByRole('group', {name: /Days/});
    await choose(user, 'Short day');
    await user.click(day(/^Thu 26/));
    await user.click(dialog().getByRole('button', {name: 'Apply'}));

    expect(await dialog().findByText("One day couldn't be changed")).toBeInTheDocument();
    expect(dialog().getByText('Thu 26: That day has passed.')).toBeInTheDocument();
    await expectAnnounced('Short day applied to Wed 25');
    expect(onClose).not.toHaveBeenCalled();
    expect(day(/^Wed 25/)).not.toBeChecked();
    expect(day(/^Thu 26/)).toBeChecked();
  });

  it("ticks nothing at the start when the start day isn't in the week or has passed, and waits for a day", async () => {
    const {user, unmount} = renderDialog({startDate: '2026-12-01'});
    await dialog().findByRole('group', {name: /Days/});
    expect(dialog().getAllByRole('checkbox').filter((c) => (c as HTMLInputElement).checked)).toHaveLength(0);
    await choose(user, 'Clinic day');
    expect(isDisabled(dialog().getByRole('button', {name: 'Apply'}))).toBe(true);
    unmount();

    renderDialog({startDate: '2026-11-24'});
    await dialog().findByRole('group', {name: /Days/});
    expect(day(/^Tue 24/)).not.toBeChecked();
  });

  it("reads someone else's week and templates, and says when the week can't be loaded", async () => {
    const {fetch, unmount} = renderDialog(
      {staffUserId: STAFF_OTHER.userId, calendarStaff: STAFF_OTHER.userId},
      {[`GET /api/calendar/staff/${STAFF_OTHER.userId}/day-templates`]: () => json(DAY_TEMPLATES)},
    );
    await dialog().findByRole('group', {name: /Days/});
    await waitFor(() =>
      expect(calls(fetch)).toEqual(
        expect.arrayContaining([
          `GET /api/calendar?StaffUserId=${STAFF_OTHER.userId}&From=2026-11-23&To=2026-11-29&WithoutAppointments=true`,
          `GET /api/calendar/staff/${STAFF_OTHER.userId}/day-templates?includeArchived=true`,
        ]),
      ),
    );
    unmount();

    renderDialog({}, {'GET /api/calendar': () => json({}, 500)});
    expect(await dialog().findByText('The week could not be loaded. Please try again.')).toBeInTheDocument();
  });

  it('reads nothing while closed', () => {
    const {fetch} = renderDialog({isOpen: false});
    expect(calls(fetch)).toEqual([]);
  });
});
