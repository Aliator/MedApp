import {Button} from '@astryxdesign/core/Button';
import {DropdownMenu} from '@astryxdesign/core/DropdownMenu';
import {Icon, type IconType} from '@astryxdesign/core/Icon';
import {AppIcon} from './icons';

export interface MenuAction {
  label: string;
  icon?: IconType;
  href?: string;
  onClick?: () => void;
  isDisabled?: boolean;
  isDestructive?: boolean;
}

/**
 * A row's actions as one "Actions" button opening a menu of them, for a narrow row (a phone) where more than two side
 * by side would crowd out its title (`RowCards`, `MdtItemList`). `name` is the button's accessible name, saying whose
 * actions they are where a list repeats them (the visible text stays "Actions").
 */
export function ActionsMenu({actions, name = 'Actions'}: {actions: MenuAction[]; name?: string}) {
  return (
    <DropdownMenu
      items={actions.map((action) => ({
        id: action.label,
        label: action.label,
        icon: action.icon,
        href: action.href,
        onClick: action.onClick,
        isDisabled: action.isDisabled,
        variant: action.isDestructive ? 'destructive' : undefined,
      }))}
      alignment="start"
      renderTrigger={(trigger) => (
        <Button {...trigger} label={name} variant="secondary" size="sm" icon={<Icon icon={AppIcon.Menu} />}>
          Actions
        </Button>
      )}
    />
  );
}
