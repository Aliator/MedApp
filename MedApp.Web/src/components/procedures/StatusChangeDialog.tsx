import type {IconType} from '@astryxdesign/core/Icon';
import {TextArea} from '@astryxdesign/core/TextArea';
import {useState} from 'react';
import {ConfirmDialog} from '../modals/ConfirmDialog';
import {AppIcon} from '../shared/icons';
import {statusLabel} from './ProcedureCells';

export interface StatusAction {
  /** The button that offers the change. */
  label: string;
  variant: 'primary' | 'secondary' | 'destructive';
  title: string;
  message: string;
  icon: IconType;
  /** Blocking and cancelling need a reason, which the audit log keeps. */
  needsReason: boolean;
  isDanger?: boolean;
}

/** How each status change the API allows is offered: its button, and the confirmation it asks for. */
export function statusAction(to: string): StatusAction {
  switch (to) {
    case 'Blocked':
      return {
        label: 'Block',
        variant: 'secondary',
        title: 'Block Procedure',
        message: 'Work on its stages stops until it is unblocked. Say what it is waiting on.',
        icon: AppIcon.Warning,
        needsReason: true,
      };
    case 'Cancelled':
      return {
        label: 'Cancel Procedure',
        variant: 'destructive',
        title: 'Cancel Procedure',
        message: 'A cancelled procedure can only be archived after this. Say why it is being cancelled.',
        icon: AppIcon.Cancel,
        needsReason: true,
        isDanger: true,
      };
    case 'Completed':
      return {
        label: 'Mark Completed',
        variant: 'primary',
        title: 'Complete Procedure',
        message: 'Every stage is finished. Mark the procedure as completed?',
        icon: AppIcon.Success,
        needsReason: false,
      };
    case 'Archived':
      return {
        label: 'Archive',
        variant: 'secondary',
        title: 'Archive Procedure',
        message: 'Archived procedures are hidden from lists unless you search for them.',
        icon: AppIcon.Info,
        needsReason: false,
      };
    default:
      // Unblocking goes back to the status it was blocked from.
      return {
        label: 'Unblock',
        variant: 'primary',
        title: 'Unblock Procedure',
        message: `It goes back to ${statusLabel(to).toLowerCase()}, and work on its stages can carry on.`,
        icon: AppIcon.Refresh,
        needsReason: false,
      };
  }
}

export interface StatusChangeDialogProps {
  /** The status to change to; null when closed. */
  to: string | null;
  isProcessing: boolean;
  onConfirm: (reason: string | undefined) => void;
  onCancel: () => void;
}

/** Confirms a status change, asking for a reason when one is needed. Give it a `key` per change so the reason starts empty. */
export function StatusChangeDialog({to, isProcessing, onConfirm, onCancel}: StatusChangeDialogProps) {
  const [reason, setReason] = useState('');
  const action = statusAction(to ?? '');

  return (
    <ConfirmDialog
      isOpen={to !== null}
      title={action.title}
      message={action.message}
      icon={action.icon}
      isDanger={action.isDanger}
      confirmText={action.label}
      confirmDisabled={action.needsReason && !reason.trim()}
      isProcessing={isProcessing}
      onConfirm={() => onConfirm(action.needsReason ? reason.trim() : undefined)}
      onCancel={onCancel}>
      {action.needsReason && (
        <TextArea label="Reason" value={reason} onChange={setReason} rows={3} isRequired isDisabled={isProcessing} />
      )}
    </ConfirmDialog>
  );
}
