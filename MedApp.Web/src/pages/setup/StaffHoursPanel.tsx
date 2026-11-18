import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {VStack} from '@astryxdesign/core/Stack';
import {Switch} from '@astryxdesign/core/Switch';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {staffHoursQuery, useCalendarActions, type StaffHours} from '../../api/calendar';
import {sitesQuery} from '../../api/sites';
import {HoursEditor} from '../../components/calendar/HoursEditor';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {PanelCard} from '../../components/views/PanelCard';
import {formatDate} from '../../utils/dates';
import {rowProblem, staffInput, staffRows, WEEK_DAYS, type HoursRow} from '../../utils/workingHours';
import {saveProblem, type SaveProblem} from '../saveProblem';
import type {TemplatesOwner} from './calendarTemplatesOwner';

/** A person's hours as days with times, for `hoursSummary`. */
export const hoursDays = (hours: StaffHours) => (hours.days ?? []).map((d) => ({dayOfWeek: d.dayOfWeek, from: d.starts, to: d.ends}));

const clock = (time: string | null | undefined) => (time ?? '').slice(0, 5);

/** What went wrong saving hours, in words: the API's message, or its field errors together. */
export const problemText = (problem: SaveProblem) => problem.message ?? Object.values(problem.validation ?? {}).flat().join(' ');

/**
 * A person's working hours (centre plan decision 119), the templates page's last tab: the days they work, the times and
 * where, or the default (Monday to Friday, 08:00 to 18:00, at no particular site). Their calendar follows them: the
 * days they work are the ones anything can be booked, blocked or arranged on, inside those times, and a site's closed
 * day closes theirs. Holders of ManageClinics or ManageAnyCalendar change them (the API's `canEdit`), from today; past
 * days keep what applied then, and what's already on the calendar stays where it is.
 */
export function StaffHoursPanel({owner, onError}: {owner: TemplatesOwner; onError: (message: string | null) => void}) {
  const hours = useQuery({...staffHoursQuery(owner.ownerId ?? ''), enabled: owner.ownerId !== undefined});
  const sites = useQuery(sitesQuery());
  const {setHours} = useCalendarActions();
  const announce = useAnnounce();
  const [rows, setRows] = useState<HoursRow[] | null>(null);
  const [isDefault, setIsDefault] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);

  if (hours.isError) return <AlertMessage message="The working hours could not be loaded. Please try again." />;

  const data = hours.data;
  const whose = owner.isOwn ? 'You work' : `${owner.ownerName || 'They'} works`;
  const openSites = (sites.data ?? []).filter((s) => !s.isArchived);
  // A site they already work at stays choosable, archived or not.
  const siteOptions = [
    ...openSites,
    ...(sites.data ?? []).filter((s) => s.isArchived && (data?.days ?? []).some((d) => d.siteId === s.id)),
  ].map((s) => ({value: s.id ?? '', label: s.isArchived ? `${s.name} (archived)` : (s.name ?? '')}));

  const open = () => {
    if (!data) return;
    onError(null);
    setProblem(null);
    setIsDefault(!data.hasOwnHours);
    setRows(staffRows(data, openSites[0]?.id ?? ''));
  };
  const close = () => setRows(null);
  const invalid = !isDefault && (rows ?? []).some((r) => rowProblem(r, true) !== null);

  const save = () => {
    if (!rows) return;
    setProblem(null);
    setHours.mutate(
      {userId: owner.ownerId ?? '', days: isDefault ? [] : staffInput(rows)},
      {
        onSuccess: () => {
          announce('Working hours saved');
          close();
        },
        onError: (e) => setProblem(problemText(saveProblem(e, 'save', 'working hours'))),
      },
    );
  };

  return (
    <PanelCard
      title="Working Hours"
      footerActions={
        data?.canEdit && <Button label="Edit Working Hours" variant="secondary" icon={<Icon icon={AppIcon.Edit} />} onClick={open} />
      }>
      <VStack gap={3}>
        {!data ? (
          <Text type="body" color="secondary">
            Loading the working hours...
          </Text>
        ) : (
          <>
            <Text type="body">
              {data.hasOwnHours
                ? `${whose} these days, from ${formatDate(data.since ?? '')}. Anything on the calendar sits inside them.`
                : `${whose} the default hours: Monday to Friday, 08:00 to 18:00, at no particular site.`}
            </Text>
            <MetadataList>
              {WEEK_DAYS.map(({name}) => {
                const day = data.days?.find((d) => d.dayOfWeek === name);
                return (
                  <MetadataListItem key={name} label={name}>
                    {day ? `${clock(day.starts)} to ${clock(day.ends)}${day.siteName ? `, ${day.siteName}` : ''}` : 'Not working'}
                  </MetadataListItem>
                );
              })}
            </MetadataList>
            {!data.canEdit && (
              <Text type="supporting" color="secondary">
                Only staff who manage clinics or anyone&apos;s calendar can change working hours.
              </Text>
            )}
          </>
        )}
      </VStack>
      <ConfirmDialog
        isOpen={rows !== null}
        title="Edit Working Hours"
        message="From today: past days keep what they had, and what's already on the calendar stays where it is. Each day's hours must be inside its site's opening hours."
        icon={AppIcon.Edit}
        confirmText="Save"
        isProcessing={setHours.isPending}
        confirmDisabled={invalid}
        hasSaveShortcut
        onConfirm={save}
        onCancel={close}>
        <VStack gap={3}>
          {problem && <AlertMessage message={problem} />}
          <Switch
            label="Use the default hours: Monday to Friday, 08:00 to 18:00"
            value={isDefault}
            onChange={setIsDefault}
            isDisabled={setHours.isPending}
          />
          {!isDefault && rows && (
            <HoursEditor rows={rows} onChange={setRows} onLabel="Works" sites={siteOptions} isDisabled={setHours.isPending} />
          )}
        </VStack>
      </ConfirmDialog>
    </PanelCard>
  );
}
