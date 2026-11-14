import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {VStack} from '@astryxdesign/core/Stack';
import {Heading} from '@astryxdesign/core/Text';
import {AppIcon} from '../../components/shared/icons';

/** Stands in for a page that is still Blazor-only, so navigation works end to end. Each one is ported in phase 5. */
export function NotPortedPage({title}: {title: string}) {
  return (
    <VStack gap={6}>
      <Heading level={1}>{title}</Heading>
      <EmptyState
        title="Not ported yet"
        description="This page is still only in the Blazor app. It moves to React later in the migration."
        icon={<Icon icon={AppIcon.NotPorted} size="lg" />}
      />
    </VStack>
  );
}
