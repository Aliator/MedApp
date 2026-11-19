import {Button} from '@astryxdesign/core/Button';
import {EmptyState} from '@astryxdesign/core/EmptyState';
import {Icon} from '@astryxdesign/core/Icon';
import {Page} from '../../components/pages/Page';
import {AppIcon} from '../../components/shared/icons';
import {PERMISSION_LABELS, type Permission} from '../../auth/permissions';

/**
 * Where a page someone's roles don't allow sends them (`requirePermission`): what the page needs, in the words the
 * roles page uses, so they know what to ask an admin for, rather than a blank page or one full of errors.
 * `permission` is undefined when the address named none this app knows.
 */
export function NoPermissionPage({permission}: {permission?: Permission}) {
  return (
    <Page title="No permission">
      <EmptyState
        title="You don't have permission to use this page"
        description={`${permission ? `This page needs the ‘${PERMISSION_LABELS[permission]}’ permission. ` : ''}Ask an admin if you need it.`}
        icon={<Icon icon={AppIcon.Lock} size="lg" />}
        actions={<Button label="Go Home" href="/" variant="secondary" />}
      />
    </Page>
  );
}
