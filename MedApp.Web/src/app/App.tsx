import {RouterProvider} from '@tanstack/react-router';
import {useState} from 'react';
import {createAppRouter} from '../router';
import {AppProviders} from './AppProviders';
import {createQueryClient} from './queryClient';

export function App() {
  const [queryClient] = useState(() => createQueryClient());
  const [router] = useState(() => createAppRouter(queryClient));

  return (
    <AppProviders queryClient={queryClient}>
      <RouterProvider router={router} />
    </AppProviders>
  );
}
