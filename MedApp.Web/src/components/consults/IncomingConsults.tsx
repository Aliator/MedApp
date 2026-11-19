import {Card} from '@astryxdesign/core/Card';
import {Section} from '@astryxdesign/core/Section';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {incomingConsultsQuery} from '../../api/consults';
import {AlertMessage} from '../shared/AlertMessage';
import {PanelHeader} from '../views/PanelCard';
import {ConsultList} from './ConsultList';

export interface IncomingConsultsProps {
  /** The card's title: "Consults for the lab", "Consults for your team". */
  title: string;
  /** One of the viewer's teams, or all of them. */
  teamId?: string;
}

/**
 * A team's incoming consults (MC25), for its home: every open consult asked of the viewer's teams, oldest asked first,
 * each with the patient, a link to the pathway (readable while the consult is open), the question, and Reply. A replied
 * one stays until the asking team closes it. Built for any team: the lab's home has it first.
 */
export function IncomingConsults({title, teamId}: IncomingConsultsProps) {
  const consults = useQuery(incomingConsultsQuery(teamId));
  const items = consults.data ?? [];
  return (
    <Card padding={0} width="100%">
      <PanelHeader title={title} summary={consults.data ? <Text type="supporting" color="secondary">{`${items.length}`}</Text> : null} />
      <Section variant="transparent" padding={6}>
        {consults.isError ? (
          <AlertMessage message={`${title} could not be loaded. Please try again.`} />
        ) : consults.isPending ? (
          <Text type="body" color="secondary">Loading...</Text>
        ) : (
          <ConsultList consults={items} showsPathway emptyText="No open consults. Another team's questions about their pathways show here." />
        )}
      </Section>
    </Card>
  );
}
