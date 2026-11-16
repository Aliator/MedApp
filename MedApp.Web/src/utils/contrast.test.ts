import {describe, expect, it} from 'vitest';
import {contrastRatio, formatRatio, MIN_TEXT_CONTRAST, mixSrgb, parseHex, relativeLuminance, toHex} from './contrast';

describe('contrast', () => {
  it('reads #rrggbb colours, in either case, and refuses anything else', () => {
    expect(parseHex('#0aFf80')).toEqual([10, 255, 128]);
    expect(() => parseHex('#fff')).toThrow('Not a #rrggbb colour');
    expect(() => parseHex('red')).toThrow();
  });

  it('writes colours back as #rrggbb, rounding and keeping each channel in range', () => {
    expect(toHex([10, 255, 128])).toBe('#0aff80');
    expect(toHex([-4, 255.6, 127.5])).toBe('#00ff80');
  });

  it('gives black and white their luminance, and a mid grey its linearised one', () => {
    expect(relativeLuminance('#000000')).toBe(0);
    expect(relativeLuminance('#ffffff')).toBeCloseTo(1, 10);
    // #777777 is about 0.184 (WCAG's formula), well under half: the eye is more sensitive to dark shades.
    expect(relativeLuminance('#777777')).toBeCloseTo(0.1845, 3);
    // Low channels take the straight part of the curve.
    expect(relativeLuminance('#0a0a0a')).toBeCloseTo(10 / 255 / 12.92, 10);
  });

  it('measures contrast either way round, from 1:1 to 21:1', () => {
    expect(contrastRatio('#000000', '#ffffff')).toBeCloseTo(21, 10);
    expect(contrastRatio('#ffffff', '#000000')).toBeCloseTo(21, 10);
    expect(contrastRatio('#3b82f6', '#3b82f6')).toBe(1);
  });

  it('agrees with known pairs on either side of 4.5:1', () => {
    // #767676 on white is the classic lightest grey that passes AA; #777777 just fails.
    expect(contrastRatio('#767676', '#ffffff')).toBeGreaterThanOrEqual(MIN_TEXT_CONTRAST);
    expect(contrastRatio('#777777', '#ffffff')).toBeLessThan(MIN_TEXT_CONTRAST);
  });

  it('mixes colours as CSS color-mix does in srgb', () => {
    expect(mixSrgb('#ffffff', 50, '#000000')).toBe('#808080');
    expect(mixSrgb('#e4ebf8', 94, '#000000')).toBe('#d6dde9');
    expect(mixSrgb('#212634', 90, '#ffffff')).toBe('#373c48');
  });

  it('writes ratios to one decimal place, never rounding up past the line', () => {
    expect(formatRatio(4.49)).toBe('4.4:1');
    expect(formatRatio(21)).toBe('21.0:1');
  });
});
