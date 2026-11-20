import {Card} from '@astryxdesign/core/Card';
import {Icon} from '@astryxdesign/core/Icon';
import {List, ListItem} from '@astryxdesign/core/List';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {dataRequestCountQuery} from '../../api/dataRequests';
import {labCaseCountQuery} from '../../api/labCases';
import {letterCountQuery} from '../../api/letters';
import {awaitingTriageCountQuery} from '../../api/referrals';
import {resultCountQuery} from '../../api/results';
import {useCan} from '../../auth/auth';
import {AppIcon} from '../shared/icons';
import {PanelHeader} from '../views/PanelCard';

/** One line of the card: how many, and where to deal with them. */
export interface AttentionLine {
  key: string;
  count: number;
  /** What they are, after the number ("results awaiting your sign-off"); singular when there's one. */
  words: (count: number) => string;
  href: string;
  icon: (typeof AppIcon)[keyof typeof AppIcon];
}

const plural = (count: number, one: string, many: string) => (count === 1 ? one : many);

/**
 * The lines with something to do, from the counts the person may see (undefined: not theirs, or not loaded yet).
 * Each count names no patients (the API records nothing for it); only those above zero are lines.
 */
export function attentionLines(counts: {
  resultsAwaitingMine?: number;
  lettersToApprove?: number;
  lettersToSend?: number;
  referralsAwaitingTriage?: number;
  inYourTeams?: boolean;
  labCasesAtRisk?: number;
  labCasesOverdue?: number;
  labCasesToReport?: number;
  dataRequestsOpen?: number;
  dataRequestsDueSoon?: number;
}): AttentionLine[] {
  const lines: AttentionLine[] = [
    {
      key: 'results',
      count: counts.resultsAwaitingMine ?? 0,
      words: (n) => `${plural(n, 'result', 'results')} awaiting your sign-off`,
      href: '/results',
      icon: AppIcon.Results,
    },
    {
      key: 'letters-to-approve',
      count: counts.lettersToApprove ?? 0,
      words: (n) => `${plural(n, 'letter', 'letters')} to approve`,
      href: '/letters',
      icon: AppIcon.Letters,
    },
    {
      key: 'letters-to-send',
      count: counts.lettersToSend ?? 0,
      words: (n) => `${plural(n, 'letter', 'letters')} to send`,
      href: '/letters',
      icon: AppIcon.Letters,
    },
    {
      key: 'referrals',
      count: counts.referralsAwaitingTriage ?? 0,
      words: (n) => `${plural(n, 'referral', 'referrals')} awaiting triage${counts.inYourTeams ? ' in your teams' : ''}`,
      href: '/referrals',
      icon: AppIcon.Referrals,
    },
    {
      key: 'lab-cases-overdue',
      count: counts.labCasesOverdue ?? 0,
      words: (n) => `lab ${plural(n, 'case', 'cases')} overdue`,
      href: '/lab/cases?view=Overdue',
      icon: AppIcon.LabCases,
    },
    {
      key: 'lab-cases-at-risk',
      count: counts.labCasesAtRisk ?? 0,
      words: (n) => `lab ${plural(n, 'case', 'cases')} at risk`,
      href: '/lab/cases?view=AtRisk',
      icon: AppIcon.LabCases,
    },
    {
      key: 'lab-cases-to-report',
      count: counts.labCasesToReport ?? 0,
      words: (n) => `lab ${plural(n, 'case', 'cases')} ready to report`,
      href: '/lab/cases?view=ToReport',
      icon: AppIcon.LabCases,
    },
    {
      key: 'data-requests-due-soon',
      count: counts.dataRequestsDueSoon ?? 0,
      words: (n) => `${plural(n, 'data request', 'data requests')} due within 7 days or overdue`,
      href: '/data-requests',
      icon: AppIcon.DataRequests,
    },
    {
      key: 'data-requests-open',
      count: counts.dataRequestsOpen ?? 0,
      words: (n) => `${plural(n, 'data request', 'data requests')} open`,
      href: '/data-requests',
      icon: AppIcon.DataRequests,
    },
  ];
  return lines.filter((line) => line.count > 0);
}

/**
 * The home page's "Needs your attention" card (the owner, 18 November 2026): what used to be the side nav's count
 * badges, as lines that each link to their page. Results awaiting your sign-off (SignOffResults), letters to approve
 * (ApproveLetters) and to send (SendLetters), referrals awaiting triage (TriageReferrals), lab cases overdue, at risk
 * and ready to report (ProcessSpecimens), and data requests due soon and open (ManagePrivacy), each only for those who
 * can act on it and can view patients (as the pages they link to need). Counts only, naming no patients. The card isn't
 * there while there's nothing to do (or nothing has loaded).
 */
export function NeedsAttention() {
  const canView = useCan('ViewPatients');
  const canSignOff = useCan('SignOffResults') && canView;
  const canApprove = useCan('ApproveLetters') && canView;
  const canSend = useCan('SendLetters') && canView;
  const canTriage = useCan('TriageReferrals') && canView;
  const canWorkLabCases = useCan('ProcessSpecimens') && canView;
  const canHandleDataRequests = useCan('ManagePrivacy') && canView;
  const results = useQuery({...resultCountQuery, enabled: canSignOff}).data;
  const letters = useQuery({...letterCountQuery, enabled: canApprove || canSend}).data;
  const referrals = useQuery({...awaitingTriageCountQuery, enabled: canTriage}).data;
  const labCases = useQuery({...labCaseCountQuery, enabled: canWorkLabCases}).data;
  const dataRequests = useQuery({...dataRequestCountQuery, enabled: canHandleDataRequests}).data;

  const lines = attentionLines({
    resultsAwaitingMine: canSignOff ? results?.awaitingMine : undefined,
    lettersToApprove: canApprove ? letters?.toApprove : undefined,
    lettersToSend: canSend ? letters?.toSend : undefined,
    referralsAwaitingTriage: canTriage ? referrals?.count : undefined,
    inYourTeams: referrals?.inYourTeams,
    labCasesAtRisk: canWorkLabCases ? labCases?.atRisk : undefined,
    labCasesOverdue: canWorkLabCases ? labCases?.overdue : undefined,
    labCasesToReport: canWorkLabCases ? labCases?.toReport : undefined,
    dataRequestsOpen: canHandleDataRequests ? dataRequests?.open : undefined,
    dataRequestsDueSoon: canHandleDataRequests ? dataRequests?.dueSoon : undefined,
  });
  if (lines.length === 0) return null;

  return (
    <Card padding={0} width="100%">
      <PanelHeader title="Needs your attention" />
      <List edge="bleed" hasDividers aria-label="Needs your attention">
        {lines.map((line) => (
          <ListItem
            key={line.key}
            href={line.href}
            startContent={<Icon icon={line.icon} color="secondary" />}
            label={<Text type="body">{`${line.count} ${line.words(line.count)}`}</Text>}
          />
        ))}
      </List>
    </Card>
  );
}
