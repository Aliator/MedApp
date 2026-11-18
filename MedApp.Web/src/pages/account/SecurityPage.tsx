import {Banner} from '@astryxdesign/core/Banner';
import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {RadioList, RadioListItem} from '@astryxdesign/core/RadioList';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextInput} from '@astryxdesign/core/TextInput';
import {Token} from '@astryxdesign/core/Token';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {
  twoFactorStatusQuery,
  useDisableTwoFactor,
  useRegenerateRecoveryCodes,
  useSendTwoFactorEmailCode,
  type EmailCodesUnavailableReason,
  type TwoFactorMethod,
} from '../../api/account';
import {ApiValidationError, fieldStatus, otherMessages, refusalMessage} from '../../api/errors';
import {useAnnounce} from '../../app/announcements';
import {useCurrentUser} from '../../auth/auth';
import {EmailCodeNote} from '../../components/account/EmailCodeNote';
import {EmailCodesSetup} from '../../components/account/EmailCodesSetup';
import {RecoveryCodes} from '../../components/account/RecoveryCodes';
import {TwoFactorSetup} from '../../components/account/TwoFactorSetup';
import {OneTimeCodeField} from '../../components/forms/OneTimeCodeField';
import {ConfirmDialog} from '../../components/modals/ConfirmDialog';
import {Page} from '../../components/pages/Page';
import {AlertMessage} from '../../components/shared/AlertMessage';
import {AppIcon} from '../../components/shared/icons';
import {CardSection} from '../../components/views/CardSection';
import {ViewCard} from '../../components/views/ViewCard';

/** Why it can't be turned off, when the account must use it. */
export const REQUIRED = 'Admin accounts must use two-factor authentication here, so it can\'t be turned off.';

/** Why an admin using codes by email must switch, while admins are required to use two-factor. */
export const EMAIL_NOT_ENOUGH =
  'Admin accounts here must use an authenticator app. Codes by email are weaker: anyone who gets into your mailbox gets the code.';

/** Why codes by email can't be chosen yet. */
export const EMAIL_UNAVAILABLE: Record<EmailCodesUnavailableReason, string> = {
  NoConfirmedAddress: 'Confirm an email address first (Account Settings, Email Address).',
  EmailUnavailable: "This server can't send email.",
};

/** When no more codes can be emailed for a change for now. */
export const CHANGE_NO_MORE_CODES = 'No more codes can be sent for now. Try again in a few minutes, or use a recovery code.';

/** How each method is named. */
const METHOD_NAMES: Record<TwoFactorMethod, string> = {
  None: 'None',
  Authenticator: 'Authenticator app',
  Email: 'Codes by email',
};

/** Few enough recovery codes left to say so. */
const FEW_CODES = 3;

/**
 * How long "Done" ignores clicks once new recovery codes appear in its place, so the second click of a double-click on
 * "Get New Codes" can't close the dialog before they're seen.
 */
const CODES_DONE_HOLD_MS = 1000;

type Dialog = 'disable' | 'codes' | null;
type SetUp = 'Authenticator' | 'Email' | null;

/** The fields both dialogs ask for, whose 400 messages show on them. */
const PASSWORD_AND_CODE = ['password', 'code'];

/**
 * "Email me a code", for a change confirmed with an emailed code (turning two-factor off, new recovery codes), then
 * where it went, with Resend. A refusal (409, 429) shows in the API's words.
 */
function EmailMeACode() {
  const send = useSendTwoFactorEmailCode();
  const announce = useAnnounce();
  const sendCode = (again: boolean) =>
    send.mutate(undefined, {onSuccess: (sent) => again && announce(`A new code was emailed to ${sent.sentTo}.`)});

  return (
    <VStack gap={2} hAlign="stretch">
      {send.isError && <AlertMessage message={refusalMessage(send.error, 'A code could not be sent. Please try again.')} />}
      {send.data ? (
        <EmailCodeNote
          sent={send.data}
          onResend={() => sendCode(true)}
          isResending={send.isPending}
          exhaustedMessage={CHANGE_NO_MORE_CODES}
        />
      ) : (
        <Button
          label="Email me a code"
          variant="secondary"
          icon={<Icon icon={AppIcon.Email} />}
          isLoading={send.isPending}
          onClick={() => sendCode(false)}
        />
      )}
    </VStack>
  );
}

/**
 * The signed-in user's sign-in security (Account Settings, Security): two-factor authentication on or off and how,
 * setting it up with an authenticator app (`TwoFactorSetup`) or with codes by email (`EmailCodesSetup`, only with a
 * confirmed address, the reason shown otherwise), switching from codes by email to an app, turning it off (with the
 * password and a code; not while the account must use it), and getting a new set of recovery codes (with the password
 * and a code), with how many are left. For codes by email, the dialogs offer "Email me a code". Each asks for the
 * password, so a session alone can't change them. A 400 shows on its field; a 409, a locked account (423) or too many
 * codes (429) shows in the API's words.
 */
export function SecurityPage() {
  const me = useCurrentUser();
  const {data: status, isPending, isError} = useQuery(twoFactorStatusQuery);
  const disable = useDisableTwoFactor();
  const regenerate = useRegenerateRecoveryCodes();
  const announce = useAnnounce();
  const [settingUp, setSettingUp] = useState<SetUp>(null);
  const [choice, setChoice] = useState<'Authenticator' | 'Email'>('Authenticator');
  const [dialog, setDialog] = useState<Dialog>(null);
  const [password, setPassword] = useState('');
  const [code, setCode] = useState('');
  const [newCodes, setNewCodes] = useState<string[] | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const open = (next: Dialog) => {
    setPassword('');
    setCode('');
    setNewCodes(null);
    disable.reset();
    regenerate.reset();
    setNotice(null);
    setDialog(next);
  };
  const close = () => {
    setDialog(null);
    setPassword('');
    setCode('');
    // The codes go from the page's state and the mutation's, so they don't stay in memory.
    setNewCodes(null);
    regenerate.reset();
  };

  const startSetUp = (method: 'Authenticator' | 'Email') => {
    setNotice(null);
    setSettingUp(method);
  };

  const turnOff = () =>
    disable.mutate(
      {password, code},
      {
        onSuccess: () => {
          close();
          setNotice('Two-factor authentication is off. Your other sessions have ended.');
          announce('Two-factor authentication turned off');
        },
      },
    );

  const canGetNewCodes = password !== '' && code.trim() !== '';
  const getNewCodes = () => regenerate.mutate({password, code}, {onSuccess: setNewCodes});

  const disableErrors = disable.error instanceof ApiValidationError ? disable.error.errors : null;
  const disableOther = otherMessages(disableErrors, PASSWORD_AND_CODE);
  const disableProblem =
    disable.isError && !disableErrors ? refusalMessage(disable.error, 'Two-factor authentication could not be turned off. Please try again.') : null;
  const codesErrors = regenerate.error instanceof ApiValidationError ? regenerate.error.errors : null;
  const codesOther = otherMessages(codesErrors, PASSWORD_AND_CODE);
  const codesProblem =
    regenerate.isError && !codesErrors ? refusalMessage(regenerate.error, 'New recovery codes could not be made. Please try again.') : null;

  const enabled = status?.enabled === true;
  const required = status?.required === true;
  const method: TwoFactorMethod = status?.method ?? 'None';
  const usesEmail = enabled && method === 'Email';
  const codesLeft = status?.recoveryCodesLeft ?? 0;
  const emailReason = status?.emailCodesUnavailableReason;
  const canUseEmail = status?.canUseEmailCodes === true;
  const emailUnavailable = canUseEmail ? null : EMAIL_UNAVAILABLE[emailReason ?? 'NoConfirmedAddress'];

  const passwordField = (errors: typeof disableErrors) => (
    <>
      <input type="text" name="username" autoComplete="username" value={me.username} readOnly hidden />
      <TextInput
        label="Your password"
        type="password"
        value={password}
        onChange={setPassword}
        autoComplete="current-password"
        isRequired
        hasAutoFocus
        status={fieldStatus(errors, 'password')}
      />
    </>
  );

  const finishSetUp = () => setSettingUp(null);

  return (
    <Page
      title="Security"
      backHref="/account"
      backLabel="Back to account settings"
      isLoading={isPending}
      message={
        isError ? (
          <AlertMessage message="Your two-factor authentication settings could not be loaded. Please try again." />
        ) : (
          notice && <AlertMessage status="success" message={notice} />
        )
      }
      dialogs={
        <>
          <ConfirmDialog
            isOpen={dialog === 'disable'}
            title="Turn Off Two-Factor"
            message={
              usesEmail
                ? 'Signing in will need only your password. Enter your password and a code we email you (or a recovery code) to turn it off. Your other sessions end.'
                : 'Signing in will need only your password. Enter your password and a code from your authenticator app (or a recovery code) to turn it off. Your other sessions end.'
            }
            icon={AppIcon.TwoFactor}
            isDanger
            confirmText="Turn Off"
            confirmDisabled={!password || !code.trim()}
            isProcessing={disable.isPending}
            onConfirm={turnOff}
            onCancel={close}>
            {/* Only while it's open: a password box in the page makes browsers take it for a login. Beside it, the
                signed-in user's name, hidden, so a password manager pairs the password with that. */}
            {dialog === 'disable' && (
              <VStack gap={4} hAlign="stretch">
                {(disableProblem || disableOther.length > 0) && (
                  <AlertMessage message={disableProblem ?? disableOther.join(' ')} />
                )}
                {passwordField(disableErrors)}
                {usesEmail && <EmailMeACode />}
                <OneTimeCodeField
                  kind="either"
                  label={usesEmail ? 'Code from the email, or a recovery code' : undefined}
                  value={code}
                  onChange={setCode}
                  status={fieldStatus(disableErrors, 'code')}
                />
              </VStack>
            )}
          </ConfirmDialog>

          <ConfirmDialog
            isOpen={dialog === 'codes'}
            title="New Recovery Codes"
            message={
              newCodes
                ? undefined
                : usesEmail
                  ? 'Your current recovery codes stop working, and a new set is shown once. Enter your password and a code we email you to get them.'
                  : 'Your current recovery codes stop working, and a new set is shown once. Enter your password and a code from your authenticator app to get them.'
            }
            icon={AppIcon.Password}
            intent="warning"
            confirmText={newCodes ? 'Done' : 'Get New Codes'}
            confirmHoldMs={newCodes ? CODES_DONE_HOLD_MS : undefined}
            confirmDisabled={!newCodes && !canGetNewCodes}
            isProcessing={regenerate.isPending}
            onConfirm={newCodes ? close : getNewCodes}
            onCancel={close}>
            {newCodes ? (
              <RecoveryCodes codes={newCodes} username={me.username} announcement="New recovery codes made." />
            ) : (
              // Only while it's open, as Turn Off's, with the signed-in user's name for a password manager.
              dialog === 'codes' && (
                <VStack gap={4} hAlign="stretch">
                  {(codesProblem || codesOther.length > 0) && <AlertMessage message={codesProblem ?? codesOther.join(' ')} />}
                  {passwordField(codesErrors)}
                  {usesEmail && <EmailMeACode />}
                  <OneTimeCodeField
                    kind={usesEmail ? 'email' : 'authenticator'}
                    value={code}
                    onChange={setCode}
                    status={fieldStatus(codesErrors, 'code')}
                    onEnter={() => canGetNewCodes && !regenerate.isPending && getNewCodes()}
                  />
                </VStack>
              )
            )}
          </ConfirmDialog>
        </>
      }>
      <ViewCard
        showFooter={settingUp === null && !isError}
        footerActions={
          enabled ? (
            <>
              <Button
                label="New Recovery Codes"
                variant="secondary"
                icon={<Icon icon={AppIcon.Password} />}
                onClick={() => open('codes')}
              />
              {usesEmail && (
                <Button
                  label="Switch to an Authenticator App"
                  variant={required ? 'primary' : 'secondary'}
                  icon={<Icon icon={AppIcon.TwoFactor} />}
                  onClick={() => startSetUp('Authenticator')}
                />
              )}
              <Button
                label="Turn Off"
                variant="destructive"
                icon={<Icon icon={AppIcon.Cancel} />}
                isDisabled={required}
                tooltip={required ? REQUIRED : undefined}
                onClick={() => open('disable')}
              />
            </>
          ) : (
            <Button
              label="Set Up Two-Factor"
              variant="primary"
              icon={<Icon icon={AppIcon.TwoFactor} />}
              onClick={() => startSetUp(choice === 'Email' && canUseEmail ? 'Email' : 'Authenticator')}
            />
          )
        }>
        <CardSection title="Two-factor authentication">
          {settingUp === 'Authenticator' ? (
            <VStack gap={4} hAlign="stretch">
              {usesEmail && (
                <Text type="body">
                  Codes by email stay on until the app is set up: then signing in needs a code from the app instead.
                </Text>
              )}
              <TwoFactorSetup
                username={me.username}
                doneLabel="Done"
                onDone={finishSetUp}
                onCancel={finishSetUp}
                onEnabled={() => announce('Two-factor authentication turned on')}
              />
            </VStack>
          ) : settingUp === 'Email' ? (
            <EmailCodesSetup
              username={me.username}
              doneLabel="Done"
              onDone={finishSetUp}
              onCancel={finishSetUp}
              onEnabled={() => announce('Codes by email turned on')}
            />
          ) : (
            <VStack gap={4}>
              <Text type="body">
                As well as your password, signing in asks for a code, so a password alone isn&apos;t enough to get into
                your account.
              </Text>
              <MetadataList columns={2} label={{position: 'top'}}>
                <MetadataListItem label="Two-factor authentication">
                  <Token label={enabled ? 'On' : 'Off'} color={enabled ? 'green' : 'default'} size="sm" />
                </MetadataListItem>
                {enabled && <MetadataListItem label="Codes come from">{METHOD_NAMES[method]}</MetadataListItem>}
                {enabled && <MetadataListItem label="Recovery codes left">{codesLeft}</MetadataListItem>}
              </MetadataList>
              {!enabled && !isError && (
                <RadioList
                  label="How to get your codes"
                  value={choice}
                  onChange={(value) => setChoice(value === 'Email' ? 'Email' : 'Authenticator')}>
                  <RadioListItem
                    value="Authenticator"
                    label="Authenticator app"
                    description="The stronger choice: an app on your phone shows the codes. Admin accounts may be required to use it."
                  />
                  <RadioListItem
                    value="Email"
                    label="Codes by email"
                    description={
                      emailUnavailable ??
                      'MedApp emails you a code each time you sign in. Weaker: anyone who gets into your mailbox gets the code.'
                    }
                    isDisabled={!canUseEmail}
                  />
                </RadioList>
              )}
              {required && enabled && method === 'Email' && <Banner status="warning" container="card" title={EMAIL_NOT_ENOUGH} />}
              {required && enabled && method !== 'Email' && <Banner status="info" container="card" title={REQUIRED} />}
              {enabled && codesLeft <= FEW_CODES && (
                <Banner
                  status="warning"
                  container="card"
                  title={`You have ${codesLeft === 0 ? 'no' : `only ${codesLeft}`} recovery ${codesLeft === 1 ? 'code' : 'codes'} left.`}
                  description={
                    usesEmail
                      ? 'Get new recovery codes, so you can still sign in if you can\'t get into your email.'
                      : 'Get new recovery codes, so you can still sign in if you lose your authenticator app.'
                  }
                />
              )}
            </VStack>
          )}
        </CardSection>
      </ViewCard>
    </Page>
  );
}
