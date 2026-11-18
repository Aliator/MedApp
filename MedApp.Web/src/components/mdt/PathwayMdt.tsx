import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {Skeleton} from '@astryxdesign/core/Skeleton';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {problemMessage} from '../../api/errors';
import {mdtMeetingsQuery, pathwayMdtItemsQuery, useListForMdt} from '../../api/mdt';
import type {Pathway} from '../../api/pathways';
import {useAnnounce} from '../../app/announcements';
import {useCan} from '../../auth/auth';
import {AppIcon} from '../shared/icons';
import {ListForMdtDialog} from './MdtDialogs';
import {MdtItemList} from './MdtItemList';

/** Statuses a pathway can't be listed for an MDT in: discharged, cancelled or archived (the API's 409 otherwise). */
const CLOSED = new Set(['Completed', 'Cancelled', 'Archived']);

/**
 * The pathway page's MDT section (decisions 141 to 143): **List for MDT** (holders of WorkPathways, while the pathway
 * isn't closed and the record can change), then its MDT items, newest listed first, with the question, the meeting,
 * the outcome as written and who was present, and each item's actions. Reading them records a view of the pathway.
 */
export function PathwayMdt({pathway}: {pathway: Pathway}) {
  const pathwayId = pathway.id ?? '';
  const items = useQuery(pathwayMdtItemsQuery(pathwayId));
  const meetings = useQuery(mdtMeetingsQuery());
  const listForMdt = useListForMdt();
  const announce = useAnnounce();
  const [isListing, setIsListing] = useState(false);
  const canList = useCan('WorkPathways') && !CLOSED.has(pathway.status ?? '') && !pathway.readOnlyReason;

  return (
    <VStack gap={3}>
      {canList && (
        <Button
          label="List for MDT"
          variant="secondary"
          size="sm"
          icon={<Icon icon={AppIcon.Mdt} />}
          onClick={() => setIsListing(true)}
        />
      )}
      {items.isError ? (
        // Plain words, not an alert: the rest of the page works without them.
        <Text type="body" color="secondary">{problemMessage(items.error, "The pathway's MDT items could not be loaded.")}</Text>
      ) : !items.data ? (
        <Skeleton height="var(--spacing-8)" />
      ) : (
        <MdtItemList
          items={items.data}
          meetings={meetings.data ?? []}
          isLoadingMeetings={meetings.isLoading}
          emptyText="Not listed for an MDT."
        />
      )}
      {isListing && (
        <ListForMdtDialog
          patient={pathway.patient}
          pathwayTitle={pathway.title ?? 'this pathway'}
          meetings={meetings.data ?? []}
          isLoadingMeetings={meetings.isLoading}
          isProcessing={listForMdt.isPending}
          onConfirm={(input, fail) =>
            listForMdt.mutate(
              {pathwayId, ...input},
              {
                onSuccess: () => {
                  setIsListing(false);
                  announce('Listed for MDT');
                },
                onError: fail,
              },
            )
          }
          onCancel={() => setIsListing(false)}
        />
      )}
    </VStack>
  );
}
