import {Button} from '@astryxdesign/core/Button';
import {Card} from '@astryxdesign/core/Card';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {MetadataListItem} from '@astryxdesign/core/MetadataList';
import {ProgressBar} from '@astryxdesign/core/ProgressBar';
import {Section} from '@astryxdesign/core/Section';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {useMediaQuery} from '@astryxdesign/core/hooks';
import {Heading, Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import type {ReactNode} from 'react';
import {ApiError} from '../../api/errors';
import {procedureQuery, type ProcedureStage} from '../../api/procedures';
import {NARROW_WINDOW_QUERY, Page} from '../../components/pages/Page';
import {TwoColumns} from '../../components/pages/TwoColumns';
import {DueDateCell, PriorityToken, StatusToken, statusLabel} from '../../components/procedures/ProcedureCells';
import {StageDetails, StageList, StageTitle} from '../../components/procedures/StageList';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {ViewCard} from '../../components/views/ViewCard';
import {ViewSection} from '../../components/views/ViewSection';
import {formatDateTime} from '../../utils/dates';

/**
 * One procedure: its details, who is assigned and how far through it is, beside its stages in order with their
 * notes. Like Home, the two columns fill the page and scroll on their own; a narrow window stacks them.
 * Read-only for now; working on it (notes, finishing stages, status changes) comes in the next phase.
 */
export function ProcedurePage({id}: {id: string}) {
  const {data: procedure, error, isPending} = useQuery(procedureQuery(id));
  const isMissing = error instanceof ApiError && error.status === 404;
  const isNarrow = useMediaQuery(NARROW_WINDOW_QUERY, false);
  const fill = !isNarrow && procedure !== undefined;
  const currentStage = procedure?.stages?.find((s) => s.id === procedure.currentStageId);

  return (
    <Page
      title={procedure?.title ?? 'Procedure'}
      backHref="/procedures"
      backLabel="Back to procedures"
      isLoading={isPending}
      loadingMessage="Loading procedure..."
      fill={fill}>
      {isMissing && <AlertMessage message="Procedure not found. It may have been deleted." />}
      {error && !isMissing && <AlertMessage message="The procedure could not be loaded. Please try again." />}
      {procedure && (
        <Layout isSideBySide={fill}>
          <ViewCard
            fill={fill}
            footerActions={
              <Button
                label="View Patient"
                variant="secondary"
                icon={<Icon icon={AppIcon.View} />}
                href={`/patients/${procedure.patientId}`}
              />
            }>
            <HStack gap={2} vAlign="center">
              <StatusToken status={procedure.status ?? ''} />
              <PriorityToken priority={procedure.priority ?? ''} />
              {procedure.status === 'Blocked' && procedure.blockedFromStatus && (
                <Text type="supporting" color="secondary">
                  Was {statusLabel(procedure.blockedFromStatus).toLowerCase()} before it was blocked
                </Text>
              )}
            </HStack>
            <ProgressBar
              label="Stages finished"
              value={procedure.completedStageCount ?? 0}
              max={Math.max(procedure.stageCount ?? 0, 1)}
              hasValueLabel
              formatValueLabel={(value) => `${value} of ${procedure.stageCount ?? 0}`}
            />
            <ViewSection title="Details">
              <MetadataListItem label="Patient">
                <Link href={`/patients/${procedure.patientId}`}>{procedure.patientName}</Link>
              </MetadataListItem>
              <MetadataListItem label="Template">{procedure.templateName || '—'}</MetadataListItem>
              <MetadataListItem label="Due">
                <DueDateCell dueDate={procedure.dueDate ?? ''} isOverdue={procedure.isOverdue ?? false} />
              </MetadataListItem>
              <MetadataListItem label="Assigned to">
                {procedure.assignments?.length
                  ? procedure.assignments.map((a) => a.username).join(', ')
                  : 'Nobody yet'}
              </MetadataListItem>
              <MetadataListItem label="Created">{formatDateTime(procedure.createdAt ?? '')}</MetadataListItem>
              <MetadataListItem label="Last updated">{formatDateTime(procedure.lastUpdated ?? '')}</MetadataListItem>
            </ViewSection>
            {procedure.notes?.trim() && (
              <ViewSection title="Notes" useGrid={false}>
                <MetadataListItem label="Procedure notes">{procedure.notes}</MetadataListItem>
              </ViewSection>
            )}
            <CurrentStage stage={currentStage} />
          </ViewCard>
          <Card padding={0} width="100%" height={fill ? '100%' : undefined}>
            <VStack gap={0} height={fill ? '100%' : undefined}>
              <Section variant="transparent" padding={6} dividers={['bottom']}>
                <VStack gap={1}>
                  <Heading level={2}>Stages</Heading>
                  <Text type="supporting" color="secondary">
                    Worked in order. A finished stage is locked unless an admin unlocks it.
                  </Text>
                </VStack>
              </Section>
              <StackItem size={fill ? 'fill' : 'static'} isScrollable={fill}>
                {/* The current stage is shown in full in the details card, so the list starts with every stage closed. */}
                <StageList stages={procedure.stages ?? []} isCurrentOpen={false} />
              </StackItem>
            </VStack>
          </Card>
        </Layout>
      )}
    </Page>
  );
}

/** The details and the stages: equal columns side by side, as on Home, or stacked for a narrow window. */
function Layout({isSideBySide, children}: {isSideBySide: boolean; children: [ReactNode, ReactNode]}) {
  return isSideBySide ? <TwoColumns start={children[0]} end={children[1]} /> : <VStack gap={6}>{children}</VStack>;
}

/** The stage being worked on, in full; or a note that every stage is finished. */
function CurrentStage({stage}: {stage: ProcedureStage | undefined}) {
  return (
    <VStack gap={4}>
      <Heading level={2}>Current stage</Heading>
      {stage ? (
        <VStack gap={3}>
          <StageTitle stage={stage} />
          <StageDetails stage={stage} />
        </VStack>
      ) : (
        <Text type="body" color="secondary">
          Every stage is finished.
        </Text>
      )}
    </VStack>
  );
}
