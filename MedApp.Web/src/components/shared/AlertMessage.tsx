import {Banner} from '@astryxdesign/core/Banner';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {allMessages, type ValidationErrors} from '../../api/errors';

export interface AlertMessageProps {
  /** The headline, e.g. "Patient saved" or a validation summary. */
  message: string;
  status?: 'error' | 'warning' | 'success' | 'info';
  /** Individual messages listed under the headline. */
  details?: string[];
}

/**
 * A status banner with rounded corners (replaces Blazor's AlertMessage). A page's own messages go in its header
 * (`Page`'s `message`). Errors and warnings are announced to screen readers
 * as alerts; success and info messages are not.
 */
export function AlertMessage({message, status = 'error', details = []}: AlertMessageProps) {
  const isUrgent = status === 'error' || status === 'warning';

  return (
    <Banner
      status={status}
      title={message}
      container="card"
      role={isUrgent ? 'alert' : undefined}
      // As `description`, not children: Banner folds children behind an "Expand" toggle, and errors must stay visible.
      description={
        details.length > 0 ? (
          <VStack gap={1}>
            {details.map((detail, i) => (
              <Text key={`${i}-${detail}`} type="body">
                {detail}
              </Text>
            ))}
          </VStack>
        ) : undefined
      }
    />
  );
}

export interface ValidationAlertProps {
  errors: ValidationErrors;
  /**
   * The fields the form shows, whose messages show on the fields themselves (`fieldStatus`), so the banner leaves
   * them out and stays small. Without it, the banner lists every message.
   */
  fields?: readonly string[];
}

/** A 400's errors as a banner: the title, then each message that isn't already shown on one of `fields`. */
export function ValidationAlert({errors, fields}: ValidationAlertProps) {
  const details = fields
    ? [...Object.entries(errors.fields).flatMap(([field, messages]) => (fields.includes(field) ? [] : messages)), ...errors.general]
    : allMessages(errors);
  return <AlertMessage message={errors.title} details={details} />;
}
