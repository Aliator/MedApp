import {Avatar} from '@astryxdesign/core/Avatar';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {HStack} from '@astryxdesign/core/Stack';
import type {ReactNode} from 'react';
import {formatDate} from '../../utils/dates';
import {formatNhsNumber} from '../../utils/nhsNumber';

export interface PatientSummaryProps {
  firstName?: string | null;
  lastName?: string | null;
  /** YYYY-MM-DD. */
  dateOfBirth?: string | null;
  email?: string | null;
  /** Shown (grouped 3, 3, 4) when given, as "Not given" when given blank or null, and left out when not given. */
  nhsNumber?: string | null;
  showDateOfBirth?: boolean;
  showEmail?: boolean;
  /** More MetadataListItems after the standard ones. */
  additionalItems?: ReactNode;
}

/** Who a patient is, at a glance (replaces Blazor's PatientSummary), e.g. inside a delete confirmation. */
export function PatientSummary({
  firstName,
  lastName,
  dateOfBirth,
  email,
  nhsNumber,
  showDateOfBirth = true,
  showEmail = true,
  additionalItems,
}: PatientSummaryProps) {
  const name = [firstName, lastName].filter(Boolean).join(' ').trim();

  return (
    <HStack gap={4} vAlign="start">
      <Avatar name={name || undefined} size="lg" tooltip={false} />
      <MetadataList>
        <MetadataListItem label="Name">{name || '—'}</MetadataListItem>
        {showDateOfBirth && dateOfBirth && <MetadataListItem label="Date of Birth">{formatDate(dateOfBirth)}</MetadataListItem>}
        {nhsNumber !== undefined && (
          <MetadataListItem label="NHS Number">{nhsNumber?.trim() ? formatNhsNumber(nhsNumber) : 'Not given'}</MetadataListItem>
        )}
        {showEmail && email && <MetadataListItem label="Email">{email}</MetadataListItem>}
        {additionalItems}
      </MetadataList>
    </HStack>
  );
}
