import {MetadataList} from '@astryxdesign/core/MetadataList';
import type {ReactNode} from 'react';
import {CardSection} from './CardSection';

export interface ViewSectionProps {
  title?: string;
  /** Two columns of values (the default, as in Blazor), or one. */
  useGrid?: boolean;
  /** MetadataListItems: label and value (Blazor's ViewGroup). */
  children: ReactNode;
}

/** A titled group of read-only values in a ViewCard (replaces Blazor's ViewSection); in a card, its title is a header bar. */
export function ViewSection({title, useGrid = true, children}: ViewSectionProps) {
  return (
    <CardSection title={title}>
      <MetadataList columns={useGrid ? 2 : 'single'} label={{position: 'top'}}>
        {children}
      </MetadataList>
    </CardSection>
  );
}
