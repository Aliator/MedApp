import {Banner} from '@astryxdesign/core/Banner';
import {Button} from '@astryxdesign/core/Button';
import {Card} from '@astryxdesign/core/Card';
import {Center} from '@astryxdesign/core/Center';
import {Icon} from '@astryxdesign/core/Icon';
import {VStack} from '@astryxdesign/core/Stack';
import {Heading, Text} from '@astryxdesign/core/Text';
import {TextInput} from '@astryxdesign/core/TextInput';
import {useState, type CSSProperties, type FormEvent} from 'react';
import {ApiValidationError, fieldStatus} from '../../api/errors';
import {useLogin} from '../../auth/auth';
import {AppIcon} from '../../components/shared/icons';
import {ForgotPasswordDialog} from './ForgotPasswordDialog';

// The login page has no AppShell, so it paints the body background itself (from Astryx's `login` template).
const pageStyle: CSSProperties = {minHeight: '100dvh', backgroundColor: 'var(--color-background-body)'};
// Stack has no maxWidth prop, so the column's cap is set here.
const columnStyle: CSSProperties = {width: '100%', maxWidth: 400};

/**
 * Sign in (replaces Blazor's Login page). Field errors from a 400 go on their fields; any other failure (a 401 for
 * a wrong username or password, or a server error) shows "Invalid credentials", as Blazor does.
 */
export function LoginPage() {
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [isForgotOpen, setIsForgotOpen] = useState(false);
  const login = useLogin();

  const validation = login.error instanceof ApiValidationError ? login.error.errors : null;
  const hasFieldErrors = validation !== null && Object.keys(validation.fields).length > 0;
  const showBanner = login.isError && !hasFieldErrors;

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    login.mutate({username, password});
  };

  return (
    <Center axis="both" padding={6} style={pageStyle}>
      <VStack gap={4} hAlign="center" style={columnStyle}>
        <VStack gap={2} hAlign="center">
          <Icon icon={AppIcon.Shield} size="lg" color="accent" />
          <Text type="body" weight="bold" size="lg">
            MedApp
          </Text>
        </VStack>

        <Card padding={8} width="100%">
          <form onSubmit={handleSubmit} noValidate>
            <VStack gap={4} hAlign="stretch">
              <Heading level={1}>Sign in</Heading>

              {showBanner && <Banner status="error" title="Invalid credentials" container="card" role="alert" />}

              <TextInput
                label="Username"
                value={username}
                onChange={setUsername}
                placeholder="Enter your username"
                autoComplete="username"
                isRequired
                status={fieldStatus(validation, 'username')}
                size="lg"
              />

              <TextInput
                label="Password"
                type="password"
                value={password}
                onChange={setPassword}
                placeholder="Enter your password"
                autoComplete="current-password"
                isRequired
                status={fieldStatus(validation, 'password')}
                size="lg"
              />

              <Button type="submit" label="Sign in" variant="primary" size="lg" isLoading={login.isPending} />
              <Button label="Forgot password?" variant="ghost" size="lg" onClick={() => setIsForgotOpen(true)} />
            </VStack>
          </form>
        </Card>
      </VStack>
      <ForgotPasswordDialog isOpen={isForgotOpen} onClose={() => setIsForgotOpen(false)} />
    </Center>
  );
}
