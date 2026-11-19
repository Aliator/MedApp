import {Banner} from '@astryxdesign/core/Banner';
import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {List, ListItem} from '@astryxdesign/core/List';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Heading, Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import type {Pathway, PathwayStage} from '../../api/pathways';
import {pathwaySpecimensQuery, specimenPrintHref, type Specimen, type SpecimenSummary} from '../../api/specimens';
import {useCan} from '../../auth/auth';
import {formatDateTime} from '../../utils/dates';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';
import {OverdueAtLabToken, SpecimenStatusToken} from './SpecimenCells';
import {SpecimenDetailsDialog} from './SpecimenDetailsDialog';

/** A stage and the specimens it took, oldest first. */
interface StageSpecimens {
  stage: PathwayStage;
  specimens: SpecimenSummary[];
}

/**
 * The stages that took specimens, or can take one now, in the pathway's order, each with its specimens oldest first.
 * A specimen whose stage the pathway no longer has (it can't, but just in case) is left out.
 */
export function specimensByStage(stages: readonly PathwayStage[], specimens: readonly SpecimenSummary[]): StageSpecimens[] {
  return stages
    .map((stage) => ({
      stage,
      specimens: specimens
        .filter((specimen) => specimen.stageId === stage.id)
        .sort((a, b) => (a.takenAtUtc ?? '').localeCompare(b.takenAtUtc ?? '')),
    }))
    .filter(({stage, specimens}) => specimens.length > 0 || stage.canRecordSpecimen);
}

/**
 * A pathway's specimens (the pathway page's Specimens section), by the stage that took them: each one's number
 * (opening its page), status, type, lab and when it was taken, and Overdue at lab. Record Specimen on each stage that
 * can take one now (the API's `canRecordSpecimen`, for holders of RecordSpecimens) opens `SpecimenDetailsDialog`; once
 * it's recorded, the section says so, with links to the specimen and its label. It lays out no padding of its own.
 */
export function PathwaySpecimens({pathway}: {pathway: Pathway}) {
  const canRecord = useCan('RecordSpecimens');
  const {data, isPending, isError} = useQuery(pathwaySpecimensQuery(pathway.id ?? ''));
  const [recordingAt, setRecordingAt] = useState<PathwayStage | null>(null);
  const [recorded, setRecorded] = useState<Specimen | null>(null);

  if (isError) return <AlertMessage label="Samples not loaded" />;
  if (isPending)
    return (
      <Text type="body" color="secondary">
        Loading samples...
      </Text>
    );

  const groups = specimensByStage(pathway.stages ?? [], data?.items ?? []);
  const record = (stage: PathwayStage) => {
    setRecorded(null);
    setRecordingAt(stage);
  };

  return (
    <VStack gap={4} hAlign="stretch">
      {recorded && (
        <Banner
          status="success"
          container="card"
          title={`Sample ${recorded.number} recorded`}
          description={
            <HStack gap={4} wrap="wrap">
              <Link href={`/specimens/${recorded.id}`}>Open the sample</Link>
              <Link href={specimenPrintHref(recorded.id ?? '', 'label')}>Print its label</Link>
            </HStack>
          }
          isDismissable
          onDismiss={() => setRecorded(null)}
        />
      )}
      {groups.length === 0 ? (
        <Text type="body" color="secondary">
          No samples taken.
        </Text>
      ) : (
        groups.map(({stage, specimens}) => (
          <VStack key={stage.id} gap={2} hAlign="stretch">
            <HStack gap={3} vAlign="center" wrap="wrap" justify="between">
              <Heading level={3}>{stage.name}</Heading>
              {canRecord && stage.canRecordSpecimen && (
                <Button
                  label="Record Sample"
                  tooltip={`Record a sample taken at ${stage.name}`}
                  variant="secondary"
                  size="sm"
                  icon={<Icon icon={AppIcon.Specimens} />}
                  onClick={() => record(stage)}
                />
              )}
            </HStack>
            {specimens.length === 0 ? (
              <Text type="supporting" color="secondary">
                None taken yet.
              </Text>
            ) : (
              <List edge="flush" hasDividers aria-label={`Samples taken at ${stage.name}`}>
                {specimens.map((specimen) => (
                  <ListItem
                    key={specimen.id}
                    align="first-line"
                    label={
                      <Text type="body" weight="semibold">
                        <Link href={`/specimens/${specimen.id}`}>{specimen.number}</Link>
                      </Text>
                    }
                    description={
                      <VStack gap={1}>
                        <Text type="supporting" color="secondary">
                          {specimen.specimenTypeName}
                        </Text>
                        <Text type="supporting" color="secondary">
                          {`Taken ${formatDateTime(specimen.takenAtUtc ?? '')}`}
                        </Text>
                      </VStack>
                    }
                    endContent={
                      <HStack gap={2} vAlign="center" wrap="wrap">
                        <SpecimenStatusToken status={specimen.status} />
                        {specimen.isOverdueAtLab && <OverdueAtLabToken />}
                      </HStack>
                    }
                  />
                ))}
              </List>
            )}
          </VStack>
        ))
      )}
      {/* Mounted only while it's open, so it starts afresh at now each time. */}
      {recordingAt && (
        <SpecimenDetailsDialog
          isOpen
          onClose={() => setRecordingAt(null)}
          patient={pathway.patient}
          record={{pathwayId: pathway.id ?? '', pathwayTitle: pathway.title ?? '', stageId: recordingAt.id ?? '', stageName: recordingAt.name ?? ''}}
          onSaved={setRecorded}
        />
      )}
    </VStack>
  );
}
