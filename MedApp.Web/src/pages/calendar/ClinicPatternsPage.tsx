import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {pixel, proportional} from '@astryxdesign/core/Table';
import {Text} from '@astryxdesign/core/Text';
import {Token} from '@astryxdesign/core/Token';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {MAX_WEEKS_AHEAD, patternsQuery, useCreateSessions, type ClinicPattern, type CreatedSessions} from '../../api/clinics';
import {problemMessage} from '../../api/errors';
import {useAnnounce} from '../../app/announcements';
import {PatternFormDialog} from '../../components/calendar/PatternFormDialog';
import {AnchoredSelector} from '../../components/forms/AnchoredSelector';
import {DialogFrame} from '../../components/modals/DialogFrame';
import {Page} from '../../components/pages/Page';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {DataTable, type DataTableColumn} from '../../components/tables/DataTable';
import {formatShortDayDate, formatTimeRange} from '../../utils/calendarSlots';
import {formatShortDate} from '../../utils/dates';

type PatternRow = ClinicPattern & Record<string, unknown>;

/** A pattern's day and times in words: "Mondays, 09:00 to 12:00". */
export const patternWhen = (p: Pick<ClinicPattern, 'dayOfWeek' | 'starts' | 'ends'>) =>
  `${p.dayOfWeek}s, ${(p.starts ?? '').slice(0, 5)} to ${(p.ends ?? '').slice(0, 5)}`;

/** How often and for how long: "Every week from 16 Nov 2026", "Every 2 weeks from 16 Nov 2026 until 1 Mar 2027". */
export const patternRepeat = (p: Pick<ClinicPattern, 'everyWeeks' | 'fromDate' | 'untilDate'>) =>
  `${p.everyWeeks === 1 ? 'Every week' : `Every ${p.everyWeeks} weeks`} from ${formatShortDate(p.fromDate ?? '')}${p.untilDate ? ` until ${formatShortDate(p.untilDate)}` : ''}`;

/**
 * Clinic Patterns (`/calendar/clinics/patterns`, ManageClinics; centre plan decision 125): clinics that repeat, each
 * with its clinician, room, team, day and times, slot length and how often. **Create sessions** makes a pattern's
 * sessions for the next 1 to 13 weeks, when asked (nothing makes them in the background), and lists what it made and the
 * dates it skipped, with why (already made, a closed day, a clash).
 *
 * **Deprecated** (the owner, 18 November 2026): no new work, kept building and working by address, with its tests;
 * nothing links to it from the Calendar page any more (docs/agents/calendar.md).
 */
export function ClinicPatternsPage() {
  const patterns = useQuery(patternsQuery);
  const [editing, setEditing] = useState<ClinicPattern | null>(null);
  const [isAdding, setIsAdding] = useState(false);
  const [creating, setCreating] = useState<ClinicPattern | null>(null);

  const columns: DataTableColumn<PatternRow>[] = [
    {
      key: 'when',
      header: 'When',
      width: proportional(1, {minWidth: 200}),
      renderCell: (p) => (
        <VStack gap={0}>
          <Text type="body" weight="semibold">
            {patternWhen(p)}
          </Text>
          <Text type="supporting" color="secondary">
            {`${patternRepeat(p)} · ${p.slotMinutes}-minute slots`}
          </Text>
          {p.hasEnded && <Token size="sm" label="Ended" color="gray" />}
        </VStack>
      ),
    },
    {key: 'clinician', header: 'Clinician', width: proportional(1, {minWidth: 130}), renderCell: (p) => <Text type="body">{p.staffUsername}</Text>},
    {
      key: 'room',
      header: 'Room',
      width: proportional(1, {minWidth: 150}),
      renderCell: (p) => (
        <VStack gap={0}>
          <Text type="body">{p.roomName}</Text>
          <Text type="supporting" color="secondary">
            {p.siteName}
          </Text>
        </VStack>
      ),
    },
    {key: 'team', header: 'Team', width: proportional(1, {minWidth: 140}), renderCell: (p) => <Text type="body">{p.teamName}</Text>},
    {
      key: 'actions',
      header: 'Actions',
      align: 'end',
      width: pixel(260),
      renderCell: (p) => (
        <StackItem>
          <HStack gap={2}>
            <Button label={`Edit: ${patternWhen(p)}, ${p.staffUsername}`} variant="secondary" size="sm" onClick={() => setEditing(p)}>
              Edit
            </Button>
            <Button
              label={`Create sessions: ${patternWhen(p)}, ${p.staffUsername}`}
              variant="secondary"
              size="sm"
              icon={<Icon icon={AppIcon.Add} />}
              isDisabled={p.hasEnded === true}
              onClick={() => setCreating(p)}>
              Create Sessions
            </Button>
          </HStack>
        </StackItem>
      ),
    },
  ];

  const count = patterns.data?.length ?? 0;
  return (
    <Page
      title="Clinic Patterns"
      subtitle={patterns.isPending ? 'Loading...' : count === 1 ? '1 pattern' : `${count} patterns`}
      fill
      message={patterns.isError ? <AlertMessage label="Clinic patterns not loaded" message="The clinic patterns could not be loaded. Please try again." /> : undefined}
      actions={
        <>
          <Button label="Clinics" variant="secondary" href="/calendar/clinics" icon={<Icon icon={AppIcon.Clinics} />} />
          <Button label="New Pattern" variant="primary" icon={<Icon icon={AppIcon.Add} />} onClick={() => setIsAdding(true)} />
        </>
      }
      dialogs={
        <>
          <PatternFormDialog isOpen={isAdding} onClose={() => setIsAdding(false)} />
          <PatternFormDialog isOpen={editing !== null} pattern={editing ?? undefined} onClose={() => setEditing(null)} />
          <CreateSessionsDialog pattern={creating} onClose={() => setCreating(null)} />
        </>
      }>
      <DataTable
        data={(patterns.data ?? []) as PatternRow[]}
        columns={columns}
        idKey="id"
        isLoading={patterns.isPending}
        skeletonRows={2}
        search={
          <Text type="body" color="secondary">
            A pattern makes no sessions by itself. Create Sessions makes them for the weeks ahead, skipping closed days, days
            the clinician doesn’t work, and anything already on their calendar or in the room.
          </Text>
        }
        emptyState={
          <EmptyState
            title="No clinic patterns"
            description="Add one with New Pattern for a clinic that repeats every week or few weeks."
            icon={<Icon icon={AppIcon.ClinicPatterns} size="lg" />}
            isCompact
          />
        }
      />
    </Page>
  );
}

/** Create sessions for a pattern: how many weeks ahead, then what it made and skipped. */
export function CreateSessionsDialog({pattern, onClose}: {pattern: ClinicPattern | null; onClose: () => void}) {
  const create = useCreateSessions();
  const announce = useAnnounce();
  const [weeks, setWeeks] = useState(6);
  const [result, setResult] = useState<CreatedSessions | null>(null);
  const [problem, setProblem] = useState<string | null>(null);

  const close = () => {
    setResult(null);
    setProblem(null);
    onClose();
  };

  const run = () => {
    if (!pattern) return;
    setProblem(null);
    create.mutate(
      {id: pattern.id ?? '', weeks},
      {
        onSuccess: (made) => {
          setResult(made);
          announce(madeWords(made));
        },
        onError: (error) => setProblem(problemMessage(error, 'The sessions could not be made. Please try again.')),
      },
    );
  };

  return (
    <DialogFrame
      isOpen={pattern !== null}
      title="Create Sessions"
      icon={<Icon icon={AppIcon.ClinicPatterns} size="lg" color="accent" />}
      onClose={close}
      width={520}
      purpose={create.isPending ? 'required' : 'form'}
      footer={
        result ? (
          <Button label="Close" variant="primary" onClick={close} />
        ) : (
          <>
            <Button label="Cancel" variant="secondary" isDisabled={create.isPending} onClick={close} />
            <Button label="Create Sessions" variant="primary" isLoading={create.isPending} onClick={run} />
          </>
        )
      }>
      {pattern && (
        <VStack gap={4} hAlign="stretch">
          <Text type="body" weight="semibold">{`${patternWhen(pattern)}, ${pattern.staffUsername}, ${pattern.roomName}`}</Text>
          {problem && <AlertMessage label="Sessions not created" message={problem} />}
          {result ? (
            <>
              <AlertMessage status="success" label={madeWords(result).replace(/\.$/, '')} />
              {(result.created ?? []).length > 0 && (
                <VStack gap={1} role="list" aria-label="Sessions made">
                  {(result.created ?? []).map((s) => (
                    <Text key={s.id} type="body" role="listitem">
                      {`${formatShortDayDate(s.date ?? '')}, ${formatTimeRange(s.startUtc ?? '', s.endUtc ?? '')}`}
                    </Text>
                  ))}
                </VStack>
              )}
              {(result.skipped ?? []).length > 0 && (
                <VStack gap={1} role="list" aria-label="Dates skipped">
                  {(result.skipped ?? []).map((s) => (
                    <Text key={s.date} type="body" role="listitem">
                      {`${formatShortDayDate(s.date ?? '')}: ${s.reason}`}
                    </Text>
                  ))}
                </VStack>
              )}
            </>
          ) : (
            <AnchoredSelector
              label="For the next"
              value={String(weeks)}
              onChange={(value) => setWeeks(Number(value))}
              options={Array.from({length: MAX_WEEKS_AHEAD}, (_, i) => i + 1).map((n) => ({value: String(n), label: n === 1 ? '1 week' : `${n} weeks`}))}
            />
          )}
        </VStack>
      )}
    </DialogFrame>
  );
}

/** What Create Sessions did, in words: "Created 4 sessions; skipped 2 dates." */
export function madeWords(made: CreatedSessions): string {
  const created = made.created?.length ?? 0;
  const skipped = made.skipped?.length ?? 0;
  const madePart = created === 0 ? 'Created no sessions' : created === 1 ? 'Created 1 session' : `Created ${created} sessions`;
  return skipped === 0 ? `${madePart}.` : `${madePart}; skipped ${skipped === 1 ? '1 date' : `${skipped} dates`}.`;
}
