import {useQuery} from '@tanstack/react-query';
import {useMemo} from 'react';
import {stageDefinitionsQuery} from '../../api/pathwaySetup';
import {teamsQuery} from '../../api/teams';
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

/** The pathways' own team (copied from the template when each was made): any of them. */
export const teamField = (teams: readonly Named[], current: string | undefined) =>
  idField('team', 'Team', teams, current, 'Selected team');

/**
 * The stage reached: pathways whose current stage comes from any of these stage definitions (e.g. "Stage reached: is
 * Histology (lab)"). The API leaves out completed, cancelled and archived pathways.
 */
export const stageReachedField = (definitions: readonly Named[], current: string | undefined) =>
  idField('stage', 'Stage reached', definitions, current, 'Selected stage');

/** Both fields, from the teams and stage definitions (archived ones too, as old pathways have them). */
export function useTeamAndStageFields(search: {team?: string; stage?: string}): ListFilterField[] {
  const teams = useQuery(teamsQuery(true)).data;
  const definitions = useQuery(stageDefinitionsQuery).data;
  return useMemo(
    () => [teamField(teams ?? [], search.team), stageReachedField(definitions ?? [], search.stage)],
    [teams, definitions, search.team, search.stage],
  );
}
