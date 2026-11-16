import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {Card} from '@astryxdesign/core/Card';
import {Section} from '@astryxdesign/core/Section';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {useRef, type FormEvent, type ReactNode} from 'react';
import {AppIcon} from '../shared/icons';
import {InCardContext} from '../views/CardSection';
import {useSaveShortcut} from './SaveShortcut';

export interface FormCardProps {
  /** The form's sections, usually FormSection. */
  children: ReactNode;
  /** Submits the form: the Save button, Enter in a field, or Ctrl+S (⌘S on a Mac). Omit for no Save button. */
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
  /**
   * Fill the height it's given (e.g. in a `Page` with `fill`, or in `TwoColumns`): the sections scroll inside the
   * card and the footer stays at the bottom, so a long form never scrolls the page.
   */
  fill?: boolean;
}

// The native form has no height prop; filling, it passes the card's height on to its stack.
const FILL = {height: '100%'};

/**
 * A form in a card (replaces Blazor's FormCard): sections, each with its title as a header bar across the card,
 * then a footer with Delete at the start and Cancel and Save at the end. It is a native <form>, so Enter submits
 * it, and Save is its submit button. Ctrl+S (⌘S on a Mac) submits it too (`useSaveShortcut`).
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
  fill = false,
}: FormCardProps) {
  const form = useRef<HTMLFormElement>(null);
  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (!isProcessing) onSave?.();
  };
  // Ctrl+S submits it, as Save does (so it waits while saving, like the button).
  useSaveShortcut(form, onSave && (() => form.current?.requestSubmit()));

  const sections = <InCardContext.Provider value>{children}</InCardContext.Provider>;

  return (
    <Card padding={0} width="100%" height={fill ? '100%' : undefined}>
      <form ref={form} onSubmit={handleSubmit} noValidate style={fill ? FILL : undefined}>
        {fill ? (
          <VStack gap={0} height="100%">
            <StackItem size="fill" isScrollable>
              {sections}
            </StackItem>
            {showFooter && footer()}
          </VStack>
        ) : (
          <>
            {sections}
            {showFooter && footer()}
          </>
        )}
      </form>
    </Card>
  );

  function footer() {
    return (
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
    );
  }
}
