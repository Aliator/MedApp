import type {CSSProperties} from 'react';

/**
 * A description with nothing in it, which still takes its line. Astryx puts a field's description between its label
 * and its input, so a field without one sits higher than a neighbour with one. Give this to the field with none when
 * another field in its row has a description, or when its own description comes and goes, so the inputs line up and
 * don't jump.
 */
export const BLANK_DESCRIPTION = ' ';

/**
 * Every field keeps an empty line under its input for its error message (the theme's `components.field`), so an
 * error showing or clearing never moves anything. A field that never shows an error, such as a stage's notes, can
 * drop that line with this as its `containerStyle`.
 */
export const NO_STATUS_LINE = {paddingBottom: 0} as const;

/**
 * The same for every field inside an element (it sets the variable the theme reads): for a part whose fields never
 * show an error, e.g. a list's search, or a rich text box's toolbar (its block format is a field). Give it as that
 * element's `style`.
 */
export const NO_FIELD_STATUS_LINE = {'--medapp-field-status-line': '0px'} as CSSProperties;
