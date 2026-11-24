import {FormLayout} from '@astryxdesign/core/FormLayout';
import {Grid} from '@astryxdesign/core/Grid';
import type {ReactNode} from 'react';
import {useIsNarrow} from '../pages/Page';
import {CardSection} from '../views/CardSection';

/** In a narrow layout, two columns only where both fit at this width (px), else one: a phone's form is one column. */
const NARROW_COLUMNS = {minWidth: 240, max: 2};

export interface FormSectionProps {
  title?: string;
  /** Two columns of fields (the default, as in Blazor; one in a narrow layout where two don't fit), or one. */
  useGrid?: boolean;
  /** Astryx inputs (TextInput, DateInput, …) or Field-wrapped controls. */
  children: ReactNode;
}

/**
 * A titled group of fields in a FormCard (replaces Blazor's FormSection); in a card, its title is a header bar.
 * Fields mark required ones themselves.
 */
export function FormSection({title, useGrid = true, children}: FormSectionProps) {
  const isNarrow = useIsNarrow();
  return (
    <CardSection title={title}>
      <FormLayout defaultOptionality="optional">
        {useGrid ? (
          <Grid columns={isNarrow ? NARROW_COLUMNS : 2} gap={4}>
            {children}
          </Grid>
        ) : (
          children
        )}
      </FormLayout>
    </CardSection>
  );
}
