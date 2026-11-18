import {mdtMeeting} from '../test/mdt';
import {mdtStatusWords, upcomingMeetings} from './mdt';

describe('upcomingMeetings', () => {
  it('keeps the MDTs not yet ended, soonest first', () => {
    const now = new Date('2026-11-18T12:00:00Z');
    const ended = mdtMeeting({id: 'a', startUtc: '2026-11-17T13:00:00Z', endUtc: '2026-11-17T14:00:00Z'});
    const later = mdtMeeting({id: 'b', startUtc: '2026-12-03T13:00:00Z', endUtc: '2026-12-03T14:00:00Z'});
    const sooner = mdtMeeting({id: 'c', startUtc: '2026-11-26T13:00:00Z', endUtc: '2026-11-26T14:00:00Z'});
    const underWay = mdtMeeting({id: 'd', startUtc: '2026-11-18T11:30:00Z', endUtc: '2026-11-18T12:30:00Z'});

    expect(upcomingMeetings([ended, later, sooner, underWay], now).map((m) => m.id)).toEqual(['d', 'c', 'b']);
  });
});

describe('mdtStatusWords', () => {
  it('puts each status in words, and anything else as it is', () => {
    expect(mdtStatusWords('Discussed')).toBe('Discussed');
    expect(mdtStatusWords('Unknown')).toBe('Unknown');
    expect(mdtStatusWords(null)).toBe('');
  });
});
