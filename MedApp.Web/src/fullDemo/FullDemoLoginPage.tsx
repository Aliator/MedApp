import {Banner} from '@astryxdesign/core/Banner';
import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {List, ListItem} from '@astryxdesign/core/List';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Heading, Text} from '@astryxdesign/core/Text';
import {useLogin} from '../auth/auth';
import {StandalonePage} from '../components/pages/StandalonePage';
import {AppIcon} from '../components/shared/icons';
import {accountDescription, accountLabel, orderAccounts} from './accounts';
import type {DemoAccount} from './backend';
import {FullDemoProgress, useStartup} from './FullDemoProgress';
import {FullDemoResetButton} from './FullDemoResetButton';
import {DEMO_PASSWORD} from './session';

/**
 * The full demo's login page (its build only): the people in the demo database (`db-version.json`'s `accounts`), the
 * four demo accounts first, then everyone else at the centre, shown at once; under them the download of MedApp
 * itself and its database, with the sign-in buttons disabled until the backend has started. Choosing someone signs in
 * through the real API (`POST api/auth/login`, any password) and goes in.
 */
export function FullDemoLoginPage({redirectTo}: {redirectTo?: string}) {
  const login = useLogin(redirectTo);
  const state = useStartup();
  const isReady = state.phase === 'ready';
  const {demo, others} = orderAccounts(state.version?.accounts ?? []);

  const row = (account: DemoAccount, isMain: boolean) => (
    <ListItem
      key={account.username}
      label={`${account.fullName} (${account.username})`}
      description={accountDescription(account)}
      endContent={
        <Button
          label={`Sign in as ${accountLabel(account)}`}
          variant={isMain && account.roles.includes('Admin') ? 'primary' : 'secondary'}
          icon={<Icon icon={AppIcon.Login} />}
          isDisabled={!isReady || login.isPending}
          onClick={() => login.mutate({username: account.username, password: DEMO_PASSWORD})}
        />
      }
    />
  );

  return (
    <StandalonePage title="MedApp: full demo" maxWidth={640}>
      <VStack gap={4} hAlign="stretch">
        <Banner
          status="info"
          container="card"
          title="Synthetic data, kept only in this browser"
          description="The whole of MedApp, running in your browser on made-up patients. Don’t enter real patient details."
        />
        <FullDemoProgress />
        {login.isError && <Banner status="error" container="card" title="That didn’t work. Reload the page and try again." role="alert" />}
        <Text type="body">
          Choose who to be. Walkthrough, in the side nav, switches account for you where a step needs another; or sign out and choose again.
        </Text>
        {demo.length === 0 && state.phase !== 'failed' && <Text type="supporting">Reading who’s at the centre…</Text>}
        {demo.length > 0 && <List edge="flush">{demo.map((account) => row(account, true))}</List>}
        {others.length > 0 && (
          <VStack gap={2} hAlign="stretch">
            <Heading level={2}>Other people at the centre</Heading>
            <List edge="flush">{others.map((account) => row(account, false))}</List>
          </VStack>
        )}
        <VStack gap={2} hAlign="stretch">
          <Text type="supporting">Reset the Demo starts again from the published data, without your changes.</Text>
          <HStack>
            {/* Not while it starts: the start would save the database it's downloading after it was forgotten. */}
            <FullDemoResetButton path="/login" isDisabled={!isReady && state.phase !== 'failed'} />
          </HStack>
        </VStack>
      </VStack>
    </StandalonePage>
  );
}
