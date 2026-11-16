import {Button} from '@astryxdesign/core/Button';
import {Card} from '@astryxdesign/core/Card';
import {ClickableCard} from '@astryxdesign/core/ClickableCard';
import {Icon} from '@astryxdesign/core/Icon';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {Tooltip} from '@astryxdesign/core/Tooltip';
import {VisuallyHidden} from '@astryxdesign/core/VisuallyHidden';
import {useEffect, useId, useMemo, useRef, useState, useSyncExternalStore, type KeyboardEvent, type PointerEvent, type ReactElement, type Ref} from 'react';
import type {CalendarDay} from '../../api/calendar';
import {formatCalendarDay, formatShortDay, formatWeekday, formatTimeRange, intervalOf, SLOT_MINUTES} from '../../utils/calendarSlots';
import {formatTime, parseUtc} from '../../utils/dates';
import {AppIcon} from '../shared/icons';
import type {CalendarGridItem} from './calendarItems';

/** A stretch of free time on one day: what selecting on the grid gives, and where an item is moved to. */
export interface CalendarSelection {
  date: string;
  startUtc: string;
  endUtc: string;
}

export interface CalendarGridProps {
  /** Names the grid for screen readers, e.g. "jsmith's calendar". */
  label: string;
  /** The days to show, in order: one (a day view) or a week, Monday to Sunday. Weekends show as narrow, greyed columns. */
  days: CalendarDay[];
  items: CalendarGridItem[];
  /** Opening an item (a click, or Enter on it). Without it, items still show but do nothing. */
  onItemOpen?: (item: CalendarGridItem) => void;
  /**
   * Selecting free time: a click on an empty slot (15 minutes), a drag across slots, or the keyboard. With the slot
   * where it ended, e.g. to show a menu there. Without it, the grid is read-only: nothing can be selected.
   */
  onSelectRange?: (selection: CalendarSelection, anchor: HTMLElement | null) => void;
  /** Moving an item marked `isMovable` by dragging it to another time or day; it keeps its length, in 15-minute steps. */
  onItemMove?: (item: CalendarGridItem, to: CalendarSelection) => void;
  /** Free time to show as selected, e.g. while a menu for it is open. Its outline gets `selectionRef`. */
  selection?: CalendarSelection | null;
  selectionRef?: Ref<HTMLElement>;
  /** Opening a day from its column's heading, e.g. a week view going to that day. */
  onDayOpen?: (date: string) => void;
  /**
   * Leaves out the row of day headings, where something above already names the day (Home's day calendar). Each
   * day's column is still named for screen readers.
   */
  isHeadingHidden?: boolean;
}

const MINUTE = 60_000;
const SLOT = SLOT_MINUTES * MINUTE;
/** How much of today before now stays in view when the grid opens on it. */
const SCROLL_LEAD = 60 * MINUTE;
/** Rows in a working day (08:00 to 18:00), when a day doesn't say. */
const DEFAULT_ROWS = 40;

interface DayLayout {
  day: CalendarDay;
  date: string;
  start: number;
  rows: number;
  /** Free time can be selected on it: a working day that isn't past, on a grid that selects. */
  canSelect: boolean;
  /** Each row is taken by an item. */
  taken: boolean[];
}

/** Where an item sits in its day: rows from the top, and its share of the width beside items it overlaps. */
interface PlacedItem {
  item: CalendarGridItem;
  top: number;
  height: number;
  column: number;
  columns: number;
}

/** Items beside each other where they overlap: each cluster of overlapping items shares the width. */
function placeItems(items: CalendarGridItem[], layout: DayLayout): PlacedItem[] {
  const span = layout.rows * SLOT;
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

/** A slot on the grid: a day (by its index) and a row. */
interface Slot {
  day: number;
  row: number;
}

/** A selection being made: the slot it started from, and the one it reaches now. */
interface Pending {
  day: number;
  anchor: number;
  focus: number;
}

/** An item being dragged: where it was picked up, and where it would go. */
interface Drag {
  item: CalendarGridItem;
  pointerId: number;
  x: number;
  y: number;
  /** Rows between the item's start and the point it was picked up by. */
  grabRows: number;
  isMoving: boolean;
  to: {day: number; row: number} | null;
}

/**
 * A day or a week of a member of staff's calendar as a time grid (docs/calendar-plan.md, decision 28): 08:00 to 18:00
 * in 15-minute rows, hour labels down the side, appointments and blocked time placed by their times. Past days and
 * times are dimmed, today's column is tinted, and a line shows the time now (both once it's in the browser), drawn
 * behind items so their text stays readable, with a dot at the column's edge. Weekends show as narrow greyed columns
 * that do nothing. A week's columns are headed by their days; a single day, which the page around it already dates,
 * by its weekday only.
 *
 * Use it for any calendar of days: the Calendar page (selecting free time, opening and dragging items), Home's day
 * calendar (read-only), or anything else that shows a person's time. It knows nothing of the API beyond the days: its
 * items are generic (`calendarItems.ts` makes them from a calendar).
 *
 * Keyboard: the free slots are one tab stop. The arrow keys move between them (up and down a slot, left and right a
 * day), Shift with Up or Down selects more, Enter or Space selects, and Escape lets go. Items are buttons of their own,
 * so they're opened with Enter; they're moved from their dialogs, the alternative to dragging (WCAG 2.5.7).
 *
 * Astryx has no time grid, so its cells are plain elements styled with theme tokens; items are Astryx cards.
 */
export function CalendarGrid({label, days, items, onItemOpen, onSelectRange, onItemMove, selection, selectionRef, onDayOpen, isHeadingHidden = false}: CalendarGridProps) {
  const now = useNow();
  const hintId = useId();
  const rootRef = useRef<HTMLDivElement>(null);
  const columnRefs = useRef<(HTMLDivElement | null)[]>([]);
  const canSelect = onSelectRange !== undefined;

  const layouts = useMemo<DayLayout[]>(
    () =>
      days.map((day) => {
        const start = day.workStartUtc ? parseUtc(day.workStartUtc).getTime() : 0;
        const end = day.workEndUtc ? parseUtc(day.workEndUtc).getTime() : start + DEFAULT_ROWS * SLOT;
        const rows = Math.max(1, Math.round((end - start) / SLOT));
        const dayItems = items.filter((i) => i.date === day.date);
        const taken = Array.from({length: rows}, (_, row) => {
          const slot = {start: start + row * SLOT, end: start + (row + 1) * SLOT};
          return dayItems.some((i) => {
            const t = intervalOf(i.startUtc, i.endUtc);
            return t.start < slot.end && slot.start < t.end;
          });
        });
        return {day, date: day.date ?? '', start, rows, canSelect: canSelect && !!day.isWorkingDay && !day.isPast, taken};
      }),
    [days, items, canSelect],
  );

  const placedByDay = useMemo(() => layouts.map((l) => placeItems(items.filter((i) => i.date === l.date), l)), [layouts, items]);

  const nowMs = now ?? undefined;

  // Opens on the part of today that matters: once the time is known (after mount), with today shown and the day under
  // way, it scrolls so an hour before now is at the top. Only the first time; moving through days leaves it alone.
  const hasScrolledToNow = useRef(false);
  useEffect(() => {
    if (hasScrolledToNow.current || nowMs === undefined) return;
    hasScrolledToNow.current = true;
    const index = layouts.findIndex((l) => l.day.isToday);
    const scroller = rootRef.current?.querySelector<HTMLElement>('.mc-scroll');
    const column = columnRefs.current[index];
    if (index < 0 || !scroller || !column) return;
    const today = layouts[index];
    const fraction = (nowMs - SCROLL_LEAD - today.start) / (today.rows * SLOT);
    if (fraction > 0) scroller.scrollTop = Math.min(fraction, 1) * column.offsetHeight;
  }, [nowMs, layouts]);

  /** A slot that can be selected: free, on a day that can be, and not started yet. */
  const isFree = (day: number, row: number) => {
    const l = layouts[day];
    if (!l?.canSelect || row < 0 || row >= l.rows || l.taken[row]) return false;
    return nowMs === undefined || l.start + row * SLOT > nowMs;
  };

  const firstFree = (): Slot | null => {
    for (let day = 0; day < layouts.length; day++) {
      for (let row = 0; row < layouts[day].rows; row++) if (isFree(day, row)) return {day, row};
    }
    return null;
  };

  const [active, setActive] = useState<Slot | null>(null);
  const [pending, setPending] = useState<Pending | null>(null);
  const [drag, setDrag] = useState<Drag | null>(null);
  const isSelecting = useRef(false);
  const justDragged = useRef(false);

  // The tab stop: the slot last moved to, while it's still free; otherwise the first free one.
  const tabStop = active && isFree(active.day, active.row) ? active : firstFree();

  const slotElement = (slot: Slot) => rootRef.current?.querySelector<HTMLElement>(`[data-slot="${slot.day}-${slot.row}"]`) ?? null;

  const focusSlot = (slot: Slot) => {
    setActive(slot);
    slotElement(slot)?.focus();
  };

  /** The selection a pending one makes, when the slots between its ends are all free. */
  const rangeOf = (p: Pending): CalendarSelection => {
    const l = layouts[p.day];
    const first = Math.min(p.anchor, p.focus);
    const last = Math.max(p.anchor, p.focus);
    return {date: l.date, startUtc: iso(l.start + first * SLOT), endUtc: iso(l.start + (last + 1) * SLOT)};
  };

  const commit = (p: Pending) => {
    setPending(null);
    onSelectRange?.(rangeOf(p), slotElement({day: p.day, row: p.focus}));
  };

  /** How far a selection can reach from `anchor` towards `row`: up to the last free slot before something taken. */
  const reach = (day: number, anchor: number, row: number) => {
    const step = row >= anchor ? 1 : -1;
    let reached = anchor;
    for (let r = anchor + step; step > 0 ? r <= row : r >= row; r += step) {
      if (!isFree(day, r)) break;
      reached = r;
    }
    return reached;
  };

  /** The free slot nearest a row on another day, looking further out each way. */
  const nearestFree = (day: number, row: number): number | null => {
    for (let distance = 0; distance < layouts[day].rows; distance++) {
      if (isFree(day, row + distance)) return row + distance;
      if (isFree(day, row - distance)) return row - distance;
    }
    return null;
  };

  const onSlotKeyDown = (event: KeyboardEvent, slot: Slot) => {
    const {day, row} = slot;
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
          const base = pending && pending.day === day ? pending : {day, anchor: row, focus: row};
          const focus = reach(day, base.anchor, base.focus + step);
          setPending({...base, focus});
          focusSlot({day, row: focus});
          return;
        }
        setPending(null);
        let next = row + step;
        while (next >= 0 && next < layouts[day].rows && !isFree(day, next)) next += step;
        return move(isFree(day, next) ? {day, row: next} : null);
      }
      case 'ArrowLeft':
      case 'ArrowRight': {
        setPending(null);
        const step = event.key === 'ArrowLeft' ? -1 : 1;
        for (let d = day + step; d >= 0 && d < layouts.length; d += step) {
          const near = nearestFree(d, row);
          if (near !== null) return move({day: d, row: near});
        }
        return move(null);
      }
      case 'Home':
      case 'End': {
        setPending(null);
        const rows = Array.from({length: layouts[day].rows}, (_, r) => r).filter((r) => isFree(day, r));
        return move(rows.length ? {day, row: event.key === 'Home' ? rows[0] : rows[rows.length - 1]} : null);
      }
      case 'Enter':
      case ' ':
        event.preventDefault();
        commit(pending && pending.day === day ? pending : {day, anchor: row, focus: row});
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

  /** The row under a pointer in a day's column, or null when it isn't over the column. */
  const rowAt = (day: number, clientY: number): number | null => {
    const column = columnRefs.current[day];
    if (!column) return null;
    const box = column.getBoundingClientRect();
    if (box.height === 0) return null;
    return Math.floor(((clientY - box.top) / box.height) * layouts[day].rows);
  };

  const dayAt = (clientX: number, item: CalendarGridItem): number | null => {
    for (let day = 0; day < layouts.length; day++) {
      if (item.movesWithinDay && layouts[day].date !== item.date) continue;
      const box = columnRefs.current[day]?.getBoundingClientRect();
      if (box && clientX >= box.left && clientX < box.right && layouts[day].day.isWorkingDay && !layouts[day].day.isPast) return day;
    }
    return null;
  };

  const onSlotPointerDown = (event: PointerEvent, slot: Slot) => {
    if (event.button !== 0) return;
    event.preventDefault();
    isSelecting.current = true;
    setActive(slot);
    setPending({day: slot.day, anchor: slot.row, focus: slot.row});
    columnRefs.current[slot.day]?.setPointerCapture?.(event.pointerId);
  };

  const onColumnPointerMove = (event: PointerEvent, day: number) => {
    if (!isSelecting.current || !pending || pending.day !== day) return;
    const row = rowAt(day, event.clientY);
    if (row === null) return;
    const focus = reach(day, pending.anchor, Math.max(0, Math.min(layouts[day].rows - 1, row)));
    if (focus !== pending.focus) setPending({...pending, focus});
  };

  const onColumnPointerUp = () => {
    if (!isSelecting.current) return;
    isSelecting.current = false;
    if (!pending) return;
    // Just after the browser has finished with this press: a menu opened from the selection (the Calendar page's Book
    // or Block) would otherwise take the release, and the click after it, as a press outside it, and close at once.
    const selected = pending;
    window.setTimeout(() => commit(selected), 0);
  };

  // Dragging an item.
  const lengthRows = (item: CalendarGridItem) => {
    const t = intervalOf(item.startUtc, item.endUtc);
    return Math.max(1, Math.round((t.end - t.start) / SLOT));
  };

  const onItemPointerDown = (event: PointerEvent<HTMLElement>, item: CalendarGridItem, day: number) => {
    justDragged.current = false;
    if (event.button !== 0 || !onItemMove || !item.isMovable) return;
    const row = rowAt(day, event.clientY);
    const itemRow = Math.round((parseUtc(item.startUtc).getTime() - layouts[day].start) / SLOT);
    (event.currentTarget as HTMLElement).setPointerCapture?.(event.pointerId);
    setDrag({item, pointerId: event.pointerId, x: event.clientX, y: event.clientY, grabRows: row === null ? 0 : row - itemRow, isMoving: false, to: null});
  };

  const onItemPointerMove = (event: PointerEvent) => {
    if (!drag || event.pointerId !== drag.pointerId) return;
    const isMoving = drag.isMoving || Math.hypot(event.clientX - drag.x, event.clientY - drag.y) > 4;
    if (!isMoving) return;
    const day = dayAt(event.clientX, drag.item);
    let to: Drag['to'] = null;
    if (day !== null) {
      const row = rowAt(day, event.clientY);
      if (row !== null) {
        const length = lengthRows(drag.item);
        const target = Math.max(0, Math.min(layouts[day].rows - length, row - drag.grabRows));
        // Nothing moves into time that has started.
        if (nowMs === undefined || layouts[day].start + target * SLOT > nowMs) to = {day, row: target};
      }
    }
    setDrag({...drag, isMoving, to});
  };

  const onItemPointerUp = (event: PointerEvent) => {
    if (!drag || event.pointerId !== drag.pointerId) return;
    setDrag(null);
    if (!drag.isMoving) return;
    justDragged.current = true;
    if (!drag.to) return;
    const target = ghostOf(drag);
    if (target && !(target.date === drag.item.date && target.startUtc === iso(parseUtc(drag.item.startUtc).getTime()))) onItemMove?.(drag.item, target);
  };

  /** Where a dragged item would go. */
  function ghostOf(d: Drag): CalendarSelection | null {
    if (!d.to) return null;
    const l = layouts[d.to.day];
    const start = l.start + d.to.row * SLOT;
    return {date: l.date, startUtc: iso(start), endUtc: iso(start + lengthRows(d.item) * SLOT)};
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

  const openItem = (item: CalendarGridItem) => {
    if (justDragged.current) {
      justDragged.current = false;
      return;
    }
    onItemOpen?.(item);
  };

  const hourLabels = useMemo(() => {
    const first = layouts.find((l) => l.day.isWorkingDay) ?? layouts[0];
    if (!first) return [];
    return Array.from({length: Math.ceil(first.rows / 4)}, (_, hour) => ({row: hour * 4, text: formatTime(iso(first.start + hour * 4 * SLOT))}));
  }, [layouts]);

  const rowsShown = layouts[0]?.rows ?? DEFAULT_ROWS;
  const isOneDay = layouts.length === 1;
  const ghost = drag?.isMoving ? ghostOf(drag) : null;

  return (
    <VStack
      ref={rootRef}
      role="group"
      aria-label={label}
      height="100%"
      className="medapp-calendar"
      style={{['--mc-rows' as string]: rowsShown, minHeight: 0}}>
      <style>{GRID_CSS}</style>
      <VisuallyHidden id={hintId}>
        Arrow keys move between free times. Shift with the up or down arrow selects more. Enter selects.
      </VisuallyHidden>
      <div className="mc-scroll">
        {!isHeadingHidden && (
          <div className="mc-head">
            <div className="mc-gutter" aria-hidden />
            {layouts.map((l) => {
              const weekend = !l.day.isWorkingDay;
              // A single day is dated by the page around it (its subtitle, or a card's title), so it's headed by its weekday.
              const name = isOneDay ? formatWeekday(l.date) : formatShortDay(l.date);
              return (
                <div key={l.date} className="mc-head-cell" data-weekend={weekend || undefined} data-today={l.day.isToday || undefined}>
                  {onDayOpen && !weekend && !isOneDay ? (
                    <Button
                      label={name}
                      // Starts with the text people see (WCAG 2.5.3, label in name), so speech input finds it.
                      aria-label={`${name}${l.day.isToday ? ', today' : ''}: show the day`}
                      variant="ghost"
                      size="sm"
                      onClick={() => onDayOpen(l.date)}
                    />
                  ) : (
                    <Text type={weekend ? 'supporting' : 'body'} weight={weekend ? 'normal' : 'semibold'} color={l.day.isToday ? 'accent' : weekend ? 'secondary' : 'primary'} maxLines={1}>
                      {weekend && !isOneDay ? formatShortDay(l.date).split(' ')[0] : name}
                    </Text>
                  )}
                  {l.day.isToday && !isOneDay && <span className="mc-today-mark" aria-hidden />}
                </div>
              );
            })}
          </div>
        )}
        <div className="mc-body">
          <div className="mc-gutter" aria-hidden>
            {hourLabels.map((h) => (
              <span key={h.row} className="mc-hour" style={{top: `calc(var(--mc-row) * ${h.row})`}}>
                <Text type="supporting" color="secondary">
                  {h.text}
                </Text>
              </span>
            ))}
          </div>
          {layouts.map((l, day) => {
            const weekend = !l.day.isWorkingDay;
            const dayName = formatCalendarDay(l.date);
            const placed = placedByDay[day];
            const nowTop =
              l.day.isToday && nowMs !== undefined && nowMs >= l.start && nowMs < l.start + l.rows * SLOT
                ? `${((nowMs - l.start) / (l.rows * SLOT)) * 100}%`
                : null;
            const shownSelection =
              pending && pending.day === day
                ? rangeOf(pending)
                : selection && selection.date === l.date && !pending
                  ? selection
                  : null;
            return (
              <div
                key={l.date}
                ref={(element) => {
                  columnRefs.current[day] = element;
                }}
                className="mc-col"
                role="group"
                aria-label={weekend ? `${dayName}, not a working day` : `${dayName}${l.day.isToday ? ', today' : ''}`}
                data-weekend={weekend || undefined}
                data-today={l.day.isToday || undefined}
                data-past={l.day.isPast || undefined}
                onPointerMove={(event) => {
                  onColumnPointerMove(event, day);
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
                    const line = row % 4 === 0 ? 'hour' : row % 2 === 0 ? 'half' : 'quarter';
                    const isPast = l.day.isPast || (nowMs !== undefined && start + SLOT <= nowMs);
                    if (!isFree(day, row)) {
                      return <div key={row} className="mc-slot" data-line={line} data-past={isPast || undefined} aria-hidden />;
                    }
                    const isStop = tabStop?.day === day && tabStop.row === row;
                    return (
                      <button
                        key={row}
                        type="button"
                        className="mc-slot"
                        data-line={line}
                        data-slot={`${day}-${row}`}
                        tabIndex={isStop ? 0 : -1}
                        aria-label={`${dayName}, ${formatTime(iso(start))}, free`}
                        aria-describedby={hintId}
                        onFocus={() => setActive({day, row})}
                        onKeyDown={(event) => onSlotKeyDown(event, {day, row})}
                        onPointerDown={(event) => onSlotPointerDown(event, {day, row})}
                      />
                    );
                  })
                )}
                {shownSelection && (
                  <span
                    ref={!pending ? (selectionRef as Ref<HTMLSpanElement>) : undefined}
                    className="mc-selection"
                    style={rangeStyle(l, shownSelection.startUtc, shownSelection.endUtc)}>
                    <Text type="supporting" weight="semibold" maxLines={1}>
                      {formatTimeRange(shownSelection.startUtc, shownSelection.endUtc)}
                    </Text>
                  </span>
                )}
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
                    isDragged={drag?.isMoving === true && drag.item.key === p.item.key}
                    onOpen={onItemOpen && !p.item.isReadOnly ? () => openItem(p.item) : undefined}
                    onPointerDown={onItemMove && p.item.isMovable ? (event) => onItemPointerDown(event, p.item, day) : undefined}
                    onPointerMove={onItemPointerMove}
                    onPointerUp={onItemPointerUp}
                  />
                ))}
                {ghost && ghost.date === l.date && (
                  <span className="mc-ghost" style={rangeStyle(l, ghost.startUtc, ghost.endUtc)} aria-hidden>
                    <Text type="supporting" weight="semibold" maxLines={1}>
                      {formatTimeRange(ghost.startUtc, ghost.endUtc)}
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
function rangeStyle(l: DayLayout, startUtc: string, endUtc: string) {
  const span = l.rows * SLOT;
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
 * One item: a card in its colour (appointments tinted, blocks striped and quieter), as many lines as fit, always in the
 * same order: its name, then what it's for, then its time. One under 10 minutes, too short for any text, has a tooltip.
 */
function GridItem({
  placed,
  rows,
  isDragged,
  onOpen,
  onPointerDown,
  onPointerMove,
  onPointerUp,
}: {
  placed: PlacedItem;
  rows: number;
  isDragged: boolean;
  onOpen?: () => void;
  onPointerDown?: (event: PointerEvent<HTMLElement>) => void;
  onPointerMove: (event: PointerEvent) => void;
  onPointerUp: (event: PointerEvent) => void;
}) {
  const {item, top, height, column, columns} = placed;
  const fit = itemFit(height * rows);
  const lines = Math.max(1, Math.round(height * rows));
  const isAppointment = item.kind === 'appointment';
  const time = formatTimeRange(item.startUtc, item.endUtc);
  const style = {
    position: 'absolute' as const,
    top: `${top * 100}%`,
    // A hair shorter, so items one after another (10:00 to 10:30, then 10:30) don't run together.
    height: `calc(${height * 100}% - var(--spacing-0-5))`,
    insetInlineStart: `calc(${(column / columns) * 100}% + var(--spacing-0-5))`,
    inlineSize: `calc(${100 / columns}% - var(--spacing-1))`,
    zIndex: isAppointment ? 2 : 1,
    padding: '0 var(--spacing-1-5)',
    overflow: 'hidden',
    borderInlineStart: `var(--spacing-1) solid var(--color-border-${item.colour})`,
    borderRadius: 'var(--radius-inner)',
    opacity: isDragged ? 0.5 : undefined,
    cursor: onPointerDown ? 'grab' : undefined,
    touchAction: onPointerDown ? 'none' : undefined,
    ...(isAppointment
      ? {}
      : {
          // Quieter stripes in dark mode, where the full colour behind the text is busy.
          backgroundImage: `repeating-linear-gradient(135deg, ${stripe(item.colour)} 0 var(--spacing-1-5), transparent var(--spacing-1-5) var(--spacing-3))`,
        }),
  };
  const flag = item.isFlagged && <Icon icon={AppIcon.Warning} size="xsm" color="warning" />;
  // One line: the name, then what it's for, or the time when it says nothing more.
  const oneLine = (type: 'supporting' | 'inherit') => (
    <HStack gap={1} vAlign="center">
      {flag}
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
        {lines === 1 ? (
          oneLine('supporting')
        ) : (
          <>
            <HStack gap={1} vAlign="center">
              {flag}
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
  const variant = isAppointment ? item.colour : 'default';
  // Too short for any text: what it is shows on hover and focus.
  const withTooltip = (card: ReactElement) => (fit === 'tiny' ? <Tooltip content={item.description}>{card}</Tooltip> : card);
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

/** The grid's own rules, for what inline styles can't do (hover, focus, lines between rows). Theme tokens only. */
const GRID_CSS = `
.medapp-calendar { --mc-row: calc(var(--spacing-5) + var(--spacing-0-5)); }
.medapp-calendar .mc-scroll { flex: 1 1 auto; min-height: 0; overflow: auto; scrollbar-gutter: stable; }
.medapp-calendar .mc-head { position: sticky; top: 0; z-index: 5; display: flex; background: var(--color-background-card); border-block-end: 1px solid var(--color-border-emphasized); }
.medapp-calendar .mc-head-cell { position: relative; flex: 1 1 0; min-inline-size: 0; display: flex; align-items: center; justify-content: center; min-block-size: var(--spacing-9); padding-inline: var(--spacing-1); border-inline-start: 1px solid var(--color-border); }
.medapp-calendar .mc-head-cell[data-weekend], .medapp-calendar .mc-col[data-weekend] { flex: 0 0 var(--spacing-10); }
.medapp-calendar .mc-today-mark { position: absolute; inset-inline: var(--spacing-2); inset-block-end: 0; block-size: var(--spacing-0-5); border-radius: var(--radius-full); background: var(--color-accent); }
.medapp-calendar .mc-body { display: flex; }
.medapp-calendar .mc-gutter { position: relative; flex: 0 0 var(--spacing-12); }
.medapp-calendar .mc-hour { position: absolute; inset-inline-end: var(--spacing-2); margin-block-start: var(--spacing-0-5); line-height: 1; }
.medapp-calendar .mc-col { position: relative; flex: 1 1 0; min-inline-size: 0; block-size: calc(var(--mc-row) * var(--mc-rows)); border-inline-start: 1px solid var(--color-border); }
.medapp-calendar .mc-col[data-weekend] { background: var(--color-background-muted); display: flex; align-items: center; justify-content: center; }
.medapp-calendar .mc-col[data-today] { background: color-mix(in srgb, var(--color-accent) 5%, transparent); }
.medapp-calendar .mc-weekend-text { writing-mode: vertical-rl; transform: rotate(180deg); white-space: nowrap; }
.medapp-calendar .mc-slot { display: block; box-sizing: border-box; inline-size: 100%; block-size: var(--mc-row); margin: 0; padding: 0; border: 0; background: transparent; border-block-start: 1px dotted var(--color-border); }
.medapp-calendar .mc-slot[data-line="half"] { border-block-start-style: solid; }
.medapp-calendar .mc-slot[data-line="hour"] { border-block-start: 1px solid var(--color-border-emphasized); }
.medapp-calendar .mc-slot[data-past], .medapp-calendar .mc-col[data-past] .mc-slot { background: var(--color-background-muted); }
.medapp-calendar button.mc-slot { cursor: pointer; transition: background-color var(--duration-fast); }
.medapp-calendar button.mc-slot:hover { background: var(--color-overlay-hover); }
.medapp-calendar button.mc-slot:focus-visible { position: relative; z-index: 4; outline: var(--focus-outline-width) var(--focus-outline-style) var(--focus-outline-color); outline-offset: calc(-1 * var(--focus-outline-width)); }
.medapp-calendar .mc-selection, .medapp-calendar .mc-ghost { position: absolute; inset-inline: var(--spacing-0-5); z-index: 3; box-sizing: border-box; padding-inline: var(--spacing-1-5); overflow: hidden; pointer-events: none; border-radius: var(--radius-inner); }
.medapp-calendar .mc-selection { background: var(--color-accent-muted); border: 1px solid var(--color-accent); }
.medapp-calendar .mc-ghost { background: var(--color-background-popover); border: 2px dashed var(--color-accent); box-shadow: var(--shadow-med, none); }
.medapp-calendar .mc-item-short { font-size: var(--font-size-xs); line-height: 1; padding-block-start: var(--spacing-0-5); }
.medapp-calendar .mc-now { position: absolute; inset-inline: 0; z-index: 0; block-size: 2px; background: var(--color-icon-error); pointer-events: none; }
.medapp-calendar .mc-now-dot { position: absolute; inset-inline-start: calc(-1 * var(--spacing-1)); z-index: 4; margin-block-start: calc(-1 * var(--spacing-1) + 1px); inline-size: var(--spacing-2); block-size: var(--spacing-2); border-radius: var(--radius-full); background: var(--color-icon-error); pointer-events: none; }
`;
