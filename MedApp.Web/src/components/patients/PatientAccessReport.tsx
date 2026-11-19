import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {List, ListItem} from '@astryxdesign/core/List';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {activityActionLabel, activitySearchQuery, describeActivity} from '../../api/activity';
import {formatDateTime} from '../../utils/dates';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';
import {CardSection} from '../views/CardSection';

/** How many of the latest entries the report shows; the rest are a click away in the activity log. */
export const ACCESS_REPORT_SIZE = 10;

/** The report's query: the patient's latest activity log entries (the page's loader fetches it too, for admins). */
export const patientAccessQuery = (patientId: string) =>
  activitySearchQuery({PatientId: patientId, Page: 1, PageSize: ACCESS_REPORT_SIZE, Ascending: false});

/**
 * Who has accessed a patient's record, and when (admins only): the latest entries about the patient from the activity
 * log (views, appearances in search results, changes and exports), newest first, with a link to all of them. For
 * answering "who has seen my record?", and spotting access that has no reason behind it. A titled section: in a card
 * (the patient's details card) its title is a header bar, as the details' own sections have.
 */
export function PatientAccessReport({patientId}: {patientId: string}) {
  const {data, isPending, isError} = useQuery(patientAccessQuery(patientId));
  const entries = data?.items ?? [];
  const total = data?.totalCount ?? 0;

  return (
    <CardSection title="Access Report">
      <VStack gap={4} hAlign="stretch">
        {isError ? (
          <AlertMessage label="Access report not loaded" message="The access report could not be loaded. Please try again." />
        ) : isPending ? (
          <Text type="body" color="secondary">
            Loading who has accessed this record...
          </Text>
        ) : entries.length === 0 ? (
          <Text type="body" color="secondary">
            Nobody has accessed this record since access started being recorded.
          </Text>
        ) : (
          <List hasDividers edge="bleed">
            {entries.map((entry) => {
              const details = describeActivity(entry);
              return (
                <ListItem
                  key={entry.id}
                  align="first-line"
                  label={
                    <Text type="body" weight="semibold">
                      {entry.username || '—'}
                    </Text>
                  }
                  description={
                    <Text type="supporting" color="secondary">
                      {[activityActionLabel(entry.action), details].filter(Boolean).join(': ')}
                    </Text>
                  }
                  endContent={
                    <Text type="supporting" color="secondary">
                      {entry.atUtc ? formatDateTime(entry.atUtc) : ''}
                    </Text>
                  }
                />
              );
            })}
          </List>
        )}
        <HStack gap={2} vAlign="center" hAlign="end">
          {total > entries.length && (
            <Text type="supporting" color="secondary">
              Showing the latest {entries.length} of {total}.
            </Text>
          )}
          <Button
            label="View All in the Activity Log"
            variant="secondary"
            icon={<Icon icon={AppIcon.AuditLog} />}
            href={`/admin/activity?patientId=${patientId}`}
          />
        </HStack>
      </VStack>
    </CardSection>
  );
}
