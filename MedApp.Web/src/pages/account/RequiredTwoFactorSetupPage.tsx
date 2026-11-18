import {Banner} from '@astryxdesign/core/Banner';
import {Button} from '@astryxdesign/core/Button';
import {Text} from '@astryxdesign/core/Text';
import {useQuery} from '@tanstack/react-query';
import {useState} from 'react';
import {twoFactorStatusQuery} from '../../api/account';
import {useCurrentUser, useEnterApp, useLogout} from '../../auth/auth';
import {TwoFactorSetup} from '../../components/account/TwoFactorSetup';
import {StandalonePage} from '../../components/pages/StandalonePage';

const TITLE = 'Set up two-factor authentication';

/** Why an admin using codes by email is here too. */
export const EMAIL_CODES_NOT_ENOUGH =
  "You sign in with codes by email, but admin accounts here must use an authenticator app: codes by email are weaker, as anyone who gets into your mailbox gets the code. Set up the app, and it replaces codes by email.";

/**
 * Where an admin is held while Admin Settings requires two-factor authentication for admins and they haven't set up an
 * authenticator app (`/set-up-two-factor`, outside the signed-in layout, like `/change-password`): the API refuses
 * everything else until they do. An admin who uses codes by email is held too, since the requirement needs the app,
 * and the page says why (their two-factor state, which the API still lets them read). `TwoFactorSetup` takes them
 * through it; once they've seen their recovery codes, Continue opens their start page. Or they can sign out.
 */
export function RequiredTwoFactorSetupPage() {
  const me = useCurrentUser();
  const enterApp = useEnterApp();
  const logout = useLogout();
  const {data: status} = useQuery(twoFactorStatusQuery);
  // Once it's on, signing out would leave them without their recovery codes: they carry on instead.
  const [isOn, setIsOn] = useState(false);
  const usesEmail = status?.enabled === true && status.method === 'Email';

  return (
    <StandalonePage title={TITLE} maxWidth={520}>
      <Text type="body">
        You&apos;re signed in as {me.username}. Admin accounts must use two-factor authentication here: as well as your
        password, you&apos;ll sign in with a code from an authenticator app on your phone. Set it up before you continue.
      </Text>
      {usesEmail && !isOn && <Banner status="warning" container="card" title={EMAIL_CODES_NOT_ENOUGH} />}
      <TwoFactorSetup
        username={me.username}
        switchingFromEmail={usesEmail}
        doneLabel="Continue"
        onDone={() => void enterApp()}
        onEnabled={() => setIsOn(true)}
      />
      {!isOn && <Button label="Sign out" variant="ghost" size="lg" onClick={() => logout.mutate()} isLoading={logout.isPending} />}
    </StandalonePage>
  );
}
