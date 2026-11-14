import {FormLayout} from '@astryxdesign/core/FormLayout';
import {Grid} from '@astryxdesign/core/Grid';
import type {ReactNode} from 'react';
import {CardSection} from '../views/CardSection';

export interface FormSectionProps {
  title?: string;
  /** Two columns of fields (the default, as in Blazor), or one. */
  useGrid?: boolean;
  /** Astryx inputs (TextInput, DateInput, …) or Field-wrapped controls. */
  children: ReactNode;
}

/**
 * A titled group of fields in a FormCard (replaces Blazor's FormSection); in a card, its title is a header bar.
 * Fields mark required ones themselves.
 */
export function FormSection({title, useGrid = true, children}: FormSectionProps) {
  return (
    <CardSection title={title}>
      <FormLayout defaultOptionality="optional">
        {useGrid ? (
          <Grid columns={2} gap={4}>
            {children}
          </Grid>
        ) : (
          children
        )}
      </FormLayout>
    </CardSection>
  );
}
