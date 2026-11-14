import {createRootRouteWithContext, Outlet} from '@tanstack/react-router';
import {NotFoundPage} from '../pages/placeholder/NotFoundPage';
import type {RouterContext} from '../router';

export const Route = createRootRouteWithContext<RouterContext>()({
  component: Outlet,
  notFoundComponent: NotFoundPage,
});
