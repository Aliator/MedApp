import {createRootRouteWithContext} from '@tanstack/react-router';
import {NotFoundPage} from '../pages/placeholder/NotFoundPage';
import type {RouterContext} from '../router';
import {RootComponent} from '../ssr/document';

export const Route = createRootRouteWithContext<RouterContext>()({
  component: RootComponent,
  notFoundComponent: NotFoundPage,
});
