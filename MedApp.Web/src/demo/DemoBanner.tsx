import {Banner} from '@astryxdesign/core/Banner';
import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {useState} from 'react';
import {useCurrentUser} from '../auth/auth';
import {ConfirmDialog} from '../components/modals/ConfirmDialog';
import {AppIcon} from '../components/shared/icons';
import {ADMIN, STAFF} from './accounts';
import {resetDemo} from './session';

/**
 * The light demo's banner, at the top of the Read Me (the demo build only; the owner moved it there from above every
 * page): it says the data is synthetic and kept only in this browser, and who is signed in, and offers Reset. Switching
 * account is on the Read Me's Two accounts card and the tour's buttons.
 */
export function DemoBanner() {
  const me = useCurrentUser();
  const [isConfirmingReset, setIsConfirmingReset] = useState(false);
  const [isResetting, setIsResetting] = useState(false);
  const signedInAs = me.username === ADMIN.username ? `${ADMIN.fullName} (admin)` : `${STAFF.fullName} (staff)`;

  return (
    <>
      <Banner
        status="info"
        container="section"
        title="Light demo: synthetic data, kept only in this browser"
        description={`Don’t enter real patient details. You’re signed in as ${signedInAs}.`}
        endContent={
          <Button label="Reset" variant="ghost" size="sm" icon={<Icon icon={AppIcon.Refresh} />} onClick={() => setIsConfirmingReset(true)} />
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
