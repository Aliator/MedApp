import {FormLayout} from '@astryxdesign/core/FormLayout';
import {Grid} from '@astryxdesign/core/Grid';
import {VStack} from '@astryxdesign/core/Stack';
import {Heading} from '@astryxdesign/core/Text';
import type {ReactNode} from 'react';

export interface FormSectionProps {
  title?: string;
  /** Two columns of fields (the default, as in Blazor), or one. */
  useGrid?: boolean;
  /** Astryx inputs (TextInput, DateInput, …) or Field-wrapped controls. */
  children: ReactNode;
}

/** A titled group of fields in a FormCard (replaces Blazor's FormSection). Fields mark required ones themselves. */
export function FormSection({title, useGrid = true, children}: FormSectionProps) {
  return (
    <VStack gap={4}>
      {title && <Heading level={2}>{title}</Heading>}
      <FormLayout defaultOptionality="optional">
        {useGrid ? (
          <Grid columns={2} gap={4}>
            {children}
          </Grid>
        ) : (
          children
        )}
      </FormLayout>
    </VStack>
  );
}
