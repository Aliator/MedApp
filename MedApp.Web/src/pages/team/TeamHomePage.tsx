import {Grid} from '@astryxdesign/core/Grid';
import {Link} from '@astryxdesign/core/Link';
import {useQuery} from '@tanstack/react-query';
import {appointmentsQuery, type Appointment} from '../../api/appointments';
import {incomingConsultsQuery} from '../../api/consults';
import type {ReferralSummary} from '../../api/referrals';
import type {ResultSummary} from '../../api/results';
import {pathwaySearchQuery} from '../../api/search';
import {can, useCurrentUser, type CurrentUser} from '../../auth/auth';
import {IncomingConsults} from '../../components/consults/IncomingConsults';
import {Page, useIsNarrow} from '../../components/pages/Page';
import {formatDate, formatDateTime} from '../../utils/dates';
import {requiresAppointmentQuery} from '../calendar/RequiresAppointmentPage';
import {labPathwaysQuery, pathwayRow, WorkCard} from '../lab/LabHomePage';
import {referralsListQuery} from '../referrals/ReferralsListPage';
import {resultsListQuery} from '../results/ResultsPage';
import {taskQuery} from '../tasks/MyTasksPage';

/** The most each list shows; "See all" opens the rest. */
export const TEAM_HOME_ROWS = 5;

/** Referrals awaiting triage in my teams, longest wait first (the Referrals inbox's own order). */
export const triageQuery = referralsListQuery({}, TEAM_HOME_ROWS);

/** Unfinished pathways assigned to me, started or not, soonest target date first (My Worklist's order). */
export const myPathwaysQuery = pathwaySearchQuery(taskQuery({progress: 'any'}, TEAM_HOME_ROWS));

/** My team's pathways handed back by the lab, not yet picked up (the relay's Back from the lab, MC7). */
export const backFromLabQuery = labPathwaysQuery('backFromLab');

/** Results awaiting my sign-off, oldest report first (the Results list's own order). */
export const toSignOffQuery = resultsListQuery('AwaitingMine', {}, TEAM_HOME_ROWS);

/** Appointments asked for but not booked, soonest pathway target date first (Requires Appointment's order). */
export const toBookQuery = appointmentsQuery(requiresAppointmentQuery({}, TEAM_HOME_ROWS));

/** What the route's loader preloads: the lists the person is shown. */
export const teamHomeQueries = (user: Pick<CurrentUser, 'permissions'>) => [
  ...(can(user, 'TriageReferrals') ? [triageQuery] : []),
  myPathwaysQuery,
  backFromLabQuery,
  ...(can(user, 'SignOffResults') ? [toSignOffQuery] : []),
  ...(can(user, 'BookAppointments') ? [toBookQuery] : []),
  incomingConsultsQuery(),
];

const referralRow = (r: ReferralSummary) => ({
  key: r.id ?? '',
  label: <Link href={`/referrals/${r.id}`}>{r.patientName}</Link>,
  detail: [r.teamName, r.receivedOn ? `Received ${formatDate(r.receivedOn)}` : null].filter(Boolean).join(' · '),
});

const resultRow = (r: ResultSummary) => ({
  key: r.id ?? '',
  label: <Link href={`/results/${r.id}`}>{r.title}</Link>,
  detail: [r.patientName, r.reportedAtUtc ? `Reported ${formatDateTime(r.reportedAtUtc)}` : null].filter(Boolean).join(' · '),
});

const appointmentRow = (a: Appointment) => ({
  key: a.id ?? '',
  label: a.pathwayId ? <Link href={`/pathways/${a.pathwayId}`}>{a.patientName}</Link> : <Link href={`/patients/${a.patientId}`}>{a.patientName}</Link>,
  detail: [a.stageName, a.pathwayTitle, a.pathwayDueDate ? `Target ${formatDate(a.pathwayDueDate)}` : null].filter(Boolean).join(' · '),
});

/**
 * A clinic team's home (`/team`; MC24, demo first): the team's and my work, each list's first few and its count,
 * linking to the full list, as the lab's home does. **Triage** (referrals awaiting triage, for those who triage),
 * **My pathways** (assigned to me), **Back from the lab** (the relay's team view), **Results to sign off** (awaiting
 * mine, for those who sign off), **Appointments to book** (Requested, for those who book) and **Consults for your
 * team** (other teams' open questions to it, MC25). Built for any clinic
 * team, from the existing searches (no new endpoint); lists order by dates only, as their full lists do, and every
 * patient shown is recorded as listed by the endpoints.
 */
export function TeamHomePage() {
  const user = useCurrentUser();
  const isNarrow = useIsNarrow();
  const canTriage = can(user, 'TriageReferrals');
  const canSignOff = can(user, 'SignOffResults');
  const canBook = can(user, 'BookAppointments');
  const triage = useQuery({...triageQuery, enabled: canTriage});
  const myPathways = useQuery(myPathwaysQuery);
  const backFromLab = useQuery(backFromLabQuery);
  const toSignOff = useQuery({...toSignOffQuery, enabled: canSignOff});
  const toBook = useQuery({...toBookQuery, enabled: canBook});
  // The team's name when the person is in one clinic; "Team" for someone in several.
  const clinics = (user.teams ?? []).filter((t) => t.kind === 'Clinic');
  const title = clinics.length === 1 ? clinics[0].name : 'Team';

  return (
    <Page title={title} subtitle="Your team's work and yours, soonest first">
      <Grid columns={isNarrow ? 1 : 2} gap={6}>
        {canTriage && (
          <WorkCard
            title="Triage"
            description="Referrals to your team, waiting to be accepted, rejected or redirected."
            empty="No referrals are awaiting triage"
            query={triage}
            href="/referrals"
            row={referralRow}
          />
        )}
        <WorkCard
          title="My pathways"
          description="Unfinished pathways assigned to you."
          empty="Nothing assigned to you"
          query={myPathways}
          href="/tasks?progress=any"
          row={pathwayRow}
        />
        <WorkCard
          title="Back from the lab"
          description="Your team's pathways handed back by the lab, not yet picked up."
          empty="Nothing back from the lab"
          query={backFromLab}
          href="/tasks?view=backFromLab"
          row={pathwayRow}
        />
        {canSignOff && (
          <WorkCard
            title="Results to sign off"
            description="Results awaiting your sign-off, oldest report first."
            empty="No results await your sign-off"
            query={toSignOff}
            href="/results"
            row={resultRow}
          />
        )}
        {canBook && (
          <WorkCard
            title="Appointments to book"
            description="Appointments asked for on a pathway, not yet booked."
            empty="Nothing to book"
            query={toBook}
            href="/calendar/requires-appointment"
            row={appointmentRow}
          />
        )}
        <IncomingConsults title="Consults for your team" />
      </Grid>
    </Page>
  );
}
