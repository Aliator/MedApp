import {Button} from '@astryxdesign/core/Button';
import {List, ListItem} from '@astryxdesign/core/List';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {formatDate} from '../../utils/dates';
import {CardSection} from '../views/CardSection';

export interface Orthotic {
  id: string;
  name: string;
  madeOn: string;
  status: string;
}

export const orthoticsQuery = (patientId: string) => ({
  queryKey: ['patients', patientId, 'orthotics'],
  queryFn: async (): Promise<Orthotic[]> =>
    __LIGHT_DEMO__ ? (await import('../../demo/lowerLimb')).orthoticsFor(patientId) : [],
});

export function CustomOrthotics({patientId}: {patientId: string}) {
  const {data} = useQuery(orthoticsQuery(patientId));
  if (!data || data.length === 0) return null;
  return (
    <CardSection title="Custom Orthotics">
      <List hasDividers density="compact" edge="flush" aria-label="Custom orthotics">
        {data.map((orthotic) => (
          <ListItem
            key={orthotic.id}
            label={
              <Text type="body" maxLines={2}>
                {orthotic.name}
              </Text>
            }
            description={
              <Text type="supporting" color="secondary">
                {`${orthotic.status}, made ${formatDate(orthotic.madeOn)}`}
              </Text>
            }
            endContent={
              <Button
                label="View"
                variant="secondary"
                size="sm"
                href={`/patients/${patientId}/orthotics/${orthotic.id}`}
                aria-label={`View ${orthotic.name}`}
              />
            }
          />
        ))}
      </List>
    </CardSection>
  );
}
