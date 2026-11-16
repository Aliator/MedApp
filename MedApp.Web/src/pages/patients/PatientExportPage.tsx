import {Button} from '@astryxdesign/core/Button';
import {Card} from '@astryxdesign/core/Card';
import {Icon} from '@astryxdesign/core/Icon';
import {List, ListItem} from '@astryxdesign/core/List';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {Section} from '@astryxdesign/core/Section';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery, useQueryClient} from '@tanstack/react-query';
import {useEffect, type ReactNode} from 'react';
import {activityActionLabel, describeActivity} from '../../api/activity';
import {auditActionLabel, describeAuditDetails} from '../../api/audit';
import {ApiError} from '../../api/errors';
import {patientExportQuery, type PatientExport} from '../../api/patients';
import {Page} from '../../components/pages/Page';
import {statusLabel} from '../../components/procedures/ProcedureCells';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {RichText} from '../../components/shared/RichText';
import {PanelHeader} from '../../components/views/PanelCard';
import {formatDate, formatDateTime} from '../../utils/dates';

/** While printing, only the export shows: the side nav and the page's buttons are left off the paper. */
const PRINT_CSS = '@media print { .astryx-side-nav, button { display: none !important; } }';

/** The export's file name: the patient's ID and the day, never their name. */
export const exportFileName = (data: PatientExport) =>
  `patient-${data.patient?.id ?? 'record'}-${(data.exportedAtUtc ?? '').slice(0, 10) || 'export'}.json`;

/** Saves the export as a JSON file, through a link to it in memory (nothing is sent anywhere). */
export function downloadExport(data: PatientExport) {
  const url = URL.createObjectURL(new Blob([JSON.stringify(data, null, 2)], {type: 'application/json'}));
  const link = document.createElement('a');
  link.href = url;
  link.download = exportFileName(data);
  link.click();
  URL.revokeObjectURL(url);
}

const when = (iso: string | null | undefined) => (iso ? formatDateTime(iso) : '—');

/** One titled part of the export, as a card. */
function ExportSection({title, children}: {title: string; children: ReactNode}) {
  return (
    <Card padding={0} width="100%">
      <VStack gap={0}>
        <PanelHeader title={title} />
        <Section variant="transparent" padding={6}>
          {children}
        </Section>
      </VStack>
    </Card>
  );
}

const nothing = (text: string) => (
  <Text type="body" color="secondary">
    {text}
  </Text>
);

/**
 * A subject access export (admin): everything MedApp holds about a patient (their details, procedures with stages,
 * notes and assignments, the history of changes to them, who has accessed the record, and notifications about them),
 * readable on screen, printable, and downloadable as JSON. Opening it is recorded in the activity log, once: it's
 * fetched once per visit, and the download and print use that copy. Before it's given to the patient, a clinician
 * should check it for anything that meets the serious harm test, and for other people's details.
 */
export function PatientExportPage({id}: {id: string}) {
  const {data, error, isPending} = useQuery(patientExportQuery(id));
  const queryClient = useQueryClient();
  // The export holds the whole clinical record: it's dropped from the cache as soon as the page closes.
  useEffect(() => () => queryClient.removeQueries({queryKey: patientExportQuery(id).queryKey}), [queryClient, id]);
  const isMissing = error instanceof ApiError && error.status === 404;

  return (
    <Page
      title="Patient Record Export"
      subtitle={data ? `Exported ${when(data.exportedAtUtc)} by ${data.exportedBy}` : undefined}
      backHref={`/patients/${id}`}
      backLabel="Back to patient"
      isLoading={isPending}
      loadingMessage="Gathering everything held about this patient..."
      message={
        error ? (
          <AlertMessage message={isMissing ? 'Patient not found. They may have been deleted.' : 'The export could not be made. Please try again.'} />
        ) : (
          data && (
            <AlertMessage
              status="info"
              message="Before giving this to the patient, have a clinician check it for anything that could cause serious harm, and for other people's details."
            />
          )
        )
      }
      actions={
        data && (
          <HStack gap={2}>
            <Button label="Print" variant="secondary" icon={<Icon icon={AppIcon.View} />} onClick={() => window.print()} />
            <Button label="Download JSON" variant="primary" icon={<Icon icon={AppIcon.Save} />} onClick={() => downloadExport(data)} />
          </HStack>
        )
      }>
      <style data-medapp-print="">{PRINT_CSS}</style>
      {data && <ExportContent data={data} />}
    </Page>
  );
}

function ExportContent({data}: {data: PatientExport}) {
  const patient = data.patient;
  const procedures = data.procedures ?? [];
  const history = data.procedureHistory ?? [];
  const access = data.accessLog ?? [];
  const notifications = data.notifications ?? [];

  return (
    <VStack gap={6} hAlign="stretch">
      <ExportSection title="Patient">
        <MetadataList columns={2} label={{position: 'top'}}>
          <MetadataListItem label="Name">{`${patient?.firstName ?? ''} ${patient?.lastName ?? ''}`.trim() || '—'}</MetadataListItem>
          <MetadataListItem label="Date of Birth">{patient?.dateOfBirth ? formatDate(patient.dateOfBirth) : '—'}</MetadataListItem>
          <MetadataListItem label="Email">{patient?.email || '—'}</MetadataListItem>
          <MetadataListItem label="Patient ID">{patient?.id}</MetadataListItem>
          <MetadataListItem label="Record Created">{when(patient?.createdAt)}</MetadataListItem>
          <MetadataListItem label="Last Changed">{when(patient?.lastUpdated)}</MetadataListItem>
          {patient?.restrictedAtUtc && (
            <MetadataListItem label="Restricted">{`${when(patient.restrictedAtUtc)}: ${patient.restrictionReason ?? ''}`}</MetadataListItem>
          )}
          {patient?.disputedAtUtc && (
            <MetadataListItem label="Disputed">{`${when(patient.disputedAtUtc)}: ${patient.disputeReason ?? ''}`}</MetadataListItem>
          )}
          {patient?.anonymisedAtUtc && <MetadataListItem label="Anonymised">{when(patient.anonymisedAtUtc)}</MetadataListItem>}
          {patient?.retentionKeptUntil && (
            <MetadataListItem label="Kept Past Retention Until">
              {`${formatDate(patient.retentionKeptUntil)}: ${patient.retentionKeptReason ?? ''}`}
            </MetadataListItem>
          )}
        </MetadataList>
      </ExportSection>

      <ExportSection title={`Procedures (${procedures.length})`}>
        {procedures.length === 0
          ? nothing('No procedures.')
          : (
            <VStack gap={6} hAlign="stretch">
              {procedures.map((procedure) => (
                <VStack key={procedure.id} gap={3} hAlign="stretch">
                  <Text type="body" weight="semibold">
                    {procedure.title}
                  </Text>
                  <MetadataList columns={2} label={{position: 'top'}}>
                    <MetadataListItem label="Template">{procedure.templateName || '—'}</MetadataListItem>
                    <MetadataListItem label="Status">{statusLabel(procedure.status ?? '')}</MetadataListItem>
                    <MetadataListItem label="Priority">{procedure.priority}</MetadataListItem>
                    <MetadataListItem label="Due">{procedure.dueDate ? formatDate(procedure.dueDate) : '—'}</MetadataListItem>
                    <MetadataListItem label="Created">{when(procedure.createdAt)}</MetadataListItem>
                    <MetadataListItem label="Assigned To">
                      {(procedure.assignments ?? []).map((a) => a.username).join(', ') || 'Nobody'}
                    </MetadataListItem>
                  </MetadataList>
                  {procedure.notes && <RichText>{procedure.notes}</RichText>}
                  <List hasDividers edge="bleed">
                    {(procedure.stages ?? []).map((stage) => (
                      <ListItem
                        key={stage.id}
                        align="first-line"
                        label={
                          <Text type="body" weight="semibold">
                            {`${stage.stageOrder}. ${stage.name}`}
                          </Text>
                        }
                        description={
                          <VStack gap={1}>
                            <Text type="supporting" color="secondary">
                              {[
                                stage.startedAtUtc ? `Started ${when(stage.startedAtUtc)} by ${stage.startedByUsername}` : 'Not started',
                                stage.completedAtUtc ? `finished ${when(stage.completedAtUtc)} by ${stage.completedByUsername}` : null,
                              ]
                                .filter(Boolean)
                                .join(', ')}
                            </Text>
                            {stage.notes ? <RichText>{stage.notes}</RichText> : null}
                          </VStack>
                        }
                      />
                    ))}
                  </List>
                </VStack>
              ))}
            </VStack>
          )}
      </ExportSection>

      <ExportSection title={`Procedure History (${history.length})`}>
        {history.length === 0
          ? nothing('No changes recorded.')
          : (
            <List hasDividers edge="bleed">
              {history.map((entry) => (
                <ListItem
                  key={entry.id}
                  align="first-line"
                  label={<Text type="body">{`${when(entry.atUtc)}: ${auditActionLabel(entry.action)} by ${entry.username}`}</Text>}
                  description={
                    <Text type="supporting" color="secondary">
                      {[entry.procedureTitle, entry.stageName, describeAuditDetails(entry)].filter(Boolean).join(' · ')}
                    </Text>
                  }
                />
              ))}
            </List>
          )}
      </ExportSection>

      <ExportSection title={`Access Log (${access.length})`}>
        {access.length === 0
          ? nothing('No access recorded.')
          : (
            <List hasDividers edge="bleed">
              {access.map((entry) => (
                <ListItem
                  key={entry.id}
                  align="first-line"
                  label={<Text type="body">{`${when(entry.atUtc)}: ${activityActionLabel(entry.action)} by ${entry.username}`}</Text>}
                  description={
                    describeActivity(entry) ? (
                      <Text type="supporting" color="secondary">
                        {describeActivity(entry)}
                      </Text>
                    ) : undefined
                  }
                />
              ))}
            </List>
          )}
      </ExportSection>

      <ExportSection title={`Notifications (${notifications.length})`}>
        {notifications.length === 0
          ? nothing('No notifications.')
          : (
            <List hasDividers edge="bleed">
              {notifications.map((n) => (
                <ListItem
                  key={n.id}
                  align="first-line"
                  label={<Text type="body">{`${when(n.createdAtUtc)}: to ${n.username}`}</Text>}
                  description={
                    <Text type="supporting" color="secondary">
                      {`${n.title}: ${n.message}`}
                    </Text>
                  }
                />
              ))}
            </List>
          )}
      </ExportSection>
    </VStack>
  );
}
