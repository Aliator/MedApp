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

export interface UserTeamsDialogProps {
  isOpen: boolean;
  /** Whose teams: the row's user. */
  username: string;
  onClose: () => void;
  /** After a save, with what to tell the admin. */
  onSaved: (message: string) => void;
}

/**
 * A member of staff's teams (admins, the Users page): each team as a checkbox, theirs ticked, saved as
 * exactly the set ticked. Their teams and the list are read as it opens; the API's refusal (an unknown user, a
 * team archived meanwhile) shows in the dialog, which stays open.
 */
export function UserTeamsDialog({isOpen, username, onClose, onSaved}: UserTeamsDialogProps) {
  const theirs = useQuery({...userTeamsQuery(username), enabled: isOpen && username !== ''});
  const active = useQuery({...teamsQuery(false), enabled: isOpen});
  const save = useSetUserTeams();
  // What the admin has ticked; until they tick anything, the user's own.
  const [ticked, setTicked] = useState<string[] | null>(null);
  const [problem, setProblem] = useState<string | null>(null);

  const current = (theirs.data ?? []).map((s) => s.id ?? '');
  const selected = ticked ?? current;
  const choices = teamMembershipChoices(active.data ?? [], theirs.data ?? []);
  const isLoading = theirs.isPending || active.isPending;
  const isError = theirs.isError || active.isError;

  const close = () => {
    setTicked(null);
    setProblem(null);
    save.reset();
    onClose();
  };

  const confirm = () => {
    setProblem(null);
    save.mutate(
      {username, teamIds: selected},
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
  );

  return (
    <ConfirmDialog
      isOpen={isOpen}
      title="Edit Teams"
      message={`Tick the teams ${username} works in.`}
      icon={AppIcon.Teams}
      confirmText="Save Teams"
      confirmDisabled={isLoading || isError || sameIds(selected, current)}
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
