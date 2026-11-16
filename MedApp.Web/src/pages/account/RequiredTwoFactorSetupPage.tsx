import {Button} from '@astryxdesign/core/Button';
import {Text} from '@astryxdesign/core/Text';
import {useState} from 'react';
import {useCurrentUser, useEnterApp, useLogout} from '../../auth/auth';
import {TwoFactorSetup} from '../../components/account/TwoFactorSetup';
import {StandalonePage} from '../../components/pages/StandalonePage';

const TITLE = 'Set up two-factor authentication';

/**
 * Where an admin is held while Admin Settings requires two-factor authentication for admins and they haven't set it
 * up (`/set-up-two-factor`, outside the signed-in layout, like `/change-password`): the API refuses everything else
 * until they do. `TwoFactorSetup` takes them through it; once they've seen their recovery codes, Continue opens their
 * start page. Or they can sign out.
 */
export function RequiredTwoFactorSetupPage() {
  const me = useCurrentUser();
  const enterApp = useEnterApp();
  const logout = useLogout();
  // Once it's on, signing out would leave them without their recovery codes: they carry on instead.
  const [isOn, setIsOn] = useState(false);

  return (
    <StandalonePage title={TITLE} maxWidth={520}>
      <Text type="body">
        You&apos;re signed in as {me.username}. Admin accounts must use two-factor authentication here: as well as your
        password, you&apos;ll sign in with a code from an authenticator app on your phone. Set it up before you continue.
      </Text>
      <TwoFactorSetup username={me.username} doneLabel="Continue" onDone={() => void enterApp()} onEnabled={() => setIsOn(true)} />
      {!isOn && <Button label="Sign out" variant="ghost" size="lg" onClick={() => logout.mutate()} isLoading={logout.isPending} />}
    </StandalonePage>
  );
}
