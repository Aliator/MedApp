import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {myDetailsQuery, useSaveMyDetails, type StaffDetails} from '../../api/account';
import {ApiValidationError, fieldStatus, problemMessage} from '../../api/errors';
import {useAnnounce} from '../../app/announcements';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {CardSection} from '../../components/views/CardSection';
import {ViewCard} from '../../components/views/ViewCard';

const TITLE = 'Your details for letters';
const HINT = 'Used on letters you write or approve.';

/**
 * Account Settings' "Your details for letters" card: the signed-in user's full name and job title, as they go on
 * letters (`GET`/`PUT api/auth/me/details`; docs/centre-phases-3-5-plan.md, decision 80). Kept on the server, which
 * logs each change (names of the fields changed, never their values). Blank removes one; someone who manages users can
 * set them too, on the Users page.
 */
export function StaffDetailsCard() {
  const details = useQuery(myDetailsQuery);
  if (details.data) return <StaffDetailsForm saved={details.data} />;
  return (
    <ViewCard showFooter={false}>
      <CardSection title={TITLE}>
        {details.isError ? (
          <AlertMessage message="Your details for letters could not be loaded." />
        ) : (
          <Text type="body" color="secondary">
            Loading...
          </Text>
        )}
      </CardSection>
    </ViewCard>
  );
}

/** The form, once the details are here, so it starts from them. */
function StaffDetailsForm({saved}: {saved: StaffDetails}) {
  const save = useSaveMyDetails();
  const announce = useAnnounce();
  const [fullName, setFullName] = useState(saved.fullName ?? '');
  const [jobTitle, setJobTitle] = useState(saved.jobTitle ?? '');
  const errors = save.error instanceof ApiValidationError ? save.error.errors : null;
  const isChanged = fullName.trim() !== (saved.fullName ?? '') || jobTitle.trim() !== (saved.jobTitle ?? '');

  const submit = () =>
    save.mutate(
      {fullName, jobTitle},
      {
        onSuccess: (next) => {
          setFullName(next?.fullName ?? '');
          setJobTitle(next?.jobTitle ?? '');
          announce('Your details for letters saved');
        },
      },
    );

  return (
    <ViewCard
      footerActions={
        <Button
          label="Save Details"
          variant="secondary"
          icon={<Icon icon={AppIcon.Save} />}
          isDisabled={!isChanged}
          isLoading={save.isPending}
          onClick={submit}
        />
      }>
      <CardSection title={TITLE}>
        <VStack gap={3}>
          <Text type="body" color="secondary">
            {HINT}
          </Text>
          {save.isError && !errors && <AlertMessage message={problemMessage(save.error, 'Your details could not be saved. Please try again.')} />}
          <TextInput
            label="Full name"
            description='As it goes on letters, e.g. "Dr Sam Test".'
            value={fullName}
            onChange={setFullName}
            autoComplete="name"
            isOptional
            isDisabled={save.isPending}
            status={fieldStatus(errors, 'fullName')}
          />
          <TextInput
            label="Job title"
            description='e.g. "Consultant dermatologist".'
            value={jobTitle}
            onChange={setJobTitle}
            autoComplete="organization-title"
            isOptional
            isDisabled={save.isPending}
            status={fieldStatus(errors, 'jobTitle')}
          />
        </VStack>
      </CardSection>
    </ViewCard>
  );
}
