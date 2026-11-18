import {createFileRoute, notFound} from '@tanstack/react-router';
import {lazy, Suspense} from 'react';

/** The light demo's Read Me, in its build only (docs/agents/light-demo.md); elsewhere the page doesn't exist. */
const ReadMePage = __LIGHT_DEMO__ ? lazy(() => import('../../demo/ReadMePage').then((m) => ({default: m.ReadMePage}))) : null;

export const Route = createFileRoute('/_app/read-me')({
  beforeLoad: () => {
    if (!__LIGHT_DEMO__) throw notFound();
  },
  component: ReadMeRoute,
});

function ReadMeRoute() {
  return ReadMePage ? (
    <Suspense fallback={null}>
      <ReadMePage />
    </Suspense>
  ) : null;
}
