import {testAppointment, testBlock, testCalendar, testMeeting} from '../../test/calendar';
import {appointmentItem, appointmentPurpose, attendingCount, blockItem, calendarItems, MEETING_COLOUR, meetingItem, meetingKey} from './calendarItems';

const MONDAY = '2026-11-23';

describe('calendarItems', () => {
  it('makes a meeting an item of its own: its title, time and how many are going, in the Meeting colour', () => {
    const meeting = testMeeting({id: 'm1', date: MONDAY, start: '09:00', end: '10:00', title: 'MDT'});

    expect(meetingItem(meeting)).toEqual({
      key: 'meeting-m1',
      kind: 'meeting',
      date: MONDAY,
      startUtc: meeting.startUtc,
      endUtc: meeting.endUtc,
      colour: 'teal',
      label: 'MDT',
      sublabel: '2 attending',
      description: 'MDT, meeting, 09:00 to 10:00, 2 attending',
      isMovable: true,
    });
    expect(MEETING_COLOUR).toBe('teal');
    expect(meetingKey('m1')).toBe('meeting-m1');
  });

  it('lets only those who can change a meeting move it, and copes with missing details', () => {
    const fixed = meetingItem(testMeeting({canEdit: false}));
    expect(fixed.isMovable).toBe(false);

    const bare = meetingItem({id: 'bare', startUtc: '2026-11-24T09:00:00Z', endUtc: '2026-11-24T09:30:00Z'});
    expect(bare).toMatchObject({label: '', sublabel: '0 attending', date: '2026-11-24', isMovable: false});
    expect(meetingItem({}).date).toBe('');
    expect(attendingCount({attendees: [{userId: 'a', username: 'a'}]})).toBe('1 attending');
  });

  it("puts a calendar's blocks first, then its meetings, then its appointments, leaving out any without a time", () => {
    const block = testBlock(MONDAY, '12:00', '13:00');
    const meeting = testMeeting({date: MONDAY});
    const appointment = testAppointment({date: MONDAY});
    const calendar = testCalendar({
      from: MONDAY,
      today: MONDAY,
      blocks: [block],
      meetings: [meeting, {...meeting, id: 'no-end', endUtc: undefined}],
      appointments: [appointment, {...appointment, id: 'requested', startUtc: null}],
    });

    expect(calendarItems(calendar).map((i) => i.kind)).toEqual(['block', 'meeting', 'appointment']);
    expect(calendarItems({})).toEqual([]);
    expect(calendarItems(undefined)).toEqual([]);
    expect(blockItem(block).kind).toBe('block');
    expect(appointmentItem(appointment).kind).toBe('appointment');
  });

  it('makes blocks movable only when asked (Edit blocks), when they can change, and never on a past day', () => {
    const today = testBlock(MONDAY, '12:00', '13:00', {blockId: 'today'});
    const fixed = testBlock(MONDAY, '14:00', '15:00', {blockId: 'fixed', canEdit: false});
    const template = testBlock(MONDAY, '15:00', '15:30', {isFromTemplate: true});
    // The day before: past, whatever the block says.
    const past = testBlock('2026-11-20', '12:00', '13:00', {blockId: 'past'});
    const calendar = testCalendar({from: '2026-11-20', dayCount: 4, today: MONDAY, blocks: [today, fixed, template, past]});
    const movable = (items: ReturnType<typeof calendarItems>) => items.filter((i) => i.isMovable).map((i) => i.key);

    expect(movable(calendarItems(calendar))).toEqual([]);
    expect(movable(calendarItems(calendar, {movableBlocks: true}))).toEqual(['block-today', `block-template-block-15:00-${MONDAY}`]);
    expect(calendarItems(calendar, {movableBlocks: true}).find((i) => i.key.startsWith('block-template'))?.movesWithinDay).toBe(true);
    expect(blockItem(today).isMovable).toBe(false);
    expect(blockItem(today, {isMovable: true}).isMovable).toBe(true);
    expect(blockItem(fixed, {isMovable: true}).isMovable).toBe(false);
  });

  it('copes with blocks and appointments missing details', () => {
    expect(blockItem({})).toMatchObject({key: 'block-undefined-undefined', date: '', label: '', colour: 'gray', isMovable: false, movesWithinDay: false, isFlagged: false});
    expect(appointmentItem({})).toMatchObject({date: '', label: '', sublabel: null, colour: 'blue', isMovable: false});
  });

  it('says what an appointment is for', () => {
    expect(appointmentPurpose({procedureTitle: 'Knee review', stageName: 'Assessment'})).toBe('Knee review: Assessment');
    expect(appointmentPurpose({procedureTitle: 'Knee review', stageName: null})).toBe('Knee review');
    expect(appointmentPurpose({procedureTitle: null, stageName: null})).toBe('Appointment');
  });
});
