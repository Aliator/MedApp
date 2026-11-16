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
import {ApiValidationError, fieldStatus, otherMessages, refusalMessage} from '../../api/errors';
import {useAnnounce} from '../../app/announcements';
import {useCurrentUser} from '../../auth/auth';
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

/** The fields of the change form, whose 400 messages show on them. */
const CHANGE_FIELDS = ['email', 'password'];

/**
 * The signed-in user's email address (Account Settings, Email Address): the confirmed one in use, one waiting for its
 * link to be opened, changing it (the API emails a link to the new address; the old one stays in use until it's
 * opened) and removing it. A confirmed address is where password reset links go, so both ask for the password: a
 * session someone else got hold of can't redirect them. A 400 shows on its field; a locked account (423), too many
 * changes (429) or an email that couldn't be sent (409) shows in the API's words. When the server can't send email,
 * it says so rather than offering a change it couldn't confirm; removing still works.
 */
export function EmailPage() {
  const me = useCurrentUser();
  const {data, isPending, isError} = useQuery(myEmailQuery);
  const {data: features} = useQuery(featuresQuery);
  const change = useChangeMyEmail();
  const remove = useRemoveMyEmail();
  const announce = useAnnounce();
  const [newEmail, setNewEmail] = useState('');
  const [password, setPassword] = useState('');
  const [isRemoving, setIsRemoving] = useState(false);
  // The password the Remove dialog asks for, apart from the form's, so neither fills the other.
  const [removePassword, setRemovePassword] = useState('');
  const [notice, setNotice] = useState<string | null>(null);

  const canSendEmail = features?.canSendEmail === true;
  const email = data?.email ?? null;
  const pendingEmail = data?.pendingEmail ?? null;
  const validation = change.error instanceof ApiValidationError ? change.error.errors : null;
  const removeErrors = remove.error instanceof ApiValidationError ? remove.error.errors : null;
  const removeOther = otherMessages(removeErrors, ['password']);
  const removeProblem =
    remove.isError && !removeErrors ? refusalMessage(remove.error, 'Your email address could not be removed. Please try again.') : null;

  const save = () => {
    setNotice(null);
    remove.reset();
    change.mutate(
      {email: newEmail.trim(), password},
      {
        onSuccess: (saved) => {
          setNewEmail('');
          setPassword('');
          const message = `We've sent a link to ${saved.pendingEmail ?? newEmail.trim()}. Open it to confirm the address.`;
          setNotice(message);
          announce(message);
        },
      },
    );
  };

  const openRemove = () => {
    setRemovePassword('');
    remove.reset();
    setIsRemoving(true);
  };
  const closeRemove = () => {
    setIsRemoving(false);
    setRemovePassword('');
  };

  // A wrong password, a locked account or a failure keeps the dialog open, saying so in it.
  const confirmRemove = () =>
    remove.mutate(removePassword, {
      onSuccess: () => {
        closeRemove();
        change.reset();
        setNotice('Your email address has been removed.');
        announce('Email address removed');
      },
    });

  const message = isError ? (
    <AlertMessage message="Your email address could not be loaded. Please try again." />
  ) : change.isError ? (
    validation ? (
      <ValidationAlert errors={validation} fields={CHANGE_FIELDS} />
    ) : (
      <AlertMessage message={refusalMessage(change.error, 'The link could not be sent. Please try again.')} />
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
          message="Remove your email address? Password reset links can't reach you without one. You can add one again later. Enter your password to confirm."
          icon={AppIcon.Email}
          isDanger
          confirmText="Remove"
          confirmDisabled={!removePassword}
          isProcessing={remove.isPending}
          onConfirm={confirmRemove}
          onCancel={closeRemove}>
          {/* Only while it's open: closed dialogs stay in the page. Beside it, the signed-in user's name, hidden, so a
              password manager pairs the password with that. */}
          {isRemoving && (
            <VStack gap={4} hAlign="stretch">
              {(removeProblem || removeOther.length > 0) && <AlertMessage message={removeProblem ?? removeOther.join(' ')} />}
              <input type="text" name="username" autoComplete="username" value={me.username} readOnly hidden />
              <TextInput
                label="Your password"
                type="password"
                value={removePassword}
                onChange={setRemovePassword}
                autoComplete="current-password"
                isRequired
                hasAutoFocus
                status={fieldStatus(removeErrors, 'password')}
              />
            </VStack>
          )}
        </ConfirmDialog>
      }>
      <VStack gap={6}>
        <ViewCard
          showFooter={email !== null || pendingEmail !== null}
          footerActions={
            <Button
              label="Remove Email Address"
              variant="destructive"
              icon={<Icon icon={AppIcon.Delete} />}
              onClick={openRemove}
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
              {/* The signed-in user's name, hidden, so a password manager pairs the password with that, not with the
                  new address. */}
              <input type="text" name="username" autoComplete="username" value={me.username} readOnly hidden />
              <TextInput
                label="Your password"
                type="password"
                value={password}
                onChange={setPassword}
                autoComplete="current-password"
                description="We ask for this to check it's you, as password reset links will go to the new address."
                isRequired
                status={fieldStatus(validation, 'password')}
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
