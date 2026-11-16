/**
 * The colours charts draw their series in: the theme's data-visualisation tokens, so a chart follows the light and
 * dark modes and the colour themes like everything else. Assign them in this order and never cycle them: a chart's
 * first series is always `SERIES_COLOURS[0]`.
 *
 * Blue then teal: the pair passes the colour-blindness separation and lightness checks on the card colours of both
 * modes (the dataviz skill's validator, OKLab ΔE ≥ 29 for every kind of colour blindness). Teal is under 3:1 against
 * a light card, so every chart also has a legend (two series or more) and a table of its numbers.
 */
export const SERIES_COLOURS = ['var(--color-data-categorical-blue)', 'var(--color-data-categorical-teal)'] as const;

/** The plot's chrome: recessive hairlines and secondary text, in the theme's tokens. */
export const CHART_INK = {
  grid: 'var(--color-border)',
  axisText: 'var(--color-text-secondary)',
  /** Drawn around each bar, so neighbouring bars and stacked segments have a gap of the card's colour between them. */
  gap: 'var(--color-background-card)',
  fontSize: 'var(--font-size-sm)',
} as const;
