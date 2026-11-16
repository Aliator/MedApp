import {Banner} from '@astryxdesign/core/Banner';
import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {featuresQuery, myEmailQuery, useChangeMyEmail, useRemoveMyEmail} from '../../api/account';
import {ApiValidationError, fieldStatus, problemMessage} from '../../api/errors';
import {useAnnounce} from '../../app/announcements';
import {FormCard} from '../../components/forms/FormCard';
import {FormSection} from '../../components/forms/FormSection';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {Page} from '../../components/pages/Page';
import {AlertMessage, ValidationAlert} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {ViewCard} from '../../components/views/ViewCard';
import {CardSection} from '../../components/views/CardSection';

/** Said when the server can't send email, so an address can't be confirmed or changed here. */
export const EMAIL_UNAVAILABLE =
  "Email isn't available here: this server can't send email, so an address can't be added or confirmed.";

/**
 * The signed-in user's email address (Account Settings, Email Address): the confirmed one in use, one waiting for its
 * link to be opened, changing it (the API emails a link to the new address; the old one stays in use until it's
 * opened) and removing it. A confirmed address is where password reset links go. When the server can't send email,
 * it says so rather than offering a change it couldn't confirm; removing still works.
 */
export function EmailPage() {
  const {data, isPending, isError} = useQuery(myEmailQuery);
  const {data: features} = useQuery(featuresQuery);
  const change = useChangeMyEmail();
  const remove = useRemoveMyEmail();
  const announce = useAnnounce();
  const [newEmail, setNewEmail] = useState('');
  const [isRemoving, setIsRemoving] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);

  const canSendEmail = features?.canSendEmail === true;
  const email = data?.email ?? null;
  const pendingEmail = data?.pendingEmail ?? null;
  const validation = change.error instanceof ApiValidationError ? change.error.errors : null;

  const save = () => {
    setNotice(null);
    remove.reset();
    change.mutate(newEmail.trim(), {
      onSuccess: (saved) => {
        setNewEmail('');
        const message = `We've sent a link to ${saved.pendingEmail ?? newEmail.trim()}. Open it to confirm the address.`;
        setNotice(message);
        announce(message);
      },
    });
  };

  const confirmRemove = () =>
    remove.mutate(undefined, {
      onSuccess: () => {
        setIsRemoving(false);
        change.reset();
        setNotice('Your email address has been removed.');
        announce('Email address removed');
      },
      onError: () => setIsRemoving(false),
    });

  const message = isError ? (
    <AlertMessage message="Your email address could not be loaded. Please try again." />
  ) : remove.isError ? (
    <AlertMessage message="Your email address could not be removed. Please try again." />
  ) : change.isError ? (
    validation ? (
      <ValidationAlert errors={validation} fields={['email']} />
    ) : (
      <AlertMessage message={problemMessage(change.error, 'The link could not be sent. Please try again.')} />
    )
  ) : (
    notice && <AlertMessage status="success" message={notice} />
  );

  return (
    <Page
      title="Email Address"
      backHref="/account"
      backLabel="Back to account settings"
      isLoading={isPending}
      message={message}
      dialogs={
        <ConfirmDialog
          isOpen={isRemoving}
          title="Remove Email Address"
          message="Remove your email address? Password reset links can't reach you without one. You can add one again later."
          icon={AppIcon.Email}
          isDanger
          confirmText="Remove"
          isProcessing={remove.isPending}
          onConfirm={confirmRemove}
          onCancel={() => setIsRemoving(false)}
        />
      }>
      <VStack gap={6}>
        <ViewCard
          showFooter={email !== null || pendingEmail !== null}
          footerActions={
            <Button
              label="Remove Email Address"
              variant="destructive"
              icon={<Icon icon={AppIcon.Delete} />}
              onClick={() => setIsRemoving(true)}
            />
          }>
          <CardSection title="Your email address">
            <VStack gap={4}>
              <MetadataList columns="single" label={{position: 'top'}}>
                <MetadataListItem label="Email address">{email ?? 'None'}</MetadataListItem>
              </MetadataList>
              {pendingEmail && (
                <Banner
                  status="info"
                  container="card"
                  title={`Waiting for you to open the link we sent to ${pendingEmail}.`}
                  description={email ? `${email} stays in use until then.` : 'It will be used once you open the link.'}
                />
              )}
              <Text type="supporting" color="secondary">
                A confirmed email address is where password reset links go.
              </Text>
            </VStack>
          </CardSection>
        </ViewCard>

        {canSendEmail ? (
          <FormCard onSave={save} saveText="Send confirmation link" isProcessing={change.isPending}>
            <FormSection title={email || pendingEmail ? 'Change email address' : 'Add an email address'} useGrid={false}>
              <TextInput
                label="New email address"
                type="email"
                value={newEmail}
                onChange={setNewEmail}
                placeholder="name@example.org"
                autoComplete="email"
                description="We'll email a link to this address. It's used once you open the link."
                isRequired
                status={fieldStatus(validation, 'email')}
              />
            </FormSection>
          </FormCard>
        ) : (
          <ViewCard showFooter={false}>
            <CardSection title="Change email address">
              <Text type="body">{EMAIL_UNAVAILABLE}</Text>
            </CardSection>
          </ViewCard>
        )}
      </VStack>
    </Page>
  );
}
