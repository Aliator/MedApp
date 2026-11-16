import {Banner} from '@astryxdesign/core/Banner';
import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {TextInput} from '@astryxdesign/core/TextInput';
import {Token} from '@astryxdesign/core/Token';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {twoFactorStatusQuery, useDisableTwoFactor, useRegenerateRecoveryCodes} from '../../api/account';
import {ApiError, ApiValidationError, fieldStatus, soleFieldStatus, type ValidationErrors} from '../../api/errors';
import {useAnnounce} from '../../app/announcements';
import {useCurrentUser} from '../../auth/auth';
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

/** Few enough recovery codes left to say so. */
const FEW_CODES = 3;

/**
 * How long "Done" ignores clicks once new recovery codes appear in its place, so the second click of a double-click on
 * "Get New Codes" can't close the dialog before they're seen.
 */
const CODES_DONE_HOLD_MS = 1000;

type Dialog = 'disable' | 'codes' | null;

/** A refusal in the API's words (a 409 or a 423, which explain themselves), or `fallback`. */
function refusal(error: unknown, fallback: string) {
  return error instanceof ApiError && (error.status === 409 || error.status === 423) && error.title ? error.title : fallback;
}

/** A 400's messages that aren't on one of `fields`, for the dialog's banner. */
function otherMessages(errors: ValidationErrors | null, fields: readonly string[]) {
  if (!errors) return [];
  return [...Object.entries(errors.fields).flatMap(([field, messages]) => (fields.includes(field) ? [] : messages)), ...errors.general];
}

/**
 * The signed-in user's sign-in security (Account Settings, Security): two-factor authentication on or off, setting it
 * up with an authenticator app (`TwoFactorSetup`), turning it off (with the password and a code; not while the account
 * must use it), and getting a new set of recovery codes (with a code), with how many are left.
 */
export function SecurityPage() {
  const me = useCurrentUser();
  const {data: status, isPending, isError} = useQuery(twoFactorStatusQuery);
  const disable = useDisableTwoFactor();
  const regenerate = useRegenerateRecoveryCodes();
  const announce = useAnnounce();
  const [isSettingUp, setIsSettingUp] = useState(false);
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

  const getNewCodes = () => regenerate.mutate(code, {onSuccess: setNewCodes});

  const disableErrors = disable.error instanceof ApiValidationError ? disable.error.errors : null;
  const disableOther = otherMessages(disableErrors, ['password', 'code']);
  const disableProblem = disable.isError && !disableErrors ? refusal(disable.error, 'Two-factor authentication could not be turned off. Please try again.') : null;
  const codesErrors = regenerate.error instanceof ApiValidationError ? regenerate.error.errors : null;
  const codesProblem = regenerate.isError && !codesErrors ? refusal(regenerate.error, 'New recovery codes could not be made. Please try again.') : null;

  const enabled = status?.enabled === true;
  const required = status?.required === true;
  const codesLeft = status?.recoveryCodesLeft ?? 0;

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
            message="Signing in will need only your password. Enter your password and a code from your authenticator app (or a recovery code) to turn it off. Your other sessions end."
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
                <input type="text" name="username" autoComplete="username" value={me.username} readOnly hidden />
                <TextInput
                  label="Your password"
                  type="password"
                  value={password}
                  onChange={setPassword}
                  autoComplete="current-password"
                  isRequired
                  hasAutoFocus
                  status={fieldStatus(disableErrors, 'password')}
                />
                <OneTimeCodeField kind="either" value={code} onChange={setCode} status={fieldStatus(disableErrors, 'code')} />
              </VStack>
            )}
          </ConfirmDialog>

          <ConfirmDialog
            isOpen={dialog === 'codes'}
            title="New Recovery Codes"
            message={
              newCodes
                ? undefined
                : 'Your current recovery codes stop working, and a new set is shown once. Enter a code from your authenticator app to get them.'
            }
            icon={AppIcon.Password}
            intent="warning"
            confirmText={newCodes ? 'Done' : 'Get New Codes'}
            confirmHoldMs={newCodes ? CODES_DONE_HOLD_MS : undefined}
            confirmDisabled={!newCodes && !code.trim()}
            isProcessing={regenerate.isPending}
            onConfirm={newCodes ? close : getNewCodes}
            onCancel={close}>
            {newCodes ? (
              <RecoveryCodes codes={newCodes} username={me.username} announcement="New recovery codes made." />
            ) : (
              dialog === 'codes' && (
                <VStack gap={4} hAlign="stretch">
                  {codesProblem && <AlertMessage message={codesProblem} />}
                  <OneTimeCodeField
                    value={code}
                    onChange={setCode}
                    status={soleFieldStatus(codesErrors, 'code')}
                    hasAutoFocus
                    onEnter={() => code.trim() && getNewCodes()}
                  />
                </VStack>
              )
            )}
          </ConfirmDialog>
        </>
      }>
      <ViewCard
        showFooter={!isSettingUp && !isError}
        footerActions={
          enabled ? (
            <>
              <Button
                label="New Recovery Codes"
                variant="secondary"
                icon={<Icon icon={AppIcon.Password} />}
                onClick={() => open('codes')}
              />
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
              onClick={() => {
                setNotice(null);
                setIsSettingUp(true);
              }}
            />
          )
        }>
        <CardSection title="Two-factor authentication">
          {isSettingUp ? (
            <TwoFactorSetup
              username={me.username}
              doneLabel="Done"
              onDone={() => setIsSettingUp(false)}
              onCancel={() => setIsSettingUp(false)}
              onEnabled={() => announce('Two-factor authentication turned on')}
            />
          ) : (
            <VStack gap={4}>
              <Text type="body">
                As well as your password, signing in asks for a code from an authenticator app on your phone, so a
                password alone isn&apos;t enough to get into your account.
              </Text>
              <MetadataList columns={2} label={{position: 'top'}}>
                <MetadataListItem label="Two-factor authentication">
                  <Token label={enabled ? 'On' : 'Off'} color={enabled ? 'green' : 'default'} size="sm" />
                </MetadataListItem>
                {enabled && <MetadataListItem label="Recovery codes left">{codesLeft}</MetadataListItem>}
              </MetadataList>
              {required && enabled && <Banner status="info" container="card" title={REQUIRED} />}
              {enabled && codesLeft <= FEW_CODES && (
                <Banner
                  status="warning"
                  container="card"
                  title={`You have ${codesLeft === 0 ? 'no' : `only ${codesLeft}`} recovery ${codesLeft === 1 ? 'code' : 'codes'} left.`}
                  description="Get new recovery codes, so you can still sign in if you lose your authenticator app."
                />
              )}
            </VStack>
          )}
        </CardSection>
      </ViewCard>
    </Page>
  );
}
