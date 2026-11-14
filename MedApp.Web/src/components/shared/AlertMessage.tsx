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
 * A status banner (replaces Blazor's AlertMessage). Errors and warnings are announced to screen readers
 * as alerts; success and info messages are not.
 */
export function AlertMessage({message, status = 'error', details = []}: AlertMessageProps) {
  const isUrgent = status === 'error' || status === 'warning';

  return (
    <Banner
      status={status}
      title={message}
      container="section"
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

/** A 400's errors as a banner: the title, then every field and general message. */
export function ValidationAlert({errors}: {errors: ValidationErrors}) {
  return <AlertMessage message={errors.title} details={allMessages(errors)} />;
}
