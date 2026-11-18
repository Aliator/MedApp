import {createRootRouteWithContext} from '@tanstack/react-router';
import {leaveOldAddress} from '../app/beforePathways';
import {NotFoundPage} from '../pages/placeholder/NotFoundPage';
import type {RouterContext} from '../router';
import {RootComponent} from '../ssr/document';

export const Route = createRootRouteWithContext<RouterContext>()({
  // An address from before a page was renamed goes on to the one it has now.
  beforeLoad: leaveOldAddress,
  component: RootComponent,
  notFoundComponent: NotFoundPage,
});
