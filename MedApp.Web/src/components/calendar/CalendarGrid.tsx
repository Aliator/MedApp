import {Button} from '@astryxdesign/core/Button';
import {Card} from '@astryxdesign/core/Card';
import {ClickableCard} from '@astryxdesign/core/ClickableCard';
import {Icon} from '@astryxdesign/core/Icon';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {Tooltip} from '@astryxdesign/core/Tooltip';
import {VisuallyHidden} from '@astryxdesign/core/VisuallyHidden';
import {useEffect, useId, useLayoutEffect, useMemo, useRef, useState, useSyncExternalStore, type KeyboardEvent, type PointerEvent, type ReactElement, type Ref} from 'react';
import type {CalendarDay} from '../../api/calendar';
import {formatCalendarDay, formatShortDay, formatWeekday, formatTimeRange, intervalOf, isWeekday} from '../../utils/calendarSlots';
import {formatTime, parseUtc} from '../../utils/dates';
import {AppIcon} from '../shared/icons';
import type {CalendarGridItem} from './calendarItems';

/** How long each row of the grid is, in minutes: 15 (the default) or 5. */
export type CalendarRowMinutes = 15 | 5;

/** A stretch of free time in one column: what selecting on the grid gives, and where an item is moved to. */
export interface CalendarSelection {
  date: string;
  startUtc: string;
  endUtc: string;
  /**
   * The column it's in (its `key`): the grid always says, so a selection in a person's column in the team view means
   * that person. A selection without one is shown in the first column on its date.
   */
  column?: string;
}

/**
 * One column of the grid: a day of one person's calendar (`dayColumns` makes a day or a week of them), or a person
 * on one day (the team view), or any other stretch of one day's time with its own items.
 */
export interface CalendarColumn {
  /** Unique on the grid: the date for a day's column, a person's ID in the team view. */
  key: string;
  /**
   * Who or what the column is, shown in its heading and spoken first ("k.ashford" becomes "k.ashford, Tuesday 17
   * November"). Leave it out for a day of one person's calendar, which is headed and named by its day.
   */
  label?: string;
  /** The UK day it shows (`yyyy-mm-dd`). */
  date: string;
  /** That day: its working hours in UTC, and whether it's past, today and a working day. */
  day: CalendarDay;
  items: CalendarGridItem[];
  /**
   * Only these stretches can be selected (e.g. when everyone in the team view is free); the rest of the day shows as
   * unavailable. A column with it takes no items dropped on it. Without it, any time without an item is free.
   */
  freeTime?: {startUtc?: string; endUtc?: string}[];
}

/**
 * One person's days as columns, each with the items on its date: a day (one column) or a week. With `weekdaysOnly`
 * (a compact week), Saturday and Sunday are left out: nothing can be booked, blocked or met on them, and templates put
 * nothing there, so they're always empty. One that somehow has something on it is kept, so nothing is ever hidden.
 */
export function dayColumns(days: CalendarDay[], items: CalendarGridItem[], {weekdaysOnly = false}: {weekdaysOnly?: boolean} = {}): CalendarColumn[] {
  return days
    .map((day) => {
      const date = day.date ?? '';
      return {key: date, date, day, items: items.filter((i) => i.date === date)};
    })
    .filter((column) => !weekdaysOnly || isWeekday(column.date) || column.items.length > 0);
}

export interface CalendarGridProps {
  /** Names the grid for screen readers, e.g. "jsmith's calendar". */
  label: string;
  /**
   * The columns, in order: one person's day or week (`dayColumns`: a week runs Monday to Sunday, and its weekends show
   * as narrow, greyed columns; a compact week, Monday to Friday), or people side by side on one day. More than fit scroll sideways inside the grid, with
   * the hours staying put.
   */
  columns: CalendarColumn[];
  /** Opening an item (a click, or Enter on it), with the column it's in. Without it, items still show but do nothing. */
  onItemOpen?: (item: CalendarGridItem, column: CalendarColumn) => void;
  /**
   * Selecting free time: a click on an empty slot (15 minutes), a drag across slots, or the keyboard. With the slot
   * where it ended, e.g. to show a menu there. Without it, the grid is read-only: nothing can be selected.
   */
  onSelectRange?: (selection: CalendarSelection, anchor: HTMLElement | null) => void;
  /**
   * Moving an item marked `isMovable` by dragging it to another time or column; it keeps its length, in 15-minute
   * steps. `to.column` is the column it was dropped in.
   */
  onItemMove?: (item: CalendarGridItem, to: CalendarSelection) => void;
  /** Items move only within their own column (the team view, where a column is a person), never to another. */
  keepsItemsInColumn?: boolean;
  /** Free time to show as selected, e.g. while a menu for it is open. It's a button then, and gets `selectionRef`. */
  selection?: CalendarSelection | null;
  selectionRef?: Ref<HTMLElement>;
  /** Opening a day from its column's heading, e.g. a week view going to that day (day columns only, not labelled ones). */
  onDayOpen?: (date: string) => void;
  /**
   * Leaves out the row of headings, where something above already names the day (Home's day calendar). Each column
   * is still named for screen readers.
   */
  isHeadingHidden?: boolean;
  /**
   * Squeezed into a narrow window (the Calendar page's compact week): a narrower hour gutter, columns that may be
   * narrower, and every item on one line, cut short, its words in full in its tooltip and accessible name. Each day's
   * heading still names its full date to screen readers.
   */
  isCompact?: boolean;
  /**
   * How long each row is: 15 minutes (the default), or 5, when the rows stay as tall, so the day is three times taller
   * and scrolls inside the grid, with lighter quarter-hour times down the side. Selecting, the keyboard and dragging
   * all go a row at a time. Switching keeps the same time at the top.
   */
  rowMinutes?: CalendarRowMinutes;
}

const MINUTE = 60_000;
/** How much of today before now stays in view when the grid opens on it (at most a third of what's in view). */
const SCROLL_LEAD = 60 * MINUTE;
/** A working day (08:00 to 18:00), when a day doesn't say. */
const DEFAULT_DAY = 10 * 60 * MINUTE;

interface ColumnLayout {
  column: CalendarColumn;
  day: CalendarDay;
  date: string;
  /** How the column is spoken before a time: "Monday 23 November", or "k.ashford, Monday 23 November". */
  name: string;
  /** Not a working day, in a column of a person's own days: a narrow, greyed column. */
  isNarrow: boolean;
  start: number;
  rows: number;
  /** The time its rows cover, in ms: `rows` times the row length. */
  span: number;
  /** Free time can be selected in it: a working day that isn't past, on a grid that selects. */
  canSelect: boolean;
  /** Each row is taken: by an item, or outside the column's `freeTime`. */
  taken: boolean[];
  /** Each row is outside the column's `freeTime`, when it has one. */
  unavailable: boolean[];
}

/** Where an item sits in its column: rows from the top, and its share of the width beside items it overlaps. */
interface PlacedItem {
  item: CalendarGridItem;
  top: number;
  height: number;
  column: number;
  columns: number;
}

/** Items beside each other where they overlap: each cluster of overlapping items shares the width. */
function placeItems(items: CalendarGridItem[], layout: ColumnLayout): PlacedItem[] {
  const {span} = layout;
  const sorted = [...items]
    .map((item) => ({item, ...intervalOf(item.startUtc, item.endUtc)}))
    .sort((a, b) => a.start - b.start || b.end - a.end);
  const placed: PlacedItem[] = [];
  let cluster: {placed: PlacedItem; end: number}[] = [];
  let clusterEnd = -Infinity;
  const finish = () => {
    const columns = Math.max(0, ...cluster.map((c) => c.placed.column)) + 1;
    for (const c of cluster) c.placed.columns = columns;
    cluster = [];
  };
  for (const {item, start, end} of sorted) {
    if (start >= clusterEnd) finish();
    const used = new Set(cluster.filter((c) => c.end > start).map((c) => c.placed.column));
    let column = 0;
    while (used.has(column)) column++;
    const top = Math.max(0, (start - layout.start) / span);
    const bottom = Math.min(1, (end - layout.start) / span);
    const entry = {item, top, height: Math.max(bottom - top, 0), column, columns: 1};
    placed.push(entry);
    cluster.push({placed: entry, end});
    clusterEnd = Math.max(clusterEnd, end);
  }
  finish();
  return placed;
}

/** Ticks each minute, for `useNow`. */
function subscribeToMinutes(onTick: () => void) {
  const timer = window.setInterval(onTick, 15_000);
  return () => window.clearInterval(timer);
}

/** The time now, to the minute (the same value until the minute changes, as `useSyncExternalStore` needs). */
const minuteNow = () => Math.floor(Date.now() / MINUTE) * MINUTE;

/**
 * The time now, to the minute, kept up to date; null while rendering on the server and hydrating, so the server's
 * page and the browser's first render match, and anything that depends on it shows once in the browser.
 */
function useNow(): number | null {
  return useSyncExternalStore(subscribeToMinutes, minuteNow, () => null);
}

const iso = (ms: number) => new Date(ms).toISOString();

/** A slot on the grid: a column (by its index) and a row. */
interface Slot {
  col: number;
  row: number;
}

/** A selection being made: the slot it started from, and the one it reaches now. */
interface Pending {
  col: number;
  anchor: number;
  focus: number;
}

/** An item being dragged: where it was picked up, and where it would go. */
interface Drag {
  item: CalendarGridItem;
  /** The column it was picked up from. */
  from: number;
  pointerId: number;
  x: number;
  y: number;
  /** Rows between the item's start and the point it was picked up by. */
  grabRows: number;
  isMoving: boolean;
  to: Slot | null;
}

/** Whether a held selection is in a column: by its column's key, or (without one) the first column on its date. */
function isSelectionIn(selection: CalendarSelection, layouts: ColumnLayout[], col: number): boolean {
  if (selection.column !== undefined) return selection.column === layouts[col].column.key;
  return layouts.findIndex((l) => l.date === selection.date) === col;
}

/**
 * A member of staff's time as a grid (docs/calendar-plan.md, decisions 28 and 68): 08:00 to 18:00 in 15-minute rows
 * (or 5-minute ones, `rowMinutes`), hour labels down the side, appointments, blocked time and meetings placed by their times, in columns. A column is a day of
 * one person's calendar (a day or a week, from `dayColumns`) or a person on one day (the team view, where each column
 * is labelled with who it is). Past days and times are dimmed, today's columns are tinted, and a line shows the time
 * now (both once it's in the browser), drawn behind items so their text stays readable, with a dot at the column's
 * edge. A week's weekends show as narrow greyed columns that do nothing. A week's columns are headed by their days; a
 * single day, which the page around it already dates, by its weekday only; labelled columns by their labels. Columns
 * keep a minimum width: more than fit scroll sideways inside the grid, the hours staying put. A compact grid
 * (`isCompact`, with `dayColumns`' `weekdaysOnly`) fits a week into a narrow window: Monday to Friday, a narrower hour
 * gutter, and items on one line each.
 *
 * Use it for any calendar: the Calendar page (selecting free time, opening and dragging items, a person's day or
 * week, and the team view), Home's day calendar (read-only), or anything else that shows people's time. It knows
 * nothing of the API beyond the days: its items are generic (`calendarItems.ts` makes them from a calendar).
 *
 * Keyboard: the free slots are one tab stop. The arrow keys move between them (up and down a slot, left and right a
 * column), Shift with Up or Down selects more, Enter or Space selects, and Escape lets go. Items are buttons of their
 * own, so they're opened with Enter; they're moved from their dialogs, the alternative to dragging (WCAG 2.5.7).
 *
 * Astryx has no time grid, so its cells are plain elements styled with theme tokens; items are Astryx cards.
 */
export function CalendarGrid({
  label,
  columns,
  onItemOpen,
  onSelectRange,
  onItemMove,
  keepsItemsInColumn = false,
  selection,
  selectionRef,
  onDayOpen,
  isHeadingHidden = false,
  isCompact = false,
  rowMinutes = 15,
}: CalendarGridProps) {
  const now = useNow();
  const hintId = useId();
  const rootRef = useRef<HTMLDivElement>(null);
  const columnRefs = useRef<(HTMLDivElement | null)[]>([]);
  const canSelect = onSelectRange !== undefined;
  const SLOT = rowMinutes * MINUTE;

  const layouts = useMemo<ColumnLayout[]>(
    () =>
      columns.map((column) => {
        const {day} = column;
        const start = day.workStartUtc ? parseUtc(day.workStartUtc).getTime() : 0;
        const end = day.workEndUtc ? parseUtc(day.workEndUtc).getTime() : start + DEFAULT_DAY;
        const rows = Math.max(1, Math.round((end - start) / SLOT));
        const busy = column.items.map((i) => intervalOf(i.startUtc, i.endUtc));
        const free = column.freeTime?.filter((f) => f.startUtc && f.endUtc).map((f) => intervalOf(f.startUtc!, f.endUtc!));
        const unavailable = Array.from({length: rows}, (_, row) => {
          if (!free) return false;
          const slot = {start: start + row * SLOT, end: start + (row + 1) * SLOT};
          return !free.some((f) => f.start <= slot.start && slot.end <= f.end);
        });
        const taken = Array.from({length: rows}, (_, row) => {
          const slot = {start: start + row * SLOT, end: start + (row + 1) * SLOT};
          return unavailable[row] || busy.some((t) => t.start < slot.end && slot.start < t.end);
        });
        const dayName = formatCalendarDay(column.date);
        return {
          column,
          day,
          date: column.date,
          name: column.label ? `${column.label}, ${dayName}` : dayName,
          isNarrow: !day.isWorkingDay && !column.label,
          start,
          rows,
          span: rows * SLOT,
          canSelect: canSelect && !!day.isWorkingDay && !day.isPast,
          taken,
          unavailable,
        };
      }),
    [columns, canSelect, SLOT],
  );

  const placedByColumn = useMemo(() => layouts.map((l) => placeItems(l.column.items, l)), [layouts]);

  const nowMs = now ?? undefined;

  // Opens on the part of today that matters: once the time is known (after mount), with today shown and the working
  // day under way, it scrolls so the time now is in view, an hour before it at the top (or less, in a short window).
  // Otherwise it stays at the start of the working day. Only the first time; moving through days leaves it alone.
  const hasScrolledToNow = useRef(false);
  useEffect(() => {
    if (hasScrolledToNow.current || nowMs === undefined) return;
    hasScrolledToNow.current = true;
    const index = layouts.findIndex((l) => l.day.isToday);
    const scroller = rootRef.current?.querySelector<HTMLElement>('.mc-scroll');
    const column = columnRefs.current[index];
    if (index < 0 || !scroller || !column) return;
    const today = layouts[index];
    if (nowMs < today.start || nowMs >= today.start + today.span) return;
    const height = column.offsetHeight;
    const lead = (SCROLL_LEAD / today.span) * height;
    const top = ((nowMs - today.start) / today.span) * height - (scroller.clientHeight > 0 ? Math.min(lead, scroller.clientHeight / 3) : lead);
    if (top > 0) scroller.scrollTop = top;
  }, [nowMs, layouts]);

  // Where the view is, as a share of the day, so a change of row length keeps the same time at the top.
  const topShare = useRef(0);
  const onScroll = () => {
    const scroller = rootRef.current?.querySelector<HTMLElement>('.mc-scroll');
    const height = columnRefs.current[0]?.offsetHeight ?? 0;
    if (scroller && height > 0) topShare.current = scroller.scrollTop / height;
  };
  const shownRowMinutes = useRef(rowMinutes);
  useLayoutEffect(() => {
    if (shownRowMinutes.current === rowMinutes) return;
    shownRowMinutes.current = rowMinutes;
    const scroller = rootRef.current?.querySelector<HTMLElement>('.mc-scroll');
    const height = columnRefs.current[0]?.offsetHeight ?? 0;
    if (scroller && height > 0) scroller.scrollTop = topShare.current * height;
  }, [rowMinutes]);

  /** A slot that can be selected: free, in a column that can be, and not started yet. */
  const isFree = (col: number, row: number) => {
    const l = layouts[col];
    if (!l?.canSelect || row < 0 || row >= l.rows || l.taken[row]) return false;
    return nowMs === undefined || l.start + row * SLOT > nowMs;
  };

  const firstFree = (): Slot | null => {
    for (let col = 0; col < layouts.length; col++) {
      for (let row = 0; row < layouts[col].rows; row++) if (isFree(col, row)) return {col, row};
    }
    return null;
  };

  const [active, setActive] = useState<Slot | null>(null);
  const [pending, setPending] = useState<Pending | null>(null);
  const [drag, setDrag] = useState<Drag | null>(null);
  const isSelecting = useRef(false);
  const justDragged = useRef(false);

  // The tab stop: the slot last moved to, while it's still free; otherwise the first free one.
  const tabStop = active && isFree(active.col, active.row) ? active : firstFree();

  const slotElement = (slot: Slot) => rootRef.current?.querySelector<HTMLElement>(`[data-slot="${slot.col}-${slot.row}"]`) ?? null;

  const focusSlot = (slot: Slot) => {
    setActive(slot);
    slotElement(slot)?.focus();
  };

  /** The selection a pending one makes, when the slots between its ends are all free. */
  const rangeOf = (p: Pending): CalendarSelection => {
    const l = layouts[p.col];
    const first = Math.min(p.anchor, p.focus);
    const last = Math.max(p.anchor, p.focus);
    return {date: l.date, startUtc: iso(l.start + first * SLOT), endUtc: iso(l.start + (last + 1) * SLOT), column: l.column.key};
  };

  const commit = (p: Pending) => {
    setPending(null);
    onSelectRange?.(rangeOf(p), slotElement({col: p.col, row: p.focus}));
  };

  /** How far a selection can reach from `anchor` towards `row`: up to the last free slot before something taken. */
  const reach = (col: number, anchor: number, row: number) => {
    const step = row >= anchor ? 1 : -1;
    let reached = anchor;
    for (let r = anchor + step; step > 0 ? r <= row : r >= row; r += step) {
      if (!isFree(col, r)) break;
      reached = r;
    }
    return reached;
  };

  /** The free slot nearest a row in another column, looking further out each way. */
  const nearestFree = (col: number, row: number): number | null => {
    for (let distance = 0; distance < layouts[col].rows; distance++) {
      if (isFree(col, row + distance)) return row + distance;
      if (isFree(col, row - distance)) return row - distance;
    }
    return null;
  };

  const onSlotKeyDown = (event: KeyboardEvent, slot: Slot) => {
    const {col, row} = slot;
    const move = (to: Slot | null) => {
      event.preventDefault();
      if (to) focusSlot(to);
    };
    switch (event.key) {
      case 'ArrowUp':
      case 'ArrowDown': {
        const step = event.key === 'ArrowUp' ? -1 : 1;
        if (event.shiftKey) {
          event.preventDefault();
          const base = pending && pending.col === col ? pending : {col, anchor: row, focus: row};
          const focus = reach(col, base.anchor, base.focus + step);
          setPending({...base, focus});
          focusSlot({col, row: focus});
          return;
        }
        setPending(null);
        let next = row + step;
        while (next >= 0 && next < layouts[col].rows && !isFree(col, next)) next += step;
        return move(isFree(col, next) ? {col, row: next} : null);
      }
      case 'ArrowLeft':
      case 'ArrowRight': {
        setPending(null);
        const step = event.key === 'ArrowLeft' ? -1 : 1;
        for (let c = col + step; c >= 0 && c < layouts.length; c += step) {
          const near = nearestFree(c, row);
          if (near !== null) return move({col: c, row: near});
        }
        return move(null);
      }
      case 'Home':
      case 'End': {
        setPending(null);
        const rows = Array.from({length: layouts[col].rows}, (_, r) => r).filter((r) => isFree(col, r));
        return move(rows.length ? {col, row: event.key === 'Home' ? rows[0] : rows[rows.length - 1]} : null);
      }
      case 'Enter':
      case ' ':
        event.preventDefault();
        commit(pending && pending.col === col ? pending : {col, anchor: row, focus: row});
        return;
      case 'Escape':
        if (pending) {
          event.preventDefault();
          event.stopPropagation();
          setPending(null);
        }
        return;
    }
  };

  /** The row under a pointer in a column, or null when it isn't over the column. */
  const rowAt = (col: number, clientY: number): number | null => {
    const column = columnRefs.current[col];
    if (!column) return null;
    const box = column.getBoundingClientRect();
    if (box.height === 0) return null;
    return Math.floor(((clientY - box.top) / box.height) * layouts[col].rows);
  };

  /** The column a dragged item would be dropped in: one it may move to, a working day that isn't past. */
  const columnAt = (clientX: number, d: Drag): number | null => {
    for (let col = 0; col < layouts.length; col++) {
      const l = layouts[col];
      if (keepsItemsInColumn && col !== d.from) continue;
      if (d.item.movesWithinDay && l.date !== d.item.date) continue;
      if (l.column.freeTime) continue;
      const box = columnRefs.current[col]?.getBoundingClientRect();
      if (box && clientX >= box.left && clientX < box.right && l.day.isWorkingDay && !l.day.isPast) return col;
    }
    return null;
  };

  const onSlotPointerDown = (event: PointerEvent, slot: Slot) => {
    if (event.button !== 0) return;
    event.preventDefault();
    isSelecting.current = true;
    setActive(slot);
    setPending({col: slot.col, anchor: slot.row, focus: slot.row});
    columnRefs.current[slot.col]?.setPointerCapture?.(event.pointerId);
  };

  const onColumnPointerMove = (event: PointerEvent, col: number) => {
    if (!isSelecting.current || !pending || pending.col !== col) return;
    const row = rowAt(col, event.clientY);
    if (row === null) return;
    const focus = reach(col, pending.anchor, Math.max(0, Math.min(layouts[col].rows - 1, row)));
    if (focus !== pending.focus) setPending({...pending, focus});
  };

  const onColumnPointerUp = () => {
    if (!isSelecting.current) return;
    isSelecting.current = false;
    if (!pending) return;
    // Just after the browser has finished with this press: a menu opened from the selection (the Calendar page's Book
    // or Block) would otherwise take the release itself as a press outside it, and close at once.
    const selected = pending;
    window.setTimeout(() => commit(selected), 0);
  };

  // Dragging an item.
  const lengthRows = (item: CalendarGridItem) => {
    const t = intervalOf(item.startUtc, item.endUtc);
    return Math.max(1, Math.round((t.end - t.start) / SLOT));
  };

  const onItemPointerDown = (event: PointerEvent<HTMLElement>, item: CalendarGridItem, col: number) => {
    justDragged.current = false;
    if (event.button !== 0 || !onItemMove || !item.isMovable) return;
    const row = rowAt(col, event.clientY);
    const itemRow = Math.round((parseUtc(item.startUtc).getTime() - layouts[col].start) / SLOT);
    (event.currentTarget as HTMLElement).setPointerCapture?.(event.pointerId);
    setDrag({item, from: col, pointerId: event.pointerId, x: event.clientX, y: event.clientY, grabRows: row === null ? 0 : row - itemRow, isMoving: false, to: null});
  };

  const onItemPointerMove = (event: PointerEvent) => {
    if (!drag || event.pointerId !== drag.pointerId) return;
    const isMoving = drag.isMoving || Math.hypot(event.clientX - drag.x, event.clientY - drag.y) > 4;
    if (!isMoving) return;
    const col = columnAt(event.clientX, drag);
    let to: Drag['to'] = null;
    if (col !== null) {
      const row = rowAt(col, event.clientY);
      if (row !== null) {
        const length = lengthRows(drag.item);
        const target = Math.max(0, Math.min(layouts[col].rows - length, row - drag.grabRows));
        // Nothing moves into time that has started.
        if (nowMs === undefined || layouts[col].start + target * SLOT > nowMs) to = {col, row: target};
      }
    }
    setDrag({...drag, isMoving, to});
  };

  const onItemPointerUp = (event: PointerEvent) => {
    if (!drag || event.pointerId !== drag.pointerId) return;
    setDrag(null);
    if (!drag.isMoving) return;
    justDragged.current = true;
    const target = ghostOf(drag);
    if (!target) return;
    const isWhereItWas = target.column === layouts[drag.from].column.key && target.startUtc === iso(parseUtc(drag.item.startUtc).getTime());
    if (!isWhereItWas) onItemMove?.(drag.item, target);
  };

  /** Where a dragged item would go. */
  function ghostOf(d: Drag): CalendarSelection | null {
    if (!d.to) return null;
    const l = layouts[d.to.col];
    const start = l.start + d.to.row * SLOT;
    return {date: l.date, startUtc: iso(start), endUtc: iso(start + lengthRows(d.item) * SLOT), column: l.column.key};
  }

  // Escape drops a drag.
  useEffect(() => {
    if (!drag) return;
    const cancel = (event: globalThis.KeyboardEvent) => {
      if (event.key !== 'Escape') return;
      setDrag(null);
      // The click that ends the press doesn't open the item either.
      justDragged.current = drag.isMoving;
    };
    window.addEventListener('keydown', cancel);
    return () => window.removeEventListener('keydown', cancel);
  }, [drag]);

  const openItem = (item: CalendarGridItem, column: CalendarColumn) => {
    if (justDragged.current) {
      justDragged.current = false;
      return;
    }
    onItemOpen?.(item, column);
  };

  // The hours down the side, and with 5-minute rows the quarter hours between them, lighter.
  const hourLabels = useMemo(() => {
    const first = layouts.find((l) => l.day.isWorkingDay) ?? layouts[0];
    if (!first) return [];
    const every = (rowMinutes === 5 ? 15 : 60) / rowMinutes;
    return Array.from({length: Math.ceil(first.rows / every)}, (_, i) => {
      const row = i * every;
      return {row, text: formatTime(iso(first.start + row * SLOT)), isQuarter: (row * rowMinutes) % 60 !== 0};
    });
  }, [layouts, rowMinutes, SLOT]);

  const rowsShown = layouts[0]?.rows ?? DEFAULT_DAY / SLOT;
  const isOneDay = layouts.length === 1 && !layouts[0].column.label;
  const ghost = drag?.isMoving && drag.to ? {col: drag.to.col, range: ghostOf(drag)!} : null;
  // The narrowest the columns can be together, so more than fit scroll sideways rather than squeeze.
  const narrowCount = layouts.filter((l) => l.isNarrow).length;
  const labelledCount = layouts.filter((l) => !l.isNarrow && l.column.label).length;
  const dayCount = layouts.length - narrowCount - labelledCount;
  const minInline = `calc(var(--mc-gutter) + ${dayCount} * var(--mc-day-min) + ${labelledCount} * var(--mc-labelled-min) + ${narrowCount} * var(--mc-narrow))`;

  return (
    <VStack
      ref={rootRef}
      role="group"
      aria-label={label}
      height="100%"
      className="medapp-calendar"
      data-compact={isCompact || undefined}
      data-row-minutes={rowMinutes}
      style={{['--mc-rows' as string]: rowsShown, ['--mc-min-inline' as string]: minInline, minHeight: 0}}>
      <style>{GRID_CSS}</style>
      <VisuallyHidden id={hintId}>
        Arrow keys move between free times. Shift with the up or down arrow selects more. Enter selects.
      </VisuallyHidden>
      <div className="mc-scroll" onScroll={onScroll}>
        {!isHeadingHidden && (
          <div className="mc-head">
            <div className="mc-gutter" aria-hidden />
            {layouts.map((l) => {
              const weekend = !l.day.isWorkingDay;
              // A single day is dated by the page around it (its subtitle, or a card's title), so it's headed by its weekday.
              const name = l.column.label ?? (isOneDay ? formatWeekday(l.date) : formatShortDay(l.date));
              return (
                <div
                  key={l.column.key}
                  className="mc-head-cell"
                  data-narrow={l.isNarrow || undefined}
                  data-labelled={l.column.label ? true : undefined}
                  data-today={(l.day.isToday && !l.column.label) || undefined}>
                  {onDayOpen && !weekend && !isOneDay && !l.column.label ? (
                    <Button
                      label={name}
                      // Starts with the text people see (WCAG 2.5.3, label in name), so speech input finds it.
                      aria-label={`${name}${l.day.isToday ? ', today' : ''}: show ${isCompact ? formatCalendarDay(l.date) : 'the day'}`}
                      variant="ghost"
                      size="sm"
                      onClick={() => onDayOpen(l.date)}
                    />
                  ) : (
                    <Text
                      type={l.isNarrow ? 'supporting' : 'body'}
                      weight={l.isNarrow ? 'normal' : 'semibold'}
                      color={l.day.isToday && !l.column.label ? 'accent' : l.isNarrow ? 'secondary' : 'primary'}
                      maxLines={1}>
                      {l.isNarrow && !isOneDay ? formatShortDay(l.date).split(' ')[0] : name}
                    </Text>
                  )}
                  {l.day.isToday && !isOneDay && !l.column.label && <span className="mc-today-mark" aria-hidden />}
                </div>
              );
            })}
          </div>
        )}
        <div className="mc-body">
          <div className="mc-gutter" aria-hidden>
            {hourLabels.map((h) => (
              <span key={h.row} className="mc-hour" data-quarter={h.isQuarter || undefined} style={{top: `calc(var(--mc-row) * ${h.row})`}}>
                <Text type="supporting" color="secondary">
                  {h.text}
                </Text>
              </span>
            ))}
          </div>
          {layouts.map((l, col) => {
            const weekend = !l.day.isWorkingDay;
            const placed = placedByColumn[col];
            const nowTop =
              l.day.isToday && nowMs !== undefined && nowMs >= l.start && nowMs < l.start + l.span
                ? `${((nowMs - l.start) / l.span) * 100}%`
                : null;
            const shownSelection =
              pending && pending.col === col
                ? rangeOf(pending)
                : selection && !pending && isSelectionIn(selection, layouts, col)
                  ? selection
                  : null;
            return (
              <div
                key={l.column.key}
                ref={(element) => {
                  columnRefs.current[col] = element;
                }}
                className="mc-col"
                role="group"
                aria-label={weekend ? `${l.name}, not a working day` : `${l.name}${l.day.isToday ? ', today' : ''}`}
                data-narrow={l.isNarrow || undefined}
                data-weekend={weekend || undefined}
                data-labelled={l.column.label ? true : undefined}
                data-today={l.day.isToday || undefined}
                data-past={l.day.isPast || undefined}
                onPointerMove={(event) => {
                  onColumnPointerMove(event, col);
                }}
                onPointerUp={onColumnPointerUp}
                onPointerCancel={() => {
                  isSelecting.current = false;
                  setPending(null);
                }}>
                {weekend ? (
                  <span className="mc-weekend-text">
                    <Text type="supporting" color="secondary">
                      Not a working day
                    </Text>
                  </span>
                ) : (
                  Array.from({length: l.rows}, (_, row) => {
                    const start = l.start + row * SLOT;
                    // The line above the row: the hour's, the half hour's, a quarter's, or (5-minute rows) a fainter one.
                    const minute = row * rowMinutes;
                    const line = minute % 60 === 0 ? 'hour' : minute % 30 === 0 ? 'half' : minute % 15 === 0 ? 'quarter' : 'minor';
                    const isPast = l.day.isPast || (nowMs !== undefined && start + SLOT <= nowMs);
                    if (!isFree(col, row)) {
                      return (
                        <div
                          key={row}
                          className="mc-slot"
                          data-line={line}
                          data-past={isPast || undefined}
                          data-unavailable={l.unavailable[row] || undefined}
                          aria-hidden
                        />
                      );
                    }
                    const isStop = tabStop?.col === col && tabStop.row === row;
                    return (
                      <button
                        key={row}
                        type="button"
                        className="mc-slot"
                        data-line={line}
                        data-slot={`${col}-${row}`}
                        tabIndex={isStop ? 0 : -1}
                        aria-label={`${l.name}, ${formatTime(iso(start))}, free`}
                        aria-describedby={hintId}
                        onFocus={() => setActive({col, row})}
                        onKeyDown={(event) => onSlotKeyDown(event, {col, row})}
                        onPointerDown={(event) => onSlotPointerDown(event, {col, row})}
                      />
                    );
                  })
                )}
                {/* A column of only some free times (everyone free): each one marked, in words as well as by the
                    shading of the rest, so it never relies on colour alone. Presses go through to the slots. */}
                {!weekend &&
                  l.column.freeTime?.map((f) =>
                    f.startUtc && f.endUtc ? (
                      <span key={f.startUtc} className="mc-free" style={rangeStyle(l, f.startUtc, f.endUtc)} aria-hidden>
                        <Text type="supporting" color="secondary" maxLines={1}>
                          Free, {formatTimeRange(f.startUtc, f.endUtc)}
                        </Text>
                      </span>
                    ) : null,
                  )}
                {shownSelection &&
                  (pending ? (
                    // Being dragged out: just the highlight.
                    <span className="mc-selection" style={rangeStyle(l, shownSelection.startUtc, shownSelection.endUtc)}>
                      <Text type="supporting" weight="semibold" maxLines={1}>
                        {formatTimeRange(shownSelection.startUtc, shownSelection.endUtc)}
                      </Text>
                    </span>
                  ) : (
                    // Held: a button, as a menu opened from it (the Calendar page's Book or Block) is anchored to a button,
                    // which says it's open (`aria-expanded`). It isn't a tab stop, and presses go through it to the slots.
                    <button
                      ref={selectionRef as Ref<HTMLButtonElement>}
                      type="button"
                      tabIndex={-1}
                      className="mc-selection"
                      aria-label={`Selected time, ${formatTimeRange(shownSelection.startUtc, shownSelection.endUtc)}`}
                      style={rangeStyle(l, shownSelection.startUtc, shownSelection.endUtc)}>
                      <Text type="supporting" weight="semibold" maxLines={1}>
                        {formatTimeRange(shownSelection.startUtc, shownSelection.endUtc)}
                      </Text>
                    </button>
                  ))}
                {/* The time now: a line behind the items (before them, and under them in the CSS), so it never runs through their
                    text, and a dot at the column's edge over everything. */}
                {nowTop !== null && (
                  <>
                    <span className="mc-now" style={{top: nowTop}} aria-hidden />
                    <span className="mc-now-dot" style={{top: nowTop}} aria-hidden />
                  </>
                )}
                {placed.map((p) => (
                  <GridItem
                    key={p.item.key}
                    placed={p}
                    rows={l.rows}
                    isCompact={isCompact}
                    isDragged={drag?.isMoving === true && drag.from === col && drag.item.key === p.item.key}
                    onOpen={onItemOpen && !p.item.isReadOnly ? () => openItem(p.item, l.column) : undefined}
                    onPointerDown={onItemMove && p.item.isMovable ? (event) => onItemPointerDown(event, p.item, col) : undefined}
                    onPointerMove={onItemPointerMove}
                    onPointerUp={onItemPointerUp}
                  />
                ))}
                {ghost && ghost.col === col && (
                  <span className="mc-ghost" style={rangeStyle(l, ghost.range.startUtc, ghost.range.endUtc)} aria-hidden>
                    <Text type="supporting" weight="semibold" maxLines={1}>
                      {formatTimeRange(ghost.range.startUtc, ghost.range.endUtc)}
                    </Text>
                  </span>
                )}
              </div>
            );
          })}
        </div>
      </div>
    </VStack>
  );
}

/** A stretch of a day as a position in its column. */
function rangeStyle(l: ColumnLayout, startUtc: string, endUtc: string) {
  const {span} = l;
  const t = intervalOf(startUtc, endUtc);
  return {top: `${((t.start - l.start) / span) * 100}%`, height: `${((t.end - t.start) / span) * 100}%`};
}

/** A block's stripe colour: its activity's background, and half of it in dark mode. */
const stripe = (colour: string) =>
  `light-dark(var(--color-background-${colour}), color-mix(in srgb, var(--color-background-${colour}) 50%, transparent))`;

/**
 * How an item's text fits its height, in rows (15 minutes each): `tiny` (under 10 minutes) shows none, its words only in
 * its tooltip and accessible name; `short` (10 to 15 minutes) one small, tight line; otherwise one line a row.
 */
function itemFit(rowsTall: number): 'tiny' | 'short' | 'rows' {
  // A little leeway, so 10 minutes (two thirds of a row) counts as short whatever the rounding.
  if (rowsTall < 2 / 3 - 0.01) return 'tiny';
  return rowsTall < 1 - 0.01 ? 'short' : 'rows';
}

/**
 * One item: a card in its colour (appointments and meetings tinted, blocks striped and quieter), as many lines as fit,
 * always in the same order: its name, then what it's for, then its time. A meeting has its icon before its name, so
 * it never relies on colour alone. One under 10 minutes, too short for any text, has a tooltip. On a compact grid every
 * item is one line, cut short, with its words in full in a tooltip.
 */
function GridItem({
  placed,
  rows,
  isCompact,
  isDragged,
  onOpen,
  onPointerDown,
  onPointerMove,
  onPointerUp,
}: {
  placed: PlacedItem;
  rows: number;
  isCompact: boolean;
  isDragged: boolean;
  onOpen?: () => void;
  onPointerDown?: (event: PointerEvent<HTMLElement>) => void;
  onPointerMove: (event: PointerEvent) => void;
  onPointerUp: (event: PointerEvent) => void;
}) {
  const {item, top, height, column, columns} = placed;
  const fit = itemFit(height * rows);
  const lines = Math.max(1, Math.round(height * rows));
  const isTinted = item.kind !== 'block';
  const time = formatTimeRange(item.startUtc, item.endUtc);
  const style = {
    position: 'absolute' as const,
    top: `${top * 100}%`,
    // A hair shorter, so items one after another (10:00 to 10:30, then 10:30) don't run together.
    height: `calc(${height * 100}% - var(--spacing-0-5))`,
    insetInlineStart: `calc(${(column / columns) * 100}% + var(--spacing-0-5))`,
    inlineSize: `calc(${100 / columns}% - var(--spacing-1))`,
    zIndex: isTinted ? 2 : 1,
    padding: isCompact ? '0 var(--spacing-1)' : '0 var(--spacing-1-5)',
    overflow: 'hidden',
    borderInlineStart: `var(--spacing-1) solid var(--color-border-${item.colour})`,
    borderRadius: 'var(--radius-inner)',
    opacity: isDragged ? 0.5 : undefined,
    cursor: onPointerDown ? 'grab' : undefined,
    touchAction: onPointerDown ? 'none' : undefined,
    ...(isTinted
      ? {}
      : {
          // Quieter stripes in dark mode, where the full colour behind the text is busy.
          backgroundImage: `repeating-linear-gradient(135deg, ${stripe(item.colour)} 0 var(--spacing-1-5), transparent var(--spacing-1-5) var(--spacing-3))`,
        }),
  };
  // Before the name: a meeting's icon, and a warning on a flagged block.
  const marks = (
    <>
      {item.kind === 'meeting' && <Icon icon={AppIcon.Meeting} size="xsm" color="primary" />}
      {item.isFlagged && <Icon icon={AppIcon.Warning} size="xsm" color="warning" />}
    </>
  );
  // One line: the name, then what it's for, or the time when it says nothing more.
  const oneLine = (type: 'supporting' | 'inherit') => (
    <HStack gap={1} vAlign="center">
      {marks}
      <Text type={type} color="primary" maxLines={1}>
        <Text type="inherit" weight="semibold">
          {item.label}
        </Text>
        {item.sublabel ? `, ${item.sublabel}` : `, ${time}`}
      </Text>
    </HStack>
  );
  const content =
    fit === 'tiny' ? null : fit === 'short' ? (
      // Too short for a line of supporting text: a smaller one, set tight.
      <VStack gap={0} hAlign="stretch" className="mc-item-short">
        {oneLine('inherit')}
      </VStack>
    ) : (
      <VStack gap={0} hAlign="stretch">
        {lines === 1 || isCompact ? (
          oneLine('supporting')
        ) : (
          <>
            <HStack gap={1} vAlign="center">
              {marks}
              <Text type="supporting" color="primary" weight="semibold" maxLines={1}>
                {item.label}
              </Text>
            </HStack>
            {lines >= 3 && item.sublabel && (
              <Text type="supporting" color="primary" maxLines={Math.max(1, lines - 2)}>
                {item.sublabel}
              </Text>
            )}
            <Text type="supporting" color="primary" maxLines={1}>
              {lines === 2 && item.sublabel ? `${item.sublabel}, ${time}` : time}
            </Text>
          </>
        )}
      </VStack>
    );
  const variant = isTinted ? item.colour : 'default';
  // Too short for any text, or cut short on a compact grid: what it is shows on hover and focus.
  const withTooltip = (card: ReactElement) => (fit === 'tiny' || isCompact ? <Tooltip content={item.description}>{card}</Tooltip> : card);
  if (!onOpen) {
    // Nothing to open: shown as a plain card, named by its description.
    return withTooltip(
      <Card variant={variant} padding={0} data-kind={item.kind} data-fit={fit} style={style}>
        <VisuallyHidden>{item.description}</VisuallyHidden>
        <span aria-hidden>{content}</span>
      </Card>,
    );
  }
  return withTooltip(
    <ClickableCard
      label={item.description}
      variant={variant}
      padding={0}
      onClick={() => onOpen()}
      onPointerDown={onPointerDown}
      onPointerMove={onPointerDown ? onPointerMove : undefined}
      onPointerUp={onPointerDown ? onPointerUp : undefined}
      data-kind={item.kind}
      data-fit={fit}
      style={style}>
      {content}
    </ClickableCard>,
  );
}

/**
 * The grid's own rules, for what inline styles can't do (hover, focus, lines between rows, scrolling sideways with the
 * hours kept in view). Theme tokens only, and rem for the columns' minimum widths, so they grow with the text size.
 */
const GRID_CSS = `
.medapp-calendar { --mc-row: calc(var(--spacing-5) + var(--spacing-0-5)); --mc-gutter: var(--spacing-12); --mc-narrow: var(--spacing-10); --mc-day-min: 6rem; --mc-labelled-min: 8rem; }
.medapp-calendar[data-compact] { --mc-gutter: var(--spacing-10); --mc-day-min: 2.5rem; }
.medapp-calendar[data-compact] .mc-hour { inset-inline-end: var(--spacing-1); }
.medapp-calendar[data-compact] .mc-head-cell { padding-inline: 0; overflow: hidden; }
.medapp-calendar .mc-scroll { flex: 1 1 auto; min-height: 0; overflow: auto; scrollbar-gutter: stable; }
.medapp-calendar .mc-head { position: sticky; top: 0; z-index: 6; display: flex; min-inline-size: var(--mc-min-inline); background: var(--color-background-card); border-block-end: 1px solid var(--color-border-emphasized); }
.medapp-calendar .mc-head-cell { position: relative; flex: 1 1 0; min-inline-size: var(--mc-day-min); display: flex; align-items: center; justify-content: center; min-block-size: var(--spacing-9); padding-inline: var(--spacing-1); border-inline-start: 1px solid var(--color-border); }
.medapp-calendar .mc-head-cell[data-labelled], .medapp-calendar .mc-col[data-labelled] { min-inline-size: var(--mc-labelled-min); }
.medapp-calendar .mc-head-cell[data-narrow], .medapp-calendar .mc-col[data-narrow] { flex: 0 0 var(--mc-narrow); min-inline-size: 0; }
.medapp-calendar .mc-today-mark { position: absolute; inset-inline: var(--spacing-2); inset-block-end: 0; block-size: var(--spacing-0-5); border-radius: var(--radius-full); background: var(--color-accent); }
.medapp-calendar .mc-body { display: flex; min-inline-size: var(--mc-min-inline); }
.medapp-calendar .mc-gutter { position: sticky; inset-inline-start: 0; z-index: 5; flex: 0 0 var(--mc-gutter); background: var(--color-background-card); }
.medapp-calendar .mc-hour { position: absolute; inset-inline-end: var(--spacing-2); margin-block-start: var(--spacing-0-5); line-height: 1; }
.medapp-calendar .mc-col { position: relative; flex: 1 1 0; min-inline-size: var(--mc-day-min); block-size: calc(var(--mc-row) * var(--mc-rows)); border-inline-start: 1px solid var(--color-border); }
.medapp-calendar .mc-col[data-weekend] { background: var(--color-background-muted); display: flex; align-items: center; justify-content: center; }
.medapp-calendar .mc-col[data-today] { background: color-mix(in srgb, var(--color-accent) 5%, transparent); }
.medapp-calendar .mc-weekend-text { writing-mode: vertical-rl; transform: rotate(180deg); white-space: nowrap; }
.medapp-calendar .mc-slot { display: block; box-sizing: border-box; inline-size: 100%; block-size: var(--mc-row); margin: 0; padding: 0; border: 0; background: transparent; border-block-start: 1px dotted var(--color-border); }
.medapp-calendar .mc-slot[data-line="half"] { border-block-start-style: solid; }
.medapp-calendar .mc-slot[data-line="minor"] { border-block-start-color: color-mix(in srgb, var(--color-border) 50%, transparent); }
.medapp-calendar[data-row-minutes="5"] .mc-slot[data-line="quarter"] { border-block-start-style: solid; }
.medapp-calendar .mc-hour[data-quarter] { opacity: 0.7; }
.medapp-calendar .mc-hour[data-quarter] .astryx-text { font-size: var(--font-size-xs); }
.medapp-calendar .mc-slot[data-line="hour"] { border-block-start: 1px solid var(--color-border-emphasized); }
.medapp-calendar .mc-slot[data-past], .medapp-calendar .mc-slot[data-unavailable], .medapp-calendar .mc-col[data-past] .mc-slot { background: var(--color-background-muted); }
.medapp-calendar button.mc-slot { cursor: pointer; transition: background-color var(--duration-fast); }
.medapp-calendar button.mc-slot:hover { background: var(--color-overlay-hover); }
.medapp-calendar button.mc-slot:focus-visible { position: relative; z-index: 4; outline: var(--focus-outline-width) var(--focus-outline-style) var(--focus-outline-color); outline-offset: calc(-1 * var(--focus-outline-width)); }
.medapp-calendar .mc-free { position: absolute; inset-inline: var(--spacing-0-5); z-index: 0; box-sizing: border-box; padding-inline: var(--spacing-1-5); overflow: hidden; pointer-events: none; border: 1px dashed var(--color-border-emphasized); border-radius: var(--radius-inner); }
.medapp-calendar .mc-selection, .medapp-calendar .mc-ghost { position: absolute; inset-inline: var(--spacing-0-5); z-index: 3; box-sizing: border-box; padding-inline: var(--spacing-1-5); overflow: hidden; pointer-events: none; border-radius: var(--radius-inner); }
.medapp-calendar .mc-selection { background: var(--color-accent-muted); border: 1px solid var(--color-accent); }
.medapp-calendar button.mc-selection { display: flex; flex-direction: column; justify-content: flex-start; align-items: stretch; margin: 0; padding-block: 0; font: inherit; color: inherit; text-align: start; appearance: none; }
.medapp-calendar .mc-ghost { background: var(--color-background-popover); border: 2px dashed var(--color-accent); box-shadow: var(--shadow-med, none); }
.medapp-calendar .mc-item-short { font-size: var(--font-size-xs); line-height: 1; padding-block-start: var(--spacing-0-5); }
.medapp-calendar .mc-now { position: absolute; inset-inline: 0; z-index: 0; block-size: 2px; background: var(--color-icon-error); pointer-events: none; }
.medapp-calendar .mc-now-dot { position: absolute; inset-inline-start: calc(-1 * var(--spacing-1)); z-index: 4; margin-block-start: calc(-1 * var(--spacing-1) + 1px); inline-size: var(--spacing-2); block-size: var(--spacing-2); border-radius: var(--radius-full); background: var(--color-icon-error); pointer-events: none; }
`;
