import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {IconButton} from '@astryxdesign/core/IconButton';
import {Popover} from '@astryxdesign/core/Popover';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useEffect, useRef, useState, type FormEvent} from 'react';
import {NO_STATUS_LINE} from '../forms/fieldSpace';
import {AppIcon} from '../shared/icons';

export interface PageControlsProps {
  /** The current page, from 1. */
  page: number;
  totalPages: number;
  onPageChange: (page: number) => void;
  /** Names the controls for screen readers, e.g. "Table pagination". */
  label: string;
}

/** A gap in the page numbers, shown as "…". */
const GAP = 'gap';

/**
 * The pages to show: all of them when there are few, otherwise the first, the last, and the current one with one
 * either side, with a gap ("…") where pages are left out. Always seven slots or fewer, so the controls keep their
 * width as the page changes.
 */
export function pageRange(page: number, totalPages: number): (number | typeof GAP)[] {
  const all = (from: number, to: number) => Array.from({length: to - from + 1}, (_, i) => from + i);
  if (totalPages <= 7) return all(1, totalPages);
  if (page <= 4) return [...all(1, 5), GAP, totalPages];
  if (page >= totalPages - 3) return [1, GAP, ...all(totalPages - 4, totalPages)];
  return [1, GAP, page - 1, page, page + 1, GAP, totalPages];
}

/**
 * A list's page controls (in place of Astryx's Pagination, whose "…" can't be used): previous and next, the page
 * numbers, and each "…" a button that opens a box to type any page number into, for long lists.
 */
export function PageControls({page, totalPages, onPageChange, label}: PageControlsProps) {
  const go = (next: number) => next !== page && onPageChange(next);

  return (
    <HStack gap={1} vAlign="center" role="navigation" aria-label={label}>
      <IconButton
        label="Go to previous page"
        icon={<Icon icon={AppIcon.ChevronLeft} />}
        variant="ghost"
        isDisabled={page <= 1}
        onClick={() => go(page - 1)}
      />
      {pageRange(page, totalPages).map((item, i) =>
        item === GAP ? (
          <GoToPage key={`gap-${i}`} totalPages={totalPages} onGo={go} />
        ) : (
          <Button
            key={item}
            label={String(item)}
            aria-label={`Go to page ${item}`}
            aria-current={item === page ? 'page' : undefined}
            variant={item === page ? 'secondary' : 'ghost'}
            onClick={() => go(item)}
          />
        ),
      )}
      <IconButton
        label="Go to next page"
        icon={<Icon icon={AppIcon.ChevronRight} />}
        variant="ghost"
        isDisabled={page >= totalPages}
        onClick={() => go(page + 1)}
      />
    </HStack>
  );
}

/** A "…" in the page numbers, which opens a box to type a page number into. */
function GoToPage({totalPages, onGo}: {totalPages: number; onGo: (page: number) => void}) {
  const [isOpen, setIsOpen] = useState(false);
  const [text, setText] = useState('');
  const [problem, setProblem] = useState<string | null>(null);
  const trigger = useRef<HTMLButtonElement>(null);
  // Set once a page is chosen: the popover hands focus back to "…" as it closes, which would leave it ringed.
  const [hasGone, setHasGone] = useState(false);

  useEffect(() => {
    if (!hasGone || isOpen) return;
    const frame = requestAnimationFrame(() => {
      trigger.current?.blur();
      setHasGone(false);
    });
    return () => cancelAnimationFrame(frame);
  }, [hasGone, isOpen]);

  const open = (next: boolean) => {
    setIsOpen(next);
    setText('');
    setProblem(null);
  };
  const submit = (event?: FormEvent) => {
    event?.preventDefault();
    const page = Number(text.trim());
    if (!Number.isInteger(page) || page < 1 || page > totalPages) {
      setProblem(`Enter a page from 1 to ${totalPages}.`);
      return;
    }
    open(false);
    setHasGone(true);
    onGo(page);
  };

  return (
    <Popover
      label="Go to a page"
      isOpen={isOpen}
      onOpenChange={open}
      placement="above"
      padding={4}
      content={
        <form onSubmit={submit}>
          <VStack gap={3}>
            <TextInput
              label="Page"
              description={`1 to ${totalPages}`}
              value={text}
              onChange={(value) => {
                setText(value);
                setProblem(null);
              }}
              inputMode="numeric"
              autoComplete="off"
              hasAutoFocus
              width={160}
              containerStyle={problem ? undefined : NO_STATUS_LINE}
              status={problem ? {type: 'error', message: problem} : undefined}
            />
            <Button label="Go" variant="primary" type="submit" />
          </VStack>
        </form>
      }>
      <Button ref={trigger} label="…" aria-label="Go to a page" variant="ghost" />
    </Popover>
  );
}
