import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {Card} from '@astryxdesign/core/Card';
import {Section} from '@astryxdesign/core/Section';
import {HStack, StackItem} from '@astryxdesign/core/Stack';
import type {FormEvent, ReactNode} from 'react';
import {AppIcon} from '../shared/icons';
import {InCardContext} from '../views/CardSection';

export interface FormCardProps {
  /** The form's sections, usually FormSection. */
  children: ReactNode;
  /** Submits the form: the Save button, or Enter in a field. Omit for no Save button. */
  onSave?: () => void;
  onCancel?: () => void;
  /** Shows a destructive Delete button at the start of the footer. */
  onDelete?: () => void;
  /** Disables the buttons and shows Save as busy. */
  isProcessing?: boolean;
  saveText?: string;
  cancelText?: string;
  deleteText?: string;
  /** Extra buttons in the footer, after Delete. */
  footerActions?: ReactNode;
  showFooter?: boolean;
}

/**
 * A form in a card (replaces Blazor's FormCard): sections, each with its title as a header bar across the card,
 * then a footer with Delete at the start and Cancel and Save at the end. It is a native <form>, so Enter submits
 * it, and Save is its submit button.
 */
export function FormCard({
  children,
  onSave,
  onCancel,
  onDelete,
  isProcessing = false,
  saveText = 'Save',
  cancelText = 'Cancel',
  deleteText = 'Delete',
  footerActions,
  showFooter = true,
}: FormCardProps) {
  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!isProcessing) onSave?.();
  };

  return (
    <Card padding={0} width="100%">
      <form onSubmit={handleSubmit} noValidate>
        <InCardContext.Provider value>{children}</InCardContext.Provider>
        {showFooter && (
          <Section variant="muted" padding={4}>
            <HStack gap={2} vAlign="center">
              {onDelete && (
                <Button
                  label={deleteText}
                  variant="destructive"
                  icon={<Icon icon={AppIcon.Delete} />}
                  isDisabled={isProcessing}
                  onClick={onDelete}
                />
              )}
              {footerActions}
              <StackItem size="fill" />
              {onCancel && (
                <Button
                  label={cancelText}
                  variant="secondary"
                  icon={<Icon icon={AppIcon.Cancel} />}
                  isDisabled={isProcessing}
                  onClick={onCancel}
                />
              )}
              {onSave && (
                <Button
                  type="submit"
                  label={saveText}
                  variant="primary"
                  icon={<Icon icon={AppIcon.Save} />}
                  isLoading={isProcessing}
                />
              )}
            </HStack>
          </Section>
        )}
      </form>
    </Card>
  );
}
