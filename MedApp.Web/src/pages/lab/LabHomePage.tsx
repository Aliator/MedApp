import {Card} from '@astryxdesign/core/Card';
import {Grid} from '@astryxdesign/core/Grid';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {List, ListItem} from '@astryxdesign/core/List';
import {Section} from '@astryxdesign/core/Section';
import {Text} from '@astryxdesign/core/Text';
import {useQuery, type UseQueryResult} from '@tanstack/react-query';
import type {ReactNode} from 'react';
import {hospitalRequestSearchQuery, type HospitalRequestSummary} from '../../api/hospitals';
import type {PathwaySummary} from '../../api/pathways';
import {pathwaySearchQuery, type PathwaySearch} from '../../api/search';
import {specimenWorklistQuery, type SpecimenSummary, type SpecimenView} from '../../api/specimens';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {PanelHeader} from '../../components/views/PanelCard';
import {formatDate, formatDateTime} from '../../utils/dates';
import {TASK_VIEWS, type TaskView} from '../listSearch';

/** The most each list shows; "See all" opens the rest. */
export const LAB_HOME_ROWS = 5;

/** A team view of pathways (the relay, MC7) for the home: the soonest target dates first. */
export const labPathwaysQuery = (view: TaskView) =>
  pathwaySearchQuery({View: TASK_VIEWS[view], PageSize: LAB_HOME_ROWS, Page: 1, Sort: 'dueDate', Ascending: true} satisfies PathwaySearch);

/** A samples view for the home, in its own order (oldest first; the API's default). */
export const labSamplesQuery = (view: SpecimenView) => specimenWorklistQuery({view, page: 1, pageSize: LAB_HOME_ROWS});

/** What the route's loader preloads: every list on the page. */
export const labHomeQueries = () => [
  hospitalCasesQuery,
  labPathwaysQuery('newForUs'),
  labPathwaysQuery('ourTurn'),
  ...(['Expected', 'InLab', 'Overdue', 'Reported'] as const).map(labSamplesQuery),
];

/** One list on the home: its title, what it holds, its rows, and where all of it is. */
function WorkCard<T>({
  title,
  description,
  empty,
  query,
  href,
  row,
}: {
  title: string;
  description: string;
  empty: string;
  query: UseQueryResult<{items?: T[] | null; totalCount?: number} | undefined>;
  href: string;
  row: (item: T) => {key: string; label: ReactNode; detail: string};
}) {
  const items = query.data?.items ?? [];
  const total = query.data?.totalCount ?? 0;
  return (
    <Card padding={0} width="100%">
      <PanelHeader title={title} summary={query.data ? <Text type="supporting" color="secondary">{`${total}`}</Text> : null} />
      {query.isError ? (
        <Section variant="transparent" padding={6}>
          <AlertMessage message={`${title} could not be loaded. Please try again.`} />
        </Section>
      ) : items.length === 0 ? (
        <Section variant="transparent" padding={6}>
          <Text type="body" color="secondary">
            {query.isPending ? 'Loading...' : `${empty}. ${description}`}
          </Text>
        </Section>
      ) : (
        <List edge="bleed" hasDividers aria-label={title}>
          {items.map((item) => {
            const r = row(item);
            return <ListItem key={r.key} label={r.label} description={r.detail} />;
          })}
          <ListItem
            href={href}
            startContent={<Icon icon={AppIcon.ChevronRight} color="secondary" />}
            label={<Text type="body">{total > items.length ? `See all ${total}` : 'Open the list'}</Text>}
          />
        </List>
      )}
    </Card>
  );
}

/** The hospital's newest requests (MC18), each with its lab case. */
const hospitalCasesQuery = hospitalRequestSearchQuery({page: 1, pageSize: 5});

const hospitalRow = (r: HospitalRequestSummary) => ({
  key: r.id ?? '',
  label: <Link href={`/hospital-requests/${r.id}`}>{r.labCaseNumber}</Link>,
  detail: [r.hospitalName, r.patientName, r.reportNeededBy ? `Needed by ${formatDate(r.reportNeededBy)}` : null].filter(Boolean).join(' · '),
});

const pathwayRow = (p: PathwaySummary) => ({
  key: p.id ?? '',
  label: <Link href={`/pathways/${p.id}`}>{p.title}</Link>,
  detail: [p.patientName, p.currentStageName, p.dueDate ? `Target ${formatDate(p.dueDate)}` : null].filter(Boolean).join(' · '),
});

const sampleRow = (at: (s: SpecimenSummary) => string | null | undefined, words: string) => (s: SpecimenSummary) => ({
  key: s.id ?? '',
  label: <Link href={`/specimens/${s.id}`}>{s.number}</Link>,
  detail: [s.specimenTypeName, s.patientName, at(s) ? `${words} ${formatDateTime(at(s) ?? '')}` : null].filter(Boolean).join(' · '),
});

/**
 * The lab team's home (`/lab`; MC17, built with MC13 in the demo first): what's waiting for the lab, each list's
 * first few and its count, linking to the full list. **New for the lab** (clinic pathways handed to the lab, not
 * picked up) and **Our turn** (lab stages it's working), from the relay's team views (My Worklist's); **To receive**
 * (samples sent, not received), **In progress** (received, not reported), **Overdue** (past the type's expected days)
 * and **Back to the clinic** (reported in the last 7 days), from the samples worklist; **From the hospital** (MC18), the newest hospital requests. Lists order by target dates
 * and times only (decision 191). Every patient shown is recorded as listed, by the endpoints. To report (a case
 * whose steps are done) joins with lab cases (MC15), hospitals' cases with MC18.
 */
export function LabHomePage() {
  const isNarrow = useIsNarrow();
  const newForUs = useQuery(labPathwaysQuery('newForUs'));
  const ourTurn = useQuery(labPathwaysQuery('ourTurn'));
  const toReceive = useQuery(labSamplesQuery('Expected'));
  const inProgress = useQuery(labSamplesQuery('InLab'));
  const overdue = useQuery(labSamplesQuery('Overdue'));
  const backToClinic = useQuery(labSamplesQuery('Reported'));
  const fromHospital = useQuery(hospitalCasesQuery);

  return (
    <Page title="Lab" subtitle="What's waiting for the lab, soonest first">
      <Grid columns={isNarrow ? 1 : 2} gap={6}>
        <WorkCard
          title="New for the lab"
          description="Clinic pathways handed to the lab, not yet picked up."
          empty="Nothing waiting to be picked up"
          query={newForUs}
          href="/tasks?view=newForUs"
          row={pathwayRow}
        />
        <WorkCard
          title="Our turn"
          description="Pathways at a stage the lab works."
          empty="No pathway is with the lab"
          query={ourTurn}
          href="/tasks?view=ourTurn"
          row={pathwayRow}
        />
        <WorkCard
          title="To receive"
          description="Samples sent to the lab, not yet received."
          empty="Nothing on its way"
          query={toReceive}
          href="/lab/samples?view=Expected"
          row={sampleRow((s) => s.sentAtUtc, 'Sent')}
        />
        <WorkCard
          title="In progress"
          description="Received, not yet reported."
          empty="Nothing in the lab"
          query={inProgress}
          href="/lab/samples?view=InLab"
          row={sampleRow((s) => s.receivedAtUtc, 'Received')}
        />
        <WorkCard
          title="Overdue"
          description="Not reported by the type's expected days."
          empty="Nothing overdue"
          query={overdue}
          href="/lab/samples?view=Overdue"
          row={sampleRow((s) => s.expectedByUtc, 'Expected by')}
        />
        <WorkCard
          title="Back to the clinic"
          description="Reported in the last 7 days."
          empty="Nothing reported in the last 7 days"
          query={backToClinic}
          href="/lab/samples?view=Reported"
          row={sampleRow((s) => s.reportedAtUtc, 'Reported')}
        />
        <WorkCard
          title="From the hospital"
          description="Hospital requests and their lab cases, newest first."
          empty="No hospital requests"
          query={fromHospital}
          href="/hospital-requests"
          row={hospitalRow}
        />
      </Grid>
    </Page>
  );
}
