import {toExpiry} from './session';

describe('toExpiry', () => {
  it("counts the server's seconds left from now, on this device's clock", () => {
    expect(toExpiry({expiresAtUtc: '2026-11-16T17:00:00Z', expiresInSeconds: 300, canExtend: true}, 1_000)).toEqual({
      endsAt: 301_000,
      canExtend: true,
    });
  });

  it('takes a missing answer as ended, and not extendable', () => {
    expect(toExpiry({}, 1_000)).toEqual({endsAt: 1_000, canExtend: false});
  });
});
