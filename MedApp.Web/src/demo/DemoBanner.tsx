import {Banner} from '@astryxdesign/core/Banner';
import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {HStack} from '@astryxdesign/core/Stack';
import {useNavigate, useRouterState} from '@tanstack/react-router';
import {useEffect, useState} from 'react';
import {useCurrentUser} from '../auth/auth';
import {ConfirmDialog} from '../components/modals/ConfirmDialog';
import {AppIcon} from '../components/shared/icons';
import {ADMIN, STAFF} from './accounts';
import {hasSeenReadMe, READ_ME_PATH} from './readMeSeen';
import {resetDemo, switchAccount} from './session';

/**
 * The light demo's banner, above every signed-in page (`AppLayout`'s `banner`, in the demo build only): it says the data
 * is synthetic and kept only in this browser, and offers switching account, the Read Me and Reset. On the first visit
 * it opens the Read Me.
 */
export function DemoBanner() {
  const me = useCurrentUser();
  const navigate = useNavigate();
  const pathname = useRouterState({select: (state) => state.location.pathname});
  // Switching account stays on this page, as the other account sees it.
  const here = useRouterState({select: (state) => `${state.location.pathname}${state.location.searchStr}`});
  const [isConfirmingReset, setIsConfirmingReset] = useState(false);
  const [isResetting, setIsResetting] = useState(false);
  const other = me.username === ADMIN.username ? STAFF : ADMIN;
  const signedInAs = me.username === ADMIN.username ? `${ADMIN.fullName} (admin)` : `${STAFF.fullName} (staff)`;

  useEffect(() => {
    if (!hasSeenReadMe() && pathname !== READ_ME_PATH) void navigate({to: READ_ME_PATH, replace: true} as Parameters<typeof navigate>[0]);
  }, [navigate, pathname]);

  return (
    <>
      <Banner
        status="info"
        container="section"
        title="Light demo: synthetic data, kept only in this browser"
        description={`Don’t enter real patient details. You’re signed in as ${signedInAs}.`}
        endContent={
          <HStack gap={2} wrap="wrap">
            <Button
              label={`Switch to ${other.fullName}`}
              variant="ghost"
              size="sm"
              icon={<Icon icon={AppIcon.Account} />}
              onClick={() => void switchAccount(other.username, here)}
            />
            <Button label="Read Me" variant="ghost" size="sm" icon={<Icon icon={AppIcon.Info} />} href={READ_ME_PATH} />
            <Button label="Reset" variant="ghost" size="sm" icon={<Icon icon={AppIcon.Refresh} />} onClick={() => setIsConfirmingReset(true)} />
          </HStack>
        }
      />
      <ConfirmDialog
        isOpen={isConfirmingReset}
        title="Reset the Demo"
        message="This puts every patient, pathway, referral and appointment back as they started, for both accounts, in this browser."
        icon={AppIcon.Refresh}
        confirmText="Reset"
        isProcessing={isResetting}
        onConfirm={() => {
          setIsResetting(true);
          void resetDemo();
        }}
        onCancel={() => setIsConfirmingReset(false)}
      />
    </>
  );
}
