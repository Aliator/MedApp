import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {Link} from '@astryxdesign/core/Link';
import {List, ListItem} from '@astryxdesign/core/List';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useNavigate} from '@tanstack/react-router';
import {useState} from 'react';
import {useAnnounce} from '../../app/announcements';
import {letterHref, letterKindWords, lettersAboutQuery} from '../../api/letters';
import {useCan} from '../../auth/auth';
import {formatDateTime} from '../../utils/dates';
import type {BannerPatient} from '../patients/PatientBanner';
import {AlertMessage} from '../shared/AlertMessage';
import {AppIcon} from '../shared/icons';
import {LetterStatusToken} from './LetterCells';
import {WriteLetterDialog, type LetterAbout} from './WriteLetterDialog';

export interface LettersAboutProps {
  about: LetterAbout;
  teamId: string | null | undefined;
  patient?: BannerPatient | null;
  /** Letters can't be started now (the record is restricted or anonymised, or the referral or pathway is closed). */
  isLocked?: boolean;
}

/**
 * A pathway's or a referral's letters (their pages' Letters part, decision 108): each one's kind, template, status,
 * author and when it last changed, linking to its page; and, for holders of WriteLetters while it isn't locked,
 * **Write Letter**, which opens `WriteLetterDialog` and then the new draft's page.
 */
export function LettersAbout({about, teamId, patient, isLocked = false}: LettersAboutProps) {
  const canWrite = useCan('WriteLetters') && !isLocked;
  const {data, isPending, isError} = useQuery(lettersAboutQuery(about));
  const [isWriting, setIsWriting] = useState(false);
  const navigate = useNavigate();
  const announce = useAnnounce();
  const letters = data?.items ?? [];
  const total = data?.totalCount ?? 0;

  return (
    <VStack gap={4} hAlign="stretch">
      {isError ? (
        <AlertMessage label="Letters not loaded" message="The letters could not be loaded. Please try again." />
      ) : isPending ? (
        <Text type="body" color="secondary">
          Loading letters...
        </Text>
      ) : letters.length === 0 ? (
        <Text type="body" color="secondary">
          No letters yet.
        </Text>
      ) : (
        <List edge="flush" density="compact" hasDividers aria-label="Letters">
          {letters.map((letter) => (
            <ListItem
              key={letter.id}
              label={
                <Text type="body" weight="semibold" maxLines={2}>
                  <Link href={letterHref(letter.id ?? '')}>{`${letterKindWords(letter.kind)}: ${letter.templateName}`}</Link>
                </Text>
              }
              description={
                <VStack gap={1}>
                  <HStack gap={2} vAlign="center" wrap="wrap">
                    <LetterStatusToken letter={letter} />
                  </HStack>
                  <Text type="supporting" color="secondary">
                    {`By ${letter.authorUsername}${letter.approverUsername ? `, approver ${letter.approverUsername}` : ''} · Updated ${formatDateTime(letter.lastUpdated ?? '')}`}
                  </Text>
                </VStack>
              }
            />
          ))}
        </List>
      )}
      {total > letters.length && (
        <Text type="supporting" color="secondary">
          {`Showing the latest ${letters.length} of ${total}.`}
        </Text>
      )}
      {canWrite && (
        <HStack>
          <Button label="Write Letter" variant="secondary" icon={<Icon icon={AppIcon.Letters} />} onClick={() => setIsWriting(true)} />
        </HStack>
      )}
      {isWriting && (
        <WriteLetterDialog
          about={about}
          teamId={teamId}
          patient={patient}
          onCancel={() => setIsWriting(false)}
          onCreated={(letter) => {
            announce('Letter started');
            setIsWriting(false);
            void navigate({to: '/letters/$id', params: {id: letter.id ?? ''}});
          }}
        />
      )}
    </VStack>
  );
}
