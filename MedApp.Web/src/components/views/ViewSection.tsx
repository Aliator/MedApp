import {MetadataList} from '@astryxdesign/core/MetadataList';
import {VStack} from '@astryxdesign/core/Stack';
import {Heading} from '@astryxdesign/core/Text';
import type {ReactNode} from 'react';

export interface ViewSectionProps {
  title?: string;
  /** Two columns of values (the default, as in Blazor), or one. */
  useGrid?: boolean;
  /** MetadataListItems: label and value (Blazor's ViewGroup). */
  children: ReactNode;
}

/** A titled group of read-only values in a ViewCard (replaces Blazor's ViewSection). */
export function ViewSection({title, useGrid = true, children}: ViewSectionProps) {
  return (
    <VStack gap={4}>
      {title && <Heading level={2}>{title}</Heading>}
      <MetadataList columns={useGrid ? 2 : 'single'} label={{position: 'top'}}>
        {children}
      </MetadataList>
    </VStack>
  );
}
