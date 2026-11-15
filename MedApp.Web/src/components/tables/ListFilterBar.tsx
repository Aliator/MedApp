import {PowerSearch, type PowerSearchConfig, type PowerSearchFilter} from '@astryxdesign/core/PowerSearch';
import {HStack, StackItem} from '@astryxdesign/core/Stack';
import {useEffect, useMemo, useRef} from 'react';
import {ListFavourites} from './ListFavourites';
import {NO_FIELD_STATUS_LINE} from '../forms/fieldSpace';

/** A text field the list's API filters by "contains" (e.g. a patient's first name), kept in the URL under `key`. */
export interface TextFilterField {
  kind: 'text';
  key: string;
  label: string;
}

/**
 * A date the list's API filters by an inclusive range (e.g. date of birth), kept in the URL as two dates
 * (`yyyy-mm-dd`) under `fromKey` and `toKey`. Each end is a filter of its own ("is on or after", "is on or before"),
 * so the tokens show their dates.
 */
export interface DateFilterField {
  kind: 'date';
  key: string;
  label: string;
  fromKey: string;
  toKey: string;
}

/**
 * One of a set of values (e.g. a status), kept in the URL under `key`. With `isMultiple`, also "is any of" and "is
 * not any of": the URL keeps a comma list (`InProgress,Blocked`), with a leading `!` for "not any of" (see
 * `parseChoice` and `choiceValues` in `pages/listSearch.ts`, which the list uses for its API).
 */
export interface ChoiceFilterField {
  kind: 'choice';
  key: string;
  label: string;
  options: readonly {value: string; label: string}[];
  isMultiple?: boolean;
}

/**
 * A yes-or-no filter (e.g. overdue only), kept in the URL as `true` under `key`. Its token reads "label: operator"
 * (e.g. "Overdue: only").
 */
export interface FlagFilterField {
  kind: 'flag';
  key: string;
  label: string;
  operator: string;
}

export type ListFilterField = TextFilterField | DateFilterField | ChoiceFilterField | FlagFilterField;

/** A day for a favourite's dates, as the URL keeps it (yyyy-mm-dd): today, moved by days and years. */
export function dayFromToday({days = 0, years = 0}: {days?: number; years?: number} = {}): string {
  const day = new Date();
  day.setDate(day.getDate() + days);
  day.setFullYear(day.getFullYear() + years);
  return `${day.getFullYear()}-${String(day.getMonth() + 1).padStart(2, '0')}-${String(day.getDate()).padStart(2, '0')}`;
}

/** The list's filter values, by URL key: what `ListFilterBar` reads and writes. Unset ones are undefined. */
export type ListFilterValues = Record<string, string | boolean | undefined>;

/** A quick look-up: a name, and the filters it sets (every other filter is cleared). */
export interface ListFilterFavourite {
  label: string;
  values: ListFilterValues;
}

export interface ListFilterBarProps {
  /** The fields people can filter by, as the list's API allows. */
  fields: ListFilterField[];
  /** The list's search (from the URL); only the fields' keys are read. */
  values: object;
  /** Called with every field's key: the new value, or undefined for a filter that was removed. */
  onChange: (values: ListFilterValues) => void;
  /** Names the bar for screen readers, e.g. "Search patients". */
  label: string;
  /** Shown while there are no filters, e.g. "Search patients...". */
  placeholder: string;
  /**
   * A text field that whatever's typed searches, without picking a field first (e.g. the procedure lists' search
   * of title, template and patient). Without one, typing looks for a field to filter by.
   */
  textKey?: string;
  /**
   * Which list this is (e.g. `patients`), for the searches people save from its star (`ListFavourites`), kept with
   * their account.
   */
  list: string;
  /** The list's own quick look-ups, in the star's menu with the saved ones. */
  favourites?: ListFilterFavourite[];
  /**
   * Whether the list's URL has no filters (so it takes its starting search, if one is set). By default, when the bar
   * shows none; a list that starts filtered (My Tasks on Not started) says, as its bar shows that filter.
   */
  isUnfiltered?: boolean;
}

// Dates are whole days, kept in the URL as yyyy-mm-dd; PowerSearch holds a day as its midnight UTC in seconds
// (with no timezoneID, it reads a date-only value as its UTC day).
const toSeconds = (date: string) => Date.parse(`${date}T00:00:00Z`) / 1000;
const toDate = (seconds: number) => new Date(seconds * 1000).toISOString().slice(0, 10);

const CONTAINS = 'contains';
const ON_OR_AFTER = 'on_or_after';
const ON_OR_BEFORE = 'on_or_before';
const IS = 'is';
const ANY_OF = 'any_of';
const NONE_OF = 'none_of';

function configFor(fields: ListFilterField[], textKey?: string): PowerSearchConfig {
  return {
    name: 'ListFilters',
    contentSearchFieldKey: textKey,
    fields: fields.map((field) => {
      switch (field.kind) {
        case 'text':
          return {
            key: field.key,
            label: field.label,
            defaultOperator: CONTAINS,
            operators: [{key: CONTAINS, label: 'contains', value: {type: 'string', isArbitraryStringAllowed: true}}],
          };
        case 'date':
          return {
            key: field.key,
            label: field.label,
            defaultOperator: ON_OR_AFTER,
            operators: [
              {key: ON_OR_AFTER, label: 'is on or after', value: {type: 'date_absolute'}},
              {key: ON_OR_BEFORE, label: 'is on or before', value: {type: 'date_absolute'}},
            ],
          };
        case 'choice':
          return {
            key: field.key,
            label: field.label,
            defaultOperator: IS,
            operators: [
              {key: IS, label: 'is', value: {type: 'enum', values: field.options}},
              ...(field.isMultiple
                ? [
                    {key: ANY_OF, label: 'is any of', value: {type: 'enum_list', values: field.options}} as const,
                    {key: NONE_OF, label: 'is not any of', value: {type: 'enum_list', values: field.options}} as const,
                  ]
                : []),
            ],
          };
        case 'flag':
          return {
            key: field.key,
            label: field.label,
            defaultOperator: IS,
            operators: [{key: IS, label: field.operator, value: {type: 'empty'}}],
          };
      }
    }),
  };
}

/** The filters the list's search holds: one per value in the URL. */
export function filtersFrom(fields: ListFilterField[], search: object): PowerSearchFilter[] {
  const filters: PowerSearchFilter[] = [];
  const values = search as Record<string, unknown>;
  const text = (key: string) => {
    const value = values[key];
    return typeof value === 'string' && value ? value : undefined;
  };
  for (const field of fields) {
    switch (field.kind) {
      case 'text': {
        const value = text(field.key);
        if (value) filters.push({field: field.key, operator: CONTAINS, value: {type: 'string', value}});
        break;
      }
      case 'choice': {
        const value = text(field.key);
        if (!value) break;
        const isNone = value.startsWith('!');
        const listed = (isNone ? value.slice(1) : value).split(',').filter(Boolean);
        if (isNone) filters.push({field: field.key, operator: NONE_OF, value: {type: 'enum_list', value: listed}});
        else if (listed.length > 1) filters.push({field: field.key, operator: ANY_OF, value: {type: 'enum_list', value: listed}});
        else filters.push({field: field.key, operator: IS, value: {type: 'enum', value: listed[0]}});
        break;
      }
      case 'date': {
        const from = text(field.fromKey);
        const to = text(field.toKey);
        if (from) filters.push({field: field.key, operator: ON_OR_AFTER, value: {type: 'date_absolute', unixSeconds: toSeconds(from)}});
        if (to) filters.push({field: field.key, operator: ON_OR_BEFORE, value: {type: 'date_absolute', unixSeconds: toSeconds(to)}});
        break;
      }
      case 'flag':
        if (values[field.key] === true) filters.push({field: field.key, operator: IS, value: {type: 'empty'}});
        break;
    }
  }
  return filters;
}

/** Every field's keys, cleared: what a change starts from, so a removed filter's key goes from the URL. */
function cleared(fields: ListFilterField[]): ListFilterValues {
  const values: ListFilterValues = {};
  for (const field of fields) {
    if (field.kind === 'date') values[field.fromKey] = values[field.toKey] = undefined;
    else values[field.key] = undefined;
  }
  return values;
}

/** The list's search for these filters: every field's keys, set or cleared. A later filter on the same value wins. */
export function valuesFrom(fields: ListFilterField[], filters: readonly PowerSearchFilter[]): ListFilterValues {
  const values = cleared(fields);
  for (const filter of filters) {
    const field = fields.find((f) => f.key === filter.field);
    const {value} = filter;
    if (!field) continue;
    if (field.kind === 'text' && value.type === 'string') values[field.key] = value.value.trim() || undefined;
    else if (field.kind === 'choice' && value.type === 'enum') values[field.key] = value.value || undefined;
    else if (field.kind === 'choice' && value.type === 'enum_list')
      values[field.key] = value.value.length ? `${filter.operator === NONE_OF ? '!' : ''}${value.value.join(',')}` : undefined;
    else if (field.kind === 'flag') values[field.key] = true;
    else if (field.kind === 'date' && value.type === 'date_absolute')
      values[filter.operator === ON_OR_BEFORE ? field.toKey : field.fromKey] = toDate(value.unixSeconds);
  }
  return values;
}

/**
 * A list's search as a power search bar (Astryx PowerSearch): each filter a token, "field: operator value"
 * (e.g. "First name: contains fre", "Status: is any of In progress, Blocked"), added by typing a field's name (or,
 * with `textKey`, by typing what to search for). The filters live in the list's search (the URL), like its page
 * and sort, so the bar shows what the list is filtered by however it got there (a link, or Back). Favourites, if
 * the list has them, are a star at the bar's end.
 *
 * Use it above a list's table, as wide as it (`DataTable`'s `search`), for the filters the list's API supports.
 */
export function ListFilterBar({fields, values, onChange, label, placeholder, textKey, list, favourites, isUnfiltered}: ListFilterBarProps) {
  const config = useMemo(() => configFor(fields, textKey), [fields, textKey]);
  const filters = filtersFrom(fields, values);
  const bar = useRef<HTMLElement>(null);
  useNoAutofill(bar);
  return (
    // The search fills the row; favourites sit beside it (inside it, a press would open its field list too).
    <HStack ref={bar} gap={2} vAlign="center" style={NO_FIELD_STATUS_LINE}>
      <StackItem size="fill">
        <PowerSearch
          config={config}
          filters={filters}
          onChange={(next) => onChange(valuesFrom(fields, next))}
          label={label}
          placeholder={placeholder}
          presentation="popover"
        />
      </StackItem>
      <ListFavourites
        list={list}
        quick={favourites}
        current={valuesFrom(fields, filters)}
        normalize={(saved) => valuesFrom(fields, filtersFrom(fields, saved))}
        onApply={(saved) => onChange({...cleared(fields), ...saved})}
        isUnfiltered={isUnfiltered ?? filters.length === 0}
      />
    </HStack>
  );
}

/**
 * What password managers and browsers look for to leave a text box alone: `autocomplete="off"` alone doesn't stop
 * them filling what looks like a login (e.g. a search of usernames) with a saved one.
 */
const NO_AUTOFILL = {
  autocomplete: 'off',
  'data-1p-ignore': 'true',
  'data-lpignore': 'true',
  'data-bwignore': 'true',
  'data-form-type': 'other',
};

/**
 * Marks every text box in the bar as not for autofill, including those its popups add later (a filter's value
 * box): PowerSearch has no props for its inputs' attributes.
 */
function useNoAutofill(bar: React.RefObject<HTMLElement | null>) {
  useEffect(() => {
    const root = bar.current;
    if (!root) return;
    const mark = () => {
      for (const input of root.querySelectorAll('input')) {
        for (const [name, value] of Object.entries(NO_AUTOFILL)) {
          if (input.getAttribute(name) !== value) input.setAttribute(name, value);
        }
      }
    };
    mark();
    if (typeof MutationObserver === 'undefined') return;
    const observer = new MutationObserver(mark);
    observer.observe(root, {childList: true, subtree: true});
    return () => observer.disconnect();
  }, [bar]);
}

