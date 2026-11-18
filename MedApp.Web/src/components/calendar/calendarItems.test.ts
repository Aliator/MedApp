import {hasDeclined} from '../../api/calendar';
import {testAppointment, testBlock, testCalendar, testMeeting, STAFF_ME, STAFF_OTHER} from '../../test/calendar';
import {
  appointmentItem,
  appointmentPurpose,
  attendingCount,
  blockItem,
  calendarItems,
  MEETING_COLOUR,
  meetingItem,
  meetingKey,
  SESSION_COLOUR,
  sessionItem,
  sessionKey,
} from './calendarItems';
import {testSession} from '../../test/clinics';

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
      isFaded: false,
      isFlagged: false,
    });
    expect(MEETING_COLOUR).toBe('teal');
    expect(meetingKey('m1')).toBe('meeting-m1');
  });

  it('flags a meeting an appointment overlaps (one recorded after the event may), saying so', () => {
    const item = meetingItem(testMeeting({date: MONDAY, start: '09:00', end: '10:00', overlapsAppointment: true}));

    expect(item.isFlagged).toBe(true);
    expect(item.description).toBe('Team huddle, meeting, 09:00 to 10:00, 2 attending, overlaps an appointment');
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

  it("never lets someone who can't book move an appointment, even one that can be rescheduled", () => {
    const appointment = testAppointment({id: 'a1', canReschedule: true});
    expect(appointmentItem(appointment).isMovable).toBe(true);
    expect(appointmentItem(appointment, {canBook: false}).isMovable).toBe(false);
    const calendar = testCalendar({from: MONDAY, dayCount: 1, today: MONDAY, appointments: [appointment]});
    expect(calendarItems(calendar, {canBook: false}).filter((i) => i.kind === 'appointment').every((i) => !i.isMovable)).toBe(true);
  });

  it('says what an appointment is for', () => {
    expect(appointmentPurpose({pathwayTitle: 'Knee review', stageName: 'Assessment'})).toBe('Knee review: Assessment');
    expect(appointmentPurpose({pathwayTitle: 'Knee review', stageName: null})).toBe('Knee review');
    expect(appointmentPurpose({pathwayTitle: null, stageName: null})).toBe('Appointment');
  });

  it("fades a meeting on the calendar of someone who declined it, and says so, but not on anyone else's", () => {
    const meeting = testMeeting({
      id: 'm1',
      date: MONDAY,
      title: 'MDT',
      attendees: [
        {userId: STAFF_ME.userId, username: STAFF_ME.username, reply: 'Declined'},
        {userId: STAFF_OTHER.userId, username: STAFF_OTHER.username, reply: 'Accepted'},
      ],
    });

    expect(meetingItem(meeting, STAFF_ME.userId)).toMatchObject({
      sublabel: 'Declined',
      description: 'MDT, meeting, 09:00 to 10:00, Declined',
      isFaded: true,
    });
    expect(meetingItem(meeting, STAFF_OTHER.userId)).toMatchObject({sublabel: '1 attending', isFaded: false});
    expect(meetingItem(meeting)).toMatchObject({sublabel: '1 attending', isFaded: false});
    expect(meetingItem(meeting, null).isFaded).toBe(false);

    // A calendar's own owner decides.
    const mine = testCalendar({from: MONDAY, today: MONDAY, meetings: [meeting]});
    const theirs = testCalendar({from: MONDAY, today: MONDAY, staff: STAFF_OTHER, meetings: [meeting]});
    expect(calendarItems(mine).find((i) => i.kind === 'meeting')?.isFaded).toBe(true);
    expect(calendarItems(theirs).find((i) => i.kind === 'meeting')?.isFaded).toBe(false);
  });

  it('counts only those who have not declined as attending, and says who declined', () => {
    const attendees = [
      {userId: 'a', username: 'a', reply: 'Declined'},
      {userId: 'b', username: 'b', reply: 'NoReply'},
      {userId: 'c', username: 'c'},
    ];

    expect(attendingCount({attendees})).toBe('2 attending');
    expect(hasDeclined({attendees}, 'a')).toBe(true);
    expect(hasDeclined({attendees}, 'b')).toBe(false);
    expect(hasDeclined({attendees}, 'z')).toBe(false);
    expect(hasDeclined({}, 'a')).toBe(false);
    expect(hasDeclined({attendees}, undefined)).toBe(false);
  });

  it('makes a clinic session an item of its own: its team, room and how full it is, never dragged', () => {
    const session = testSession({date: '2026-11-24', booked: 1});

    expect(sessionItem(session)).toEqual({
      key: sessionKey('session-1'),
      kind: 'session',
      date: '2026-11-24',
      startUtc: session.startUtc,
      endUtc: session.endUtc,
      colour: SESSION_COLOUR,
      label: 'Synthetic dermatology',
      sublabel: 'Room 2 · 8 of 9 free',
      description: 'Synthetic dermatology clinic session, Room 2, 09:00 to 12:00, 8 of 9 free',
    });
    const calendar = testCalendar({from: MONDAY, today: MONDAY, sessions: [session, {...session, id: 'no-time', startUtc: undefined}]});
    expect(calendarItems(calendar).filter((i) => i.kind === 'session').map((i) => i.key)).toEqual([sessionKey('session-1')]);
  });
});
