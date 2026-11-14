import {Button} from '@astryxdesign/core/Button';
import {Center} from '@astryxdesign/core/Center';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {AppIcon} from '../../components/shared/icons';

/** Any address that isn't a page. */
export function NotFoundPage() {
  return (
    <Center axis="both" padding={6} style={{minHeight: '100dvh', backgroundColor: 'var(--color-background-body)'}}>
      <EmptyState
        title="Not found"
        description="Sorry, there's nothing at this address."
        icon={<Icon icon={AppIcon.NotFound} size="lg" />}
        actions={<Button label="Go home" href="/" variant="secondary" />}
      />
    </Center>
  );
}
