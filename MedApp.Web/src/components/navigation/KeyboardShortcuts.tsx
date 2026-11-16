import {Kbd} from '@astryxdesign/core/Kbd';
import {MetadataList, MetadataListItem} from '@astryxdesign/core/MetadataList';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useRouter} from '@tanstack/react-router';
import {useEffect, useRef, useState} from 'react';
import {useAccessibility} from '../../app/accessibility';
import {useCurrentUser} from '../../auth/auth';
import {SAVE_KEYS} from '../forms/SaveShortcut';
import {DialogFrame} from '../modals/DialogFrame';

/** "Go to" shortcuts: G, then a letter. The admin ones only for admins. */
export const GO_TO = [
  {key: 'h', label: 'Home', href: '/'},
  {key: 't', label: 'My Tasks', href: '/tasks'},
  {key: 'p', label: 'Patients', href: '/patients'},
  {key: 'r', label: 'Procedures', href: '/procedures'},
  {key: 's', label: 'Account Settings', href: '/account'},
  {key: 'u', label: 'Users', href: '/admin/users', isAdmin: true},
  {key: 'a', label: 'Audit Log', href: '/admin/audit', isAdmin: true},
  {key: 'i', label: 'Insights', href: '/admin/insights', isAdmin: true},
] as const;

/** How long after G its letter can come. */
const GO_TO_WINDOW_MS = 1500;

/** Whether a key press is typing (in a field, or with a modifier), which the shortcuts leave alone. */
function isTyping(event: KeyboardEvent) {
  if (event.ctrlKey || event.metaKey || event.altKey) return true;
  const target = event.target as HTMLElement | null;
  if (!target) return false;
  return target.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(target.tagName) || target.closest('[role="dialog"], dialog[open]') !== null;
}

/**
 * The app's keyboard shortcuts (Account Settings, Accessibility; off until turned on, and so off for anyone they'd
 * get in the way of, as WCAG 2.1.4 asks of single-key shortcuts): ? lists them, / goes to the list's search, and G then a letter goes to a page. They
 * never fire while typing in a field or in a dialog.
 *
 * The list also shows Ctrl+S (⌘S on a Mac), which saves the form that's open. That one isn't here: it's
 * `SaveShortcutProvider`'s, always on, and works in fields, as a shortcut with a modifier can.
 */
export function KeyboardShortcuts() {
  const {flags} = useAccessibility();
  const me = useCurrentUser();
  const router = useRouter();
  const [isHelpOpen, setIsHelpOpen] = useState(false);
  const goPressedAt = useRef(0);

  const isOn = flags.shortcuts;
  const isAdmin = me.isAdmin;
  useEffect(() => {
    if (!isOn) return;
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.defaultPrevented || isTyping(event)) return;
      const key = event.key.toLowerCase();
      if (event.key === '?') {
        event.preventDefault();
        setIsHelpOpen(true);
      } else if (event.key === '/') {
        // The list's search: the first search box on the page (the left one, in a half view).
        const search = document.querySelector<HTMLElement>('main input[role="combobox"]');
        if (search) {
          event.preventDefault();
          search.focus();
        }
      } else if (key === 'g') {
        goPressedAt.current = Date.now();
      } else if (Date.now() - goPressedAt.current < GO_TO_WINDOW_MS) {
        goPressedAt.current = 0;
        const page = GO_TO.find((go) => go.key === key && (!('isAdmin' in go) || isAdmin));
        if (page) {
          event.preventDefault();
          void router.navigate({href: page.href});
        }
      }
    };
    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, [isOn, isAdmin, router]);

  return (
    <DialogFrame isOpen={isOn && isHelpOpen} title="Keyboard shortcuts" purpose="info" onClose={() => setIsHelpOpen(false)}>
      <VStack gap={4}>
        <MetadataList>
          <MetadataListItem label="Show these shortcuts">
            <Kbd keys="?" />
          </MetadataListItem>
          <MetadataListItem label="Search the list">
            <Kbd keys="/" />
          </MetadataListItem>
          <MetadataListItem label="Save the form">
            <Kbd keys={SAVE_KEYS} />
          </MetadataListItem>
          {GO_TO.filter((go) => !('isAdmin' in go) || isAdmin).map((go) => (
            <MetadataListItem key={go.key} label={`Go to ${go.label}`}>
              <Text type="body">
                <Kbd keys="g" /> then <Kbd keys={go.key} />
              </Text>
            </MetadataListItem>
          ))}
        </MetadataList>
        <Text type="supporting" color="secondary">
          The single keys don’t work while you’re typing in a field; save does, and works even with these shortcuts
          off. Turn them off again in Account Settings, Accessibility.
        </Text>
      </VStack>
    </DialogFrame>
  );
}
