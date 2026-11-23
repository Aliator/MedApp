import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {useState} from 'react';
import {ConfirmDialog} from '../components/modals/ConfirmDialog';
import {AppIcon} from '../components/shared/icons';
import {resetFullDemo} from './session';

/**
 * Reset the Demo, asked first: drops this browser's copy of the database, so the page reloads on the published one
 * (`resetFullDemo`), at `path`. The login page and Account Settings have it, as the walkthrough's Reset does.
 */
export function FullDemoResetButton({path = '/', isDisabled = false}: {path?: string; isDisabled?: boolean}) {
  const [isOpen, setIsOpen] = useState(false);
  const [isResetting, setIsResetting] = useState(false);
  return (
    <>
      <Button label="Reset the Demo" variant="secondary" icon={<Icon icon={AppIcon.Refresh} />} isDisabled={isDisabled} onClick={() => setIsOpen(true)} />
      <ConfirmDialog
        isOpen={isOpen}
        title="Reset the Demo?"
        message="Everything you’ve changed in this browser goes, and the demo starts again from the published data."
        isDanger
        confirmText="Reset the Demo"
        isProcessing={isResetting}
        onCancel={() => setIsOpen(false)}
        onConfirm={() => {
          setIsResetting(true);
          void resetFullDemo(path);
        }}
      />
    </>
  );
}
