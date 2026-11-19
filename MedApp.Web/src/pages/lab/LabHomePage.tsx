import {Card} from '@astryxdesign/core/Card';
import {Grid} from '@astryxdesign/core/Grid';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {List, ListItem} from '@astryxdesign/core/List';
import {Section} from '@astryxdesign/core/Section';
import {Text} from '@astryxdesign/core/Text';
import {useQuery, type UseQueryResult} from '@tanstack/react-query';
import type {ReactNode} from 'react';
import {incomingConsultsQuery} from '../../api/consults';
import {hospitalRequestSearchQuery, type HospitalRequestSummary} from '../../api/hospitals';
import {labCaseSearchQuery, type LabCaseSummary, type LabCaseView} from '../../api/labCases';
import type {PathwaySummary} from '../../api/pathways';
import {pathwaySearchQuery, type PathwaySearch} from '../../api/search';
import {specimenWorklistQuery, type SpecimenSummary, type SpecimenView} from '../../api/specimens';
import {IncomingConsults} from '../../components/consults/IncomingConsults';
import {labClockLine} from '../../components/labCases/LabClock';
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

/** A lab cases view for the home, soonest due first (the API's order for these views). */
export const labCasesQuery = (view: LabCaseView) => labCaseSearchQuery({view, page: 1, pageSize: LAB_HOME_ROWS});

/** Lab cases with every step done and "No more work" recorded (MC15). */
export const toReportQuery = labCasesQuery('ToReport');

/** What the route's loader preloads: every list on the page. */
export const labHomeQueries = () => [
  hospitalCasesQuery,
  toReportQuery,
  labCasesQuery('AtRisk'),
  labCasesQuery('Overdue'),
  labCasesQuery('AwaitingSurgeryDetails'),
  labPathwaysQuery('newForUs'),
  labPathwaysQuery('ourTurn'),
  ...(['Expected', 'InLab', 'Reported'] as const).map(labSamplesQuery),
  incomingConsultsQuery(),
];

/** One list on a home (the lab's, a clinic team's): its title, what it holds, its rows, and where all of it is. */
export function WorkCard<T>({
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
          <AlertMessage label={`${title} not loaded`} />
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

const caseRow = (c: LabCaseSummary) => ({
  key: c.id ?? '',
  label: <Link href={`/lab/cases/${c.id}`}>{c.number}</Link>,
  detail: [c.labTestName, c.patientName, c.sourceName].filter(Boolean).join(' · '),
});

/** A case with its clock (MC19): "Due tomorrow (at risk), 21 Nov 2026", "2 days past due, due 17 Nov 2026". */
const clockRow = (c: LabCaseSummary) => {
  const row = caseRow(c);
  return {...row, detail: [row.detail, labClockLine(c.clock)].filter(Boolean).join(' · ')};
};

/** A hospital case whose report went back (MC20): when it was sent. */
const sentRow = (c: LabCaseSummary) => {
  const row = caseRow(c);
  return {...row, detail: [row.detail, c.reportSentAtUtc ? `Sent ${formatDateTime(c.reportSentAtUtc)}` : null].filter(Boolean).join(' · ')};
};

/** A pathway's row on a home: its title, opening it, then the patient, its stage and its target date. */
export const pathwayRow = (p: PathwaySummary) => ({
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
 * picked up) and **With my team** (lab stages it's working), from the relay's team views (My Worklist's); **To receive**
 * (samples sent, not received), **In progress** (received, not reported) and **Back to the clinic** (reported in the
 * last 7 days), from the samples worklist; **From the hospital** (MC18), the newest hospital requests; **Awaiting
 * surgery details** (MC20), hospital cases whose report has gone back. **To report**
 * (MC15): lab cases with every step done and "No more work" recorded. **At risk** and **Overdue** (MC19): lab cases by
 * their strict clock (the sooner of the lab's target and the hospital's date), soonest due first, each with its
 * countdown. **Consults for the lab** (MC25): other teams' open questions to the lab, oldest asked first, with Reply.
 * Lists order by target dates and times only (decision 191). Every patient shown is recorded as listed, by
 * the endpoints.
 */
export function LabHomePage() {
  const isNarrow = useIsNarrow();
  const newForUs = useQuery(labPathwaysQuery('newForUs'));
  const ourTurn = useQuery(labPathwaysQuery('ourTurn'));
  const toReceive = useQuery(labSamplesQuery('Expected'));
  const inProgress = useQuery(labSamplesQuery('InLab'));
  const toReport = useQuery(toReportQuery);
  const atRisk = useQuery(labCasesQuery('AtRisk'));
  const overdue = useQuery(labCasesQuery('Overdue'));
  const backToClinic = useQuery(labSamplesQuery('Reported'));
  const fromHospital = useQuery(hospitalCasesQuery);
  const awaitingSurgery = useQuery(labCasesQuery('AwaitingSurgeryDetails'));

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
          title="With my team"
          description="Pathways at a stage the lab works."
          empty="No pathway is with the lab"
          query={ourTurn}
          href="/tasks?view=ourTurn"
          row={pathwayRow}
        />
        <WorkCard
          title="On its way"
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
          title="To report"
          description="Lab cases with every step done: their results to enter and authorise."
          empty="Nothing to report"
          query={toReport}
          href="/lab/cases?view=ToReport"
          row={clockRow}
        />
        <WorkCard
          title="At risk"
          description="Lab cases near their due date: the sooner of the lab's target and the hospital's date."
          empty="Nothing at risk"
          query={atRisk}
          href="/lab/cases?view=AtRisk"
          row={clockRow}
        />
        <WorkCard
          title="Breached"
          description="Lab cases past their due date, not yet reported."
          empty="Nothing breached"
          query={overdue}
          href="/lab/cases?view=Overdue"
          row={clockRow}
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
        <WorkCard
          title="Awaiting surgery details"
          description="Hospital cases whose report has gone back, open until the hospital confirms what surgery was done."
          empty="Nothing awaiting surgery details"
          query={awaitingSurgery}
          href="/lab/cases?view=AwaitingSurgeryDetails"
          row={sentRow}
        />
        <IncomingConsults title="Consults for the lab" />
      </Grid>
    </Page>
  );
}
