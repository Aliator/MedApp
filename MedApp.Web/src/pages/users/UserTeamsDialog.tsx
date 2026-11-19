import {CheckboxList, CheckboxListItem} from '@astryxdesign/core/CheckboxList';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Spinner} from '@astryxdesign/core/Spinner';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {problemMessage} from '../../api/errors';
import {teamsQuery, userTeamsQuery, useSetUserTeams, type Team, type TeamMembership} from '../../api/teams';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';

interface Choice {
  value: string;
  label: string;
}

/**
 * What can be ticked: every active team, and any archived one the user already has (marked so: it can be kept, or
 * unticked to take it away, but not given to anyone new), by name.
 */
export function teamMembershipChoices(active: readonly Team[], theirs: readonly TeamMembership[]): Choice[] {
  const choices = active.filter((s) => !s.isArchived).map((s) => ({value: s.id ?? '', label: s.name ?? ''}));
  for (const s of theirs) {
    if (!choices.some((c) => c.value === s.id)) choices.push({value: s.id ?? '', label: `${s.name ?? ''} (archived)`});
  }
  return choices.sort((a, b) => a.label.localeCompare(b.label, undefined, {sensitivity: 'base'}));
}

/** The same IDs, in any order. */
const sameIds = (a: readonly string[], b: readonly string[]) => a.length === b.length && a.every((id) => b.includes(id));

/** The lab's teams (by ID): it has no clinic admin, so it isn't offered there. */
const labIds = (active: readonly Team[], theirs: readonly TeamMembership[]) =>
  [...active, ...theirs].filter((t) => t.kind === 'Lab').map((t) => t.id ?? '');

/**
 * What the dialog saves: each ticked team, with Clinic admin for those ticked in the second list (only clinics;
 * the lab's role is always Member).
 */
export function teamMembershipsToSave(selected: readonly string[], clinicAdminOf: readonly string[], lab: readonly string[]) {
  return selected.map((teamId) => ({
    teamId,
    role: clinicAdminOf.includes(teamId) && !lab.includes(teamId) ? ('ClinicAdmin' as const) : ('Member' as const),
  }));
}

export interface UserTeamsDialogProps {
  isOpen: boolean;
  /** Whose teams: the row's user. */
  username: string;
  onClose: () => void;
  /** After a save, with what to tell the admin. */
  onSaved: (message: string) => void;
}

/**
 * A member of staff's teams (admins, the Users page): each team as a checkbox, theirs ticked, and the clinics they're
 * clinic admin of, saved as exactly the set ticked. Their teams and the list are read as it opens; the API's refusal (an unknown user, a
 * team archived meanwhile) shows in the dialog, which stays open.
 */
export function UserTeamsDialog({isOpen, username, onClose, onSaved}: UserTeamsDialogProps) {
  const theirs = useQuery({...userTeamsQuery(username), enabled: isOpen && username !== ''});
  const active = useQuery({...teamsQuery(false), enabled: isOpen});
  const save = useSetUserTeams();
  // What the admin has ticked; until they tick anything, the user's own.
  const [ticked, setTicked] = useState<string[] | null>(null);
  const [adminTicked, setAdminTicked] = useState<string[] | null>(null);
  const [problem, setProblem] = useState<string | null>(null);

  const current = (theirs.data ?? []).map((s) => s.id ?? '');
  const currentAdmin = (theirs.data ?? []).filter((s) => s.role === 'ClinicAdmin').map((s) => s.id ?? '');
  const selected = ticked ?? current;
  const choices = teamMembershipChoices(active.data ?? [], theirs.data ?? []);
  const lab = labIds(active.data ?? [], theirs.data ?? []);
  // Clinic admin of: only teams ticked above, and never the lab.
  const adminChoices = choices.filter((c) => selected.includes(c.value) && !lab.includes(c.value));
  const clinicAdminOf = (adminTicked ?? currentAdmin).filter((id) => adminChoices.some((c) => c.value === id));
  const unchanged = sameIds(selected, current) && sameIds(clinicAdminOf, currentAdmin);
  const isLoading = theirs.isPending || active.isPending;
  const isError = theirs.isError || active.isError;

  const close = () => {
    setTicked(null);
    setAdminTicked(null);
    setProblem(null);
    save.reset();
    onClose();
  };

  const confirm = () => {
    setProblem(null);
    save.mutate(
      {username, teams: teamMembershipsToSave(selected, clinicAdminOf, lab)},
      {
        onSuccess: () => {
          close();
          onSaved(`${username}'s teams have been saved.`);
        },
        onError: (e) => setProblem(problemMessage(e, `The teams could not be saved for ${username}. Please try again.`)),
      },
    );
  };

  const body = isLoading ? (
    <HStack gap={2} vAlign="center">
      {/* Silent: its words beside it say it, and an announcing spinner leaves its words in a hidden status region. */}
      <Spinner size="sm" aria-hidden />
      <Text type="body">Loading teams...</Text>
    </HStack>
  ) : isError ? (
    <AlertMessage
      message={
        theirs.isError ? problemMessage(theirs.error, `${username}'s teams could not be loaded.`) : 'The teams could not be loaded.'
      }
    />
  ) : choices.length === 0 ? (
    <EmptyState title="There are no teams yet." description="Add them on the Teams page." isCompact />
  ) : (
    <VStack gap={3}>
      <CheckboxList
        label="Teams"
        isLabelHidden
        value={selected}
        onChange={setTicked}
        isDisabled={save.isPending}
        hasDividers>
        {choices.map((choice) => (
          <CheckboxListItem key={choice.value} value={choice.value} label={choice.label} />
        ))}
      </CheckboxList>
      {adminChoices.length > 0 && (
        <CheckboxList
          label="Clinic admin of"
          description="A clinic admin is staff who also looks after their clinic. The lab has none."
          value={clinicAdminOf}
          onChange={setAdminTicked}
          isDisabled={save.isPending}
          hasDividers>
          {adminChoices.map((choice) => (
            <CheckboxListItem key={choice.value} value={choice.value} label={`Clinic admin of ${choice.label}`} />
          ))}
        </CheckboxList>
      )}
    </VStack>
  );

  return (
    <ConfirmDialog
      isOpen={isOpen}
      title="Edit Teams"
      message={`Tick the teams ${username} works in, and any clinic they're the clinic admin of. An admin is in no team.`}
      icon={AppIcon.Teams}
      confirmText="Save Teams"
      confirmDisabled={isLoading || isError || unchanged}
      isProcessing={save.isPending}
      onConfirm={confirm}
      onCancel={close}>
      <VStack gap={2}>
        {problem && <AlertMessage message={problem} />}
        {body}
      </VStack>
    </ConfirmDialog>
  );
}
