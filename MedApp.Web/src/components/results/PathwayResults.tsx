import {Link} from '@astryxdesign/core/Link';
import {List, ListItem} from '@astryxdesign/core/List';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {pathwayResultsQuery} from '../../api/results';
import {formatDateTime} from '../../utils/dates';
import {AlertMessage} from '../shared/AlertMessage';
import {NeedsClinicianToken, ResultStatusToken} from './ResultCells';

/**
 * A pathway's results (the pathway page's Results section), newest report first: each one's title (opening its page),
 * where it came from, when it was reported, its responsible clinician and status, with Needs a clinician. Dates and
 * statuses only: nothing about what a report says. It lays out no padding of its own.
 */
export function PathwayResults({pathwayId}: {pathwayId: string}) {
  const {data, isPending, isError} = useQuery(pathwayResultsQuery(pathwayId));

  if (isError) return <AlertMessage message="The results could not be loaded. Please try again." />;
  if (isPending)
    return (
      <Text type="body" color="secondary">
        Loading results...
      </Text>
    );
  const results = data?.items ?? [];
  if (results.length === 0)
    return (
      <Text type="body" color="secondary">
        No results yet.
      </Text>
    );

  return (
    <List edge="flush" hasDividers aria-label="Results">
      {results.map((result) => (
        <ListItem
          key={result.id}
          align="first-line"
          label={
            <Text type="body" weight="semibold">
              <Link href={`/results/${result.id}`}>{result.title}</Link>
            </Text>
          }
          description={
            <VStack gap={1}>
              <Text type="supporting" color="secondary">
                {`${result.specimenNumber ? `Sample ${result.specimenNumber}` : 'External report'} · reported ${formatDateTime(result.reportedAtUtc ?? '')}`}
              </Text>
              <Text type="supporting" color="secondary">
                {`Responsible clinician: ${result.responsibleUsername ?? ''}`}
              </Text>
            </VStack>
          }
          endContent={
            <HStack gap={2} vAlign="center" wrap="wrap">
              <ResultStatusToken status={result.status} />
              {result.needsClinician && <NeedsClinicianToken />}
            </HStack>
          }
        />
      ))}
    </List>
  );
}
