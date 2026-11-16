/**
 * Colour contrast, as WCAG measures it (https://www.w3.org/TR/WCAG22/#dfn-contrast-ratio): from each colour's
 * relative luminance, (lighter + 0.05) / (darker + 0.05), from 1:1 (the same) to 21:1 (black on white).
 */

/** The least contrast text needs (WCAG 1.4.3, AA, for text under 18pt, or 14pt bold). */
export const MIN_TEXT_CONTRAST = 4.5;

const HEX = /^#([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})$/i;

/** A `#rrggbb` colour's red, green and blue, 0 to 255. Throws for anything else. */
export function parseHex(colour: string): [number, number, number] {
  const match = HEX.exec(colour);
  if (!match) throw new Error(`Not a #rrggbb colour: ${colour}`);
  return [parseInt(match[1], 16), parseInt(match[2], 16), parseInt(match[3], 16)];
}

/** Red, green and blue (0 to 255, rounded) as `#rrggbb`. */
export function toHex([r, g, b]: readonly number[]): string {
  return `#${[r, g, b].map((c) => Math.round(Math.min(255, Math.max(0, c))).toString(16).padStart(2, '0')).join('')}`;
}

/** One sRGB channel (0 to 255), linearised. */
function linear(channel: number): number {
  const c = channel / 255;
  return c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
}

/** A colour's relative luminance, 0 (black) to 1 (white). */
export function relativeLuminance(colour: string): number {
  const [r, g, b] = parseHex(colour).map(linear);
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

/** The contrast between two colours, 1 to 21, whichever is lighter. */
export function contrastRatio(a: string, b: string): number {
  const [lighter, darker] = [relativeLuminance(a), relativeLuminance(b)].sort((x, y) => y - x);
  return (lighter + 0.05) / (darker + 0.05);
}

/**
 * CSS's `color-mix(in srgb, colour percent%, other)`: each channel weighted, as the browser mixes them (in sRGB, not
 * linear light).
 */
export function mixSrgb(colour: string, percent: number, other: string): string {
  const a = parseHex(colour);
  const b = parseHex(other);
  return toHex(a.map((c, i) => (c * percent + b[i] * (100 - percent)) / 100));
}

/** A ratio as people read it, to one decimal place and never rounded up past the line: `4.4:1`. */
export function formatRatio(ratio: number): string {
  return `${(Math.floor(ratio * 10) / 10).toFixed(1)}:1`;
}
