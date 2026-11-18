import {Banner} from '@astryxdesign/core/Banner';
import {Button} from '@astryxdesign/core/Button';
import {Grid} from '@astryxdesign/core/Grid';
import {useClipboard} from '@astryxdesign/core/hooks';
import {Icon} from '@astryxdesign/core/Icon';
import {HStack, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useEffect, useRef} from 'react';
import {useAnnounce} from '../../app/announcements';
import {AppIcon} from '../shared/icons';

export interface RecoveryCodesProps {
  /** The codes the API returned, shown this once. */
  codes: readonly string[];
  /** Whose codes they are, named in the downloaded file. */
  username: string;
  /** What screen readers are told as they appear, e.g. "Two-factor authentication is on.". Never the codes. */
  announcement: string;
  /** How the account gets its codes, so the advice fits: an authenticator app (the default) or codes by email. */
  method?: 'authenticator' | 'email';
}

/** What a recovery code stands in for, for each method. */
const STANDS_IN_FOR = {
  authenticator: {
    banner:
      'If you lose your authenticator app, each code signs you in once instead of a code from it. Keep them somewhere safe, such as a password manager.',
    file: 'Each code signs you in once, instead of a code from your authenticator app. Keep them somewhere safe.',
  },
  email: {
    banner:
      "If you can't get MedApp's emails, each code signs you in once instead of an emailed code. Keep them somewhere safe, such as a password manager, not in the same mailbox.",
    file: 'Each code signs you in once, instead of a code MedApp emails you. Keep them somewhere safe, not in the same mailbox.',
  },
};

/** The downloaded file's name. */
export const RECOVERY_CODES_FILE = 'medapp-recovery-codes.txt';

/** The codes as a text file: who they're for, how they work, then one a line. */
export function recoveryCodesText(
  codes: readonly string[],
  username: string,
  method: 'authenticator' | 'email' = 'authenticator',
): string {
  return [
    `MedApp recovery codes for ${username}`,
    STANDS_IN_FOR[method].file,
    '',
    ...codes,
    '',
  ].join('\n');
}

/** Saves `text` as a file, through a link to it that's clicked and then forgotten. Only from an event handler. */
function download(text: string, fileName: string) {
  const url = URL.createObjectURL(new Blob([text], {type: 'text/plain;charset=utf-8'}));
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}

/**
 * Two-factor recovery codes, shown the only time they can be (two-factor just turned on, or a new set), with a warning
 * that they won't be shown again and buttons to copy them or download them as a text file. When they appear, they
 * tell screen readers and take the focus (on Copy), as `OneTimePassword` does. Used by `TwoFactorSetup` and the
 * Security page's new codes.
 */
export function RecoveryCodes({codes, username, announcement, method = 'authenticator'}: RecoveryCodesProps) {
  const announce = useAnnounce();
  const {copy, isCopied} = useClipboard({announce: 'Copied'});
  const copyButton = useRef<HTMLButtonElement>(null);
  const text = codes.join('\n');
  // Announced once per set, even when effects run twice (React's StrictMode) or the announcer changes.
  const announced = useRef<string | null>(null);

  useEffect(() => {
    copyButton.current?.focus();
  }, [text]);

  useEffect(() => {
    if (announced.current === text) return;
    announced.current = text;
    announce(`${announcement} Your ${codes.length} recovery codes are shown once, with Copy and Download buttons.`);
  }, [announce, announcement, codes.length, text]);

  return (
    <VStack gap={4} hAlign="stretch">
      <Banner
        status="warning"
        container="card"
        title="Save your recovery codes now. They won't be shown again."
        description={STANDS_IN_FOR[method].banner}
      />
      <Grid columns={2} gap={2} justify="center">
        {codes.map((code) => (
          <Text key={code} type="code" size="lg" justify="center">
            {code}
          </Text>
        ))}
      </Grid>
      <HStack gap={2} hAlign="center" wrap="wrap">
        <Button
          ref={copyButton}
          label={isCopied ? 'Copied' : 'Copy codes'}
          icon={<Icon icon={isCopied ? AppIcon.Check : AppIcon.Copy} />}
          onClick={() => void copy(text)}
        />
        <Button
          label="Download codes"
          icon={<Icon icon={AppIcon.Download} />}
          onClick={() => download(recoveryCodesText(codes, username, method), RECOVERY_CODES_FILE)}
        />
      </HStack>
    </VStack>
  );
}
