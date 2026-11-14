import {QueryClient} from '@tanstack/react-query';
import {RouterProvider} from '@tanstack/react-router';
import {useState} from 'react';
import {createAppRouter} from '../router';
import {AppProviders} from './AppProviders';

export function App() {
  const [queryClient] = useState(
    () => new QueryClient({defaultOptions: {queries: {retry: 1, refetchOnWindowFocus: false}}}),
  );
  const [router] = useState(() => createAppRouter(queryClient));

  return (
    <AppProviders queryClient={queryClient}>
      <RouterProvider router={router} />
    </AppProviders>
  );
}
