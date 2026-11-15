import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';

export interface OneTimePasswordProps {
  /** What the password is, e.g. "First password" or "New password". */
  label: string;
  /** The password the server generated. */
  password: string;
  /** Whose password it is, named in the note. */
  username: string;
}

/**
 * A password the server generated, shown to an admin the one time it can be (a new account's first password, or a
 * reset), with a note to pass it on securely. Use it inside the dialog that asked for the password.
 */
export function OneTimePassword({label, password, username}: OneTimePasswordProps) {
  return (
    <VStack gap={2} hAlign="center">
      <Text type="supporting" color="secondary">
        {label}
      </Text>
      <Text type="code" size="lg">
        {password}
      </Text>
      <Text type="supporting" color="secondary" justify="center">
        {`This password is shown only once. Save it now and pass it on to ${username} securely.`}
      </Text>
    </VStack>
  );
}
