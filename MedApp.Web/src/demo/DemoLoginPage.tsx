import {Banner} from '@astryxdesign/core/Banner';
import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {List, ListItem} from '@astryxdesign/core/List';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useLogin} from '../auth/auth';
import {StandalonePage} from '../components/pages/StandalonePage';
import {AppIcon} from '../components/shared/icons';
import {useState} from 'react';
import {accountDescription, accountLabel, DEMO_ACCOUNTS} from './accounts';
import {refreshApp} from './refreshApp';

/**
 * The light demo's login page (the demo build only): no password, just a choice of the four demo accounts. Choosing one
 * signs in through the stand-in API, as the real page would, and goes in.
 */
export function DemoLoginPage() {
  const login = useLogin();
  const [isRefreshing, setIsRefreshing] = useState(false);
  return (
    <StandalonePage title="MedApp: light demo" maxWidth={480}>
      <VStack gap={4} hAlign="stretch">
        <Banner
          status="info"
          container="card"
          title="Synthetic data, kept only in this browser"
          description="A light version of MedApp that runs in your browser, with made-up patients. Don’t enter real patient details."
        />
        {login.isError && <Banner status="error" container="card" title="That didn’t work. Reload the page and try again." role="alert" />}
        <Text type="body">Choose who to be. Walkthrough, in the side nav, switches account for you where a step needs another; or sign out and choose again.</Text>
        <List edge="flush">
          {DEMO_ACCOUNTS.map((account) => (
            <ListItem
              key={account.id}
              label={`${account.fullName} (${account.username})`}
              description={accountDescription(account)}
              endContent={
                <Button
                  label={`Sign in as ${accountLabel(account)}`}
                  variant={account.roles.includes('Admin') ? 'primary' : 'secondary'}
                  icon={<Icon icon={AppIcon.Login} />}
                  isDisabled={login.isPending}
                  onClick={() => login.mutate({username: account.username, password: 'light-demo'})}
                />
              }
            />
          ))}
        </List>
        <VStack gap={2} hAlign="start">
          <Button
            label="Refresh App"
            variant="secondary"
            icon={<Icon icon={AppIcon.Refresh} />}
            isLoading={isRefreshing}
            onClick={() => {
              setIsRefreshing(true);
              void refreshApp();
            }}
          />
          <Text type="supporting" color="secondary">
            Starts the demo again from scratch: clears what this browser kept (your changes, settings and the walkthrough&apos;s place) and loads the latest version.
          </Text>
        </VStack>
      </VStack>
    </StandalonePage>
  );
}
