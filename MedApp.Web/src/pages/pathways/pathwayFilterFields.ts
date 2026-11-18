import {useQuery} from '@tanstack/react-query';
import {useMemo} from 'react';
import {stageDefinitionsQuery} from '../../api/pathwaySetup';
import {specialtiesQuery} from '../../api/specialties';
import type {ListFilterField} from '../../components/tables/ListFilterBar';

type Named = {id?: string; name?: string | null; isArchived?: boolean};

/**
 * An ID choice field (see `parseIdChoice`): the options by name, archived ones marked, and any chosen ID not among
 * them (one not loaded yet, or gone) as `unknownLabel`, so its token still shows.
 */
function idField(key: string, label: string, items: readonly Named[], current: string | undefined, unknownLabel: string): ListFilterField {
  const options = [...items]
    .sort((a, b) => (a.name ?? '').localeCompare(b.name ?? ''))
    .map((item) => ({value: (item.id ?? '').toLowerCase(), label: item.isArchived ? `${item.name} (archived)` : (item.name ?? '')}));
  for (const id of current?.split(',') ?? []) {
    if (!options.some((o) => o.value === id)) options.push({value: id, label: unknownLabel});
  }
  return {kind: 'choice', key, label, options, isMultiple: true, canExclude: false};
}

/** The pathways' own specialty (copied from the template when each was made): any of them. */
export const specialtyField = (specialties: readonly Named[], current: string | undefined) =>
  idField('specialty', 'Specialty', specialties, current, 'Selected specialty');

/**
 * The stage reached: pathways whose current stage comes from any of these stage definitions (e.g. "Stage reached: is
 * Histology (lab)"). The API leaves out discharged, cancelled and archived pathways.
 */
export const stageReachedField = (definitions: readonly Named[], current: string | undefined) =>
  idField('stage', 'Stage reached', definitions, current, 'Selected stage');

/** Both fields, from the specialties and stage definitions (archived ones too, as old pathways have them). */
export function useSpecialtyAndStageFields(search: {specialty?: string; stage?: string}): ListFilterField[] {
  const specialties = useQuery(specialtiesQuery(true)).data;
  const definitions = useQuery(stageDefinitionsQuery).data;
  return useMemo(
    () => [specialtyField(specialties ?? [], search.specialty), stageReachedField(definitions ?? [], search.stage)],
    [specialties, definitions, search.specialty, search.stage],
  );
}
