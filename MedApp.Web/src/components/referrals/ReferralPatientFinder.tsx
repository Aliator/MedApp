import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {Typeahead, type SearchableItem, type SearchSource} from '@astryxdesign/core/Typeahead';
import {useQueryClient} from '@tanstack/react-query';
import {useMemo, useState} from 'react';
import type {Patient} from '../../api/patients';
import {patientSearchQuery, type PatientSearch} from '../../api/search';
import {formatShortDate} from '../../utils/dates';
import {formatNhsNumber, normaliseNhsNumber} from '../../utils/nhsNumber';
import {AppIcon} from '../shared/icons';

type PatientItem = SearchableItem<Patient>;

/**
 * The searches for what's typed: digits (spaces ignored) are an NHS number, or part of one; one word matches first or
 * last names, two or more a first and a last name.
 */
export function patientSearchesFor(query: string): PatientSearch[] {
  const trimmed = query.trim();
  if (/^[\d\s]+$/.test(trimmed)) return [{NhsNumber: normaliseNhsNumber(trimmed)}];
  const words = trimmed.split(/\s+/).filter(Boolean);
  if (words.length === 0) return [];
  return words.length > 1 ? [{FirstName: words[0], LastName: words.slice(1).join(' ')}] : [{FirstName: words[0]}, {LastName: words[0]}];
}

/** Where Add Patient goes: the add patient page, which comes back here with the new patient chosen. */
export const ADD_PATIENT_FOR_REFERRAL = '/patients/add?then=referral';

export interface ReferralPatientFinderProps {
  /** Called with the patient chosen. */
  onChoose: (patientId: string) => void;
}

/**
 * Finding the patient a referral is for, by NHS number or name as it's typed, each listed with their date of birth and
 * NHS number to tell patients with the same name apart. Someone not in MedApp yet is added first (Add Patient, with
 * its demographics lookup), then comes back here chosen: a referral is always for a MedApp patient (decision 11).
 */
export function ReferralPatientFinder({onChoose}: ReferralPatientFinderProps) {
  const queryClient = useQueryClient();
  // Whether the last search found nobody, so Add Patient is offered first.
  const [foundNobody, setFoundNobody] = useState(false);
  const source = useMemo<SearchSource<PatientItem>>(
    () => ({
      bootstrap: () => [],
      async search(query) {
        const searches = patientSearchesFor(query);
        const pages = await Promise.all(searches.map((search) => queryClient.fetchQuery(patientSearchQuery({...search, PageSize: 10}))));
        const seen = new Set<string>();
        const found = pages
          .flatMap((page) => page.items ?? [])
          .filter((p) => !seen.has(p.id ?? '') && seen.add(p.id ?? ''))
          .map((p) => ({id: p.id ?? '', label: `${p.firstName} ${p.lastName}`, auxiliaryData: p}));
        setFoundNobody(searches.length > 0 && found.length === 0);
        return found;
      },
    }),
    [queryClient],
  );

  return (
    <VStack gap={3} hAlign="stretch">
      <Typeahead
        label="Patient"
        description="Their NHS number, or their name."
        isRequired
        placeholder="Type an NHS number or a name"
        searchSource={source}
        value={null}
        onChange={(item) => item && onChoose(item.id)}
        emptySearchText="No patients match"
        renderItem={(item) => (
          <VStack gap={0}>
            <Text type="body">{item.label}</Text>
            <Text type="supporting" color="secondary">
              {[
                item.auxiliaryData?.dateOfBirth ? `Born ${formatShortDate(item.auxiliaryData.dateOfBirth)}` : 'Date of birth not known',
                item.auxiliaryData?.nhsNumber ? `NHS No. ${formatNhsNumber(item.auxiliaryData.nhsNumber)}` : 'No NHS number',
              ].join(' · ')}
            </Text>
          </VStack>
        )}
      />
      <HStack gap={3} vAlign="center" wrap="wrap">
        <Text type="body" color="secondary">
          {foundNobody ? 'Nobody matches: add them to MedApp first.' : 'Not in MedApp yet?'}
        </Text>
        <Button
          label="Add Patient"
          variant={foundNobody ? 'primary' : 'secondary'}
          icon={<Icon icon={AppIcon.UserPlus} />}
          href={ADD_PATIENT_FOR_REFERRAL}
        />
      </HStack>
    </VStack>
  );
}
