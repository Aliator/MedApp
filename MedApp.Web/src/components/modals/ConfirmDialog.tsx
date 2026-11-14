import {Button} from '@astryxdesign/core/Button';
import {Icon, type IconType} from '@astryxdesign/core/Icon';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import type {ReactNode} from 'react';
import {AppIcon} from '../shared/icons';
import {DialogFrame} from './DialogFrame';

export type ConfirmIntent = 'accent' | 'warning' | 'danger';

export interface ConfirmDialogProps {
  isOpen: boolean;
  title: string;
  message?: ReactNode;
  /** The large icon above the message. Defaults to a warning icon for danger, otherwise a check. */
  icon?: IconType;
  /** Colours the icon. Defaults to 'danger' when isDanger, otherwise 'accent'. */
  intent?: ConfirmIntent;
  /** Makes the confirm button destructive. */
  isDanger?: boolean;
  /** Shows the confirm button as busy, and stops the dialog being dismissed. */
  isProcessing?: boolean;
  confirmDisabled?: boolean;
  confirmText?: string;
  cancelText?: string;
  onConfirm: () => void;
  onCancel: () => void;
  /** Extra content under the message, e.g. a PatientSummary or a field. */
  children?: ReactNode;
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
  cancelText = 'Cancel',
  onConfirm,
  onCancel,
  children,
}: ConfirmDialogProps) {
  const resolvedIntent = intent ?? (isDanger ? 'danger' : 'accent');
  const resolvedIcon = icon ?? (isDanger ? AppIcon.Warning : AppIcon.Check);

  return (
    <DialogFrame
      isOpen={isOpen}
      title={title}
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
            onClick={onConfirm}
          />
        </>
      }>
      <VStack gap={4} hAlign="stretch">
        <VStack gap={3} hAlign="center">
          <Icon icon={resolvedIcon} size="lg" color={intentColor[resolvedIntent]} />
          {message && (
            <Text type="body" color="secondary" justify="center">
              {message}
            </Text>
          )}
        </VStack>
        {children}
      </VStack>
    </DialogFrame>
  );
}
