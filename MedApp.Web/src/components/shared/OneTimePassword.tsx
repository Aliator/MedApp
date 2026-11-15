import {Button} from '@astryxdesign/core/Button';
import {useClipboard} from '@astryxdesign/core/hooks';
import {Icon} from '@astryxdesign/core/Icon';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useEffect, useRef} from 'react';
import {useAnnounce} from '../../app/announcements';
import {AppIcon} from './icons';

export interface OneTimePasswordProps {
  /** What the password is, e.g. "First password" or "New password". */
  label: string;
  /** The password the server generated. */
  password: string;
  /** Whose password it is, named in the note. */
  username: string;
  /**
   * What screen readers are told when the password appears, e.g. "Account created for x.". The component adds that
   * the password is shown once, with a copy button. Never the password itself, so it isn't read out in a shared room.
   */
  announcement: string;
}

/**
 * A password the server generated, shown to an admin the only time it can be (a new account's first password, or a
 * reset), with a button to copy it and a note to pass it on securely. The password works until its user changes it.
 * Use it inside the dialog that asked for the password: when it appears, it tells screen readers and takes the focus
 * (on its copy button), so keyboard and screen reader users land on it. ("One time" means shown once: the password
 * itself isn't single-use.)
 */
export function OneTimePassword({label, password, username, announcement}: OneTimePasswordProps) {
  const announce = useAnnounce();
  const {copy, isCopied} = useClipboard({announce: 'Copied'});
  const copyButton = useRef<HTMLButtonElement>(null);
  // Announced once per password, even when effects run twice (React's StrictMode) or the announcer changes.
  const announced = useRef<string | null>(null);

  useEffect(() => {
    copyButton.current?.focus();
  }, [password]);

  useEffect(() => {
    if (announced.current === password) return;
    announced.current = password;
    announce(`${announcement} Its password is shown once, with a Copy password button.`);
  }, [announce, announcement, password]);

  return (
    <VStack gap={2} hAlign="center">
      <Text type="supporting" color="secondary">
        {label}
      </Text>
      <Text type="code" size="lg">
        {password}
      </Text>
      <Button
        ref={copyButton}
        label={isCopied ? 'Copied' : 'Copy password'}
        size="sm"
        icon={<Icon icon={isCopied ? AppIcon.Check : AppIcon.Copy} />}
        onClick={() => void copy(password)}
      />
      <Text type="supporting" color="secondary" justify="center">
        {`It's shown only this once, and works until ${username} changes it. Copy it now and pass it on to them securely.`}
      </Text>
    </VStack>
  );
}
