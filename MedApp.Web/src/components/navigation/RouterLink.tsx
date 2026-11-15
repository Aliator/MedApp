import {useRouter} from '@tanstack/react-router';
import type {AnchorHTMLAttributes, MouseEvent, Ref} from 'react';

type RouterLinkProps = AnchorHTMLAttributes<HTMLAnchorElement> & {ref?: Ref<HTMLAnchorElement>};

const isExternal = (href: string) => /^([a-z][a-z0-9+.-]*:|\/\/|#)/i.test(href);

/**
 * The link component behind every Astryx link and `href` button (via LinkProvider). App paths navigate on the
 * client through TanStack Router, so the page isn't reloaded. Astryx hands over a plain `href` (query string
 * included), which `router.navigate({href})` takes as is; modified clicks (new tab etc.) are left to the browser.
 */
export function RouterLink({href, onClick, ...props}: RouterLinkProps) {
  const router = useRouter();

  const handleClick = (event: MouseEvent<HTMLAnchorElement>) => {
    onClick?.(event);
    if (!href || isExternal(href) || event.defaultPrevented || props.target) return;
    if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
    event.preventDefault();
    void router.navigate({href});
  };

  return <a href={href} onClick={handleClick} {...props} />;
}
