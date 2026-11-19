import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery, type QueryClient} from '@tanstack/react-query';
import {problemMessage} from '../../api/errors';
import {mdtAwaitingMeetingQuery, mdtMeetingListQuery, mdtMeetingsQuery, upcomingMeetings, type MdtMeeting} from '../../api/mdt';
import {preload} from '../../api/preload';
import {teamsQuery} from '../../api/teams';
import {AnchoredSelector} from '../../components/forms/AnchoredSelector';
import {meetingLabel} from '../../components/mdt/MdtDialogs';
import {MdtItemList} from '../../components/mdt/MdtItemList';
import {Page} from '../../components/pages/Page';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {SummaryAlert} from '../../components/shared/SummaryAlert';
import {AppIcon} from '../../components/shared/icons';
import {PanelCard} from '../../components/views/PanelCard';
import type {MdtSearch} from './mdtSearch';

const ALL = 'all';

/** What the MDT page needs first, for its route's loader: the teams and the MDTs (neither names a patient). */
export const preloadMdtPage = (queryClient: QueryClient, search: MdtSearch) =>
  preload(queryClient, teamsQuery(true), mdtMeetingsQuery(search.team));

/** The meeting a search shows: the one chosen, or else the next MDT not yet ended (the soonest), or none. */
export function shownMeeting(meetings: readonly MdtMeeting[], search: MdtSearch, now = new Date()): MdtMeeting | undefined {
  if (search.meeting) return meetings.find((m) => m.id === search.meeting);
  return upcomingMeetings(meetings, now)[0];
}

export interface MdtPageProps {
  search: MdtSearch;
  onSearchChange: (search: MdtSearch) => void;
}

/**
 * The MDT page (`/mdt`, decision 143): choose a team and one of its MDTs (both in the address); the meeting's
 * list in the order items were listed, each with the patient banner (hazard H-58), the question, a link to the
 * pathway (with its files), and once discussed the outcome as written and who was present; and below, items whose
 * meeting was cancelled, to list again. Another team's MDT shows only the cases its attendees may read, and why
 * (MC26). Print prints the list. Opening the list records that each patient on it was
 * listed (the API does). A meeting's title and notes never name patients: the list is separate.
 */
export function MdtPage({search, onSearchChange}: MdtPageProps) {
  const teams = useQuery(teamsQuery(true));
  const meetings = useQuery(mdtMeetingsQuery(search.team));
  const meeting = shownMeeting(meetings.data ?? [], search);
  const list = useQuery({...mdtMeetingListQuery(meeting?.id ?? ''), enabled: Boolean(meeting?.id)});
  const awaiting = useQuery(mdtAwaitingMeetingQuery(search.team));
  const go = (next: Partial<MdtSearch>) => onSearchChange({...search, ...next});

  return (
    <Page
      title="MDT"
      subtitle="Multidisciplinary team meetings: the pathways listed, and the team's outcomes as recorded."
      fill
      message={
        meetings.isError ? (
          <AlertMessage label="MDTs not loaded" message={problemMessage(meetings.error, 'The MDTs could not be loaded.')} />
        ) : (
          list.data?.readOnlyReason && <SummaryAlert status="info" label="Another team’s MDT" message={list.data.readOnlyReason} />
        )
      }
      actions={<Button label="Print" variant="secondary" icon={<Icon icon={AppIcon.Print} />} onClick={() => window.print()} isDisabled={!list.data} />}>
      <VStack gap={4} height="100%">
        <HStack gap={2} vAlign="end" wrap="wrap">
          <AnchoredSelector
            label="Team"
            value={search.team ?? ALL}
            onChange={(value) => go({team: value === ALL ? undefined : value, meeting: undefined})}
            isLoading={teams.isLoading}
            options={[{value: ALL, label: 'Every team'}, ...(teams.data ?? []).map((s) => ({value: s.id ?? '', label: s.name ?? ''}))]}
          />
          <AnchoredSelector
            label="Meeting"
            value={meeting?.id ?? ''}
            onChange={(value) => go({meeting: value || undefined})}
            isLoading={meetings.isLoading}
            options={(meetings.data ?? []).map((m) => ({value: m.id ?? '', label: meetingLabel(m)}))}
          />
        </HStack>
        <StackItem size="fill">
          <PanelCard title={meeting ? meetingLabel(meeting) : 'No MDT chosen'}>
            {!meeting ? (
              <Text type="body" color="secondary">
                {meetings.isLoading ? 'Loading...' : 'No MDTs in the last 60 days or the next 180. Whoever arranges meetings marks one as a team’s MDT.'}
              </Text>
            ) : list.isError ? (
              <AlertMessage label="List not loaded" message={problemMessage(list.error, 'The list could not be loaded.')} />
            ) : !list.data ? (
              <Text type="body" color="secondary">Loading...</Text>
            ) : (
              <MdtItemList
                items={list.data.items ?? []}
                meetings={meetings.data ?? []}
                isLoadingMeetings={meetings.isLoading}
                showsPatient
                linksPathway
                emptyText={
                  list.data.readOnlyReason ? 'No cases on this MDT for you to see.' : 'Nobody is listed for this MDT yet. List a pathway from its page.'
                }
              />
            )}
          </PanelCard>
        </StackItem>
        {(awaiting.data ?? []).length > 0 && (
          <PanelCard title="Meeting cancelled: list again">
            <MdtItemList
              items={awaiting.data ?? []}
              meetings={meetings.data ?? []}
              isLoadingMeetings={meetings.isLoading}
              showsPatient
              linksPathway
              emptyText=""
            />
          </PanelCard>
        )}
      </VStack>
    </Page>
  );
}
