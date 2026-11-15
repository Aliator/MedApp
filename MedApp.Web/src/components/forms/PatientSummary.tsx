import {Avatar} from '@astryxdesign/core/Avatar';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {HStack} from '@astryxdesign/core/Stack';
import type {ReactNode} from 'react';
import {formatDate} from '../../utils/dates';

export interface PatientSummaryProps {
  firstName?: string | null;
  lastName?: string | null;
  /** YYYY-MM-DD. */
  dateOfBirth?: string | null;
  email?: string | null;
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
        {showEmail && email && <MetadataListItem label="Email">{email}</MetadataListItem>}
        {additionalItems}
      </MetadataList>
    </HStack>
  );
}
