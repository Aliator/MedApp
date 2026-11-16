import {Button} from '@astryxdesign/core/Button';
import {Icon, type IconType} from '@astryxdesign/core/Icon';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useEffect, useRef, type ReactNode} from 'react';
import {useSaveShortcut} from '../forms/SaveShortcut';
import {AppIcon} from '../shared/icons';
import {DialogFrame} from './DialogFrame';

export type ConfirmIntent = 'accent' | 'warning' | 'danger';

export interface ConfirmDialogProps {
  isOpen: boolean;
  title: string;
  message?: ReactNode;
  /** The icon at the start of the header, before the title. Defaults to a warning icon for danger, otherwise a check. */
  icon?: IconType;
  /** Colours the icon. Defaults to 'danger' when isDanger, otherwise 'accent'. */
  intent?: ConfirmIntent;
  /** Makes the confirm button destructive. */
  isDanger?: boolean;
  /** Shows the confirm button as busy, and stops the dialog being dismissed. */
  isProcessing?: boolean;
  confirmDisabled?: boolean;
  confirmText?: string;
  /**
   * Ignores the confirm button for this long (ms) after its text last changed, e.g. "Create User" becoming "Done",
   * so the second click of a double-click can't press the new action before it has been seen.
   */
  confirmHoldMs?: number;
  cancelText?: string;
  onConfirm: () => void;
  onCancel: () => void;
  /** Extra content under the message, e.g. a PatientSummary or a field. */
  children?: ReactNode;
  /**
   * A dialog that is a form (e.g. Add Stage): Ctrl+S (⌘S on a Mac) in it confirms, as its button would. Off for
   * dialogs that only ask "are you sure?", which want a deliberate answer.
   */
  hasSaveShortcut?: boolean;
}

const intentColor = {accent: 'accent', warning: 'warning', danger: 'error'} as const;

/**
 * Asks the user to confirm an action (replaces Blazor's ConfirmModal, plan Q16). Built on Dialog rather than
 * AlertDialog so it can show its large icon and extra content.
 */
export function ConfirmDialog({
  isOpen,
  title,
  message,
  icon,
  intent,
  isDanger = false,
  isProcessing = false,
  confirmDisabled = false,
  confirmText = 'Confirm',
  confirmHoldMs,
  cancelText = 'Cancel',
  onConfirm,
  onCancel,
  children,
  hasSaveShortcut = false,
}: ConfirmDialogProps) {
  const resolvedIntent = intent ?? (isDanger ? 'danger' : 'accent');
  const resolvedIcon = icon ?? (isDanger ? AppIcon.Warning : AppIcon.Check);

  // When the confirm button's text last changed (not when it first showed), for confirmHoldMs.
  const textChange = useRef<{text: string; at: number}>({text: confirmText, at: Number.NEGATIVE_INFINITY});
  useEffect(() => {
    if (textChange.current.text !== confirmText) textChange.current = {text: confirmText, at: Date.now()};
  }, [confirmText]);
  const confirm = () => {
    if (confirmHoldMs && Date.now() - textChange.current.at < confirmHoldMs) return;
    onConfirm();
  };
  const content = useRef<HTMLDivElement>(null);
  // Joined while open, so Ctrl+S never brings up the browser's Save page dialog here, but it only confirms when
  // the button could be pressed.
  useSaveShortcut(
    content,
    hasSaveShortcut && isOpen
      ? () => {
          if (!isProcessing && !confirmDisabled) confirm();
        }
      : undefined,
  );

  return (
    <DialogFrame
      isOpen={isOpen}
      title={title}
      icon={<Icon icon={resolvedIcon} size="lg" color={intentColor[resolvedIntent]} />}
      onClose={onCancel}
      purpose={isProcessing ? 'required' : 'form'}
      footer={
        <>
          <Button label={cancelText} variant="secondary" isDisabled={isProcessing} onClick={onCancel} />
          <Button
            label={confirmText}
            variant={isDanger ? 'destructive' : 'primary'}
            isLoading={isProcessing}
            isDisabled={confirmDisabled}
            onClick={confirm}
          />
        </>
      }>
      <VStack ref={content} gap={4} hAlign="stretch">
        {message && (
          <Text type="body" color="secondary" justify="center">
            {message}
          </Text>
        )}
        {children}
      </VStack>
    </DialogFrame>
  );
}
