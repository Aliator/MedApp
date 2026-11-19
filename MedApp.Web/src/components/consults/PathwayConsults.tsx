import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {Skeleton} from '@astryxdesign/core/Skeleton';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {isOpenConsult, pathwayConsultsQuery, useRequestConsult} from '../../api/consults';
import {problemMessage} from '../../api/errors';
import type {Pathway} from '../../api/pathways';
import {teamsQuery} from '../../api/teams';
import {useAnnounce} from '../../app/announcements';
import {AppIcon} from '../shared/icons';
import {AskConsultDialog} from './ConsultDialogs';
import {ConsultList} from './ConsultList';

/**
 * The pathway page's Consults section (MC25): **Ask Another Team** (when the API says the viewer may: the pathway's own
 * team, with WorkPathways, while it's open and the record can change), then its consults, newest asked first, with their
 * replies and actions. How many are open shows first, so a reply isn't missed (H-75).
 */
export function PathwayConsults({pathway}: {pathway: Pathway}) {
  const pathwayId = pathway.id ?? '';
  const consults = useQuery(pathwayConsultsQuery(pathwayId));
  const [isAsking, setIsAsking] = useState(false);
  const teams = useQuery({...teamsQuery(false), enabled: isAsking});
  const request = useRequestConsult();
  const announce = useAnnounce();
  const open = (consults.data?.consults ?? []).filter(isOpenConsult);
  const replied = open.filter((c) => c.status === 'Replied').length;
  // Every active team but the pathway's own, by name: none is suggested.
  const askable = (teams.data ?? [])
    .filter((t) => t.id !== pathway.teamId && !t.isArchived)
    .sort((a, b) => (a.name ?? '').localeCompare(b.name ?? ''));

  return (
    <VStack gap={3}>
      {consults.data?.canRequest && (
        <HStack>
          <Button
            label="Ask Another Team"
            variant="secondary"
            size="sm"
            icon={<Icon icon={AppIcon.Question} />}
            onClick={() => setIsAsking(true)}
          />
        </HStack>
      )}
      {consults.isError ? (
        // Plain words, not an alert: the rest of the page works without them.
        <Text type="body" color="secondary">{problemMessage(consults.error, "The pathway's consults could not be loaded.")}</Text>
      ) : !consults.data ? (
        <Skeleton height="var(--spacing-8)" />
      ) : (
        <>
          {open.length > 0 && (
            <Text type="body" weight="semibold">
              {`${open.length} open${replied > 0 ? `, ${replied} replied and waiting to be closed` : ''}`}
            </Text>
          )}
          <ConsultList consults={consults.data.consults ?? []} emptyText="No consults asked." />
        </>
      )}
      {isAsking && (
        <AskConsultDialog
          patient={pathway.patient}
          pathwayTitle={pathway.title ?? 'this pathway'}
          teams={askable}
          isLoadingTeams={teams.isLoading}
          isProcessing={request.isPending}
          onConfirm={(input, fail) =>
            request.mutate(
              {pathwayId, ...input},
              {
                onSuccess: () => {
                  setIsAsking(false);
                  announce('Consult asked');
                },
                onError: fail,
              },
            )
          }
          onCancel={() => setIsAsking(false)}
        />
      )}
    </VStack>
  );
}
