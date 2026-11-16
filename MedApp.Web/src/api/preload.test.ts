import {infiniteQueryOptions, QueryClient, queryOptions} from '@tanstack/react-query';
import {preload} from './preload';

const query = (key: string, data: () => Promise<unknown>) => queryOptions({queryKey: [key], queryFn: data});

describe('preload', () => {
  it('in the browser, starts fetching without waiting for it', async () => {
    const queryClient = new QueryClient();
    let finish!: (value: string) => void;
    const slow = query('slow', () => new Promise((resolve) => (finish = resolve)));

    await preload(queryClient, slow);

    expect(queryClient.getQueryState(['slow'])?.fetchStatus).toBe('fetching');
    finish('done');
    await vi.waitFor(() => expect(queryClient.getQueryData(['slow'])).toBe('done'));
  });

  it('rendering on the server, waits for every query, and carries on past one that fails', async () => {
    vi.stubGlobal('window', undefined);
    const queryClient = new QueryClient({defaultOptions: {queries: {retry: false}}});

    await preload(
      queryClient,
      query('a', async () => 'one'),
      query('b', async () => {
        throw new Error('down');
      }),
    );

    expect(queryClient.getQueryData(['a'])).toBe('one');
    expect(queryClient.getQueryState(['b'])?.status).toBe('error');
  });

  it('fetches the first page of a paged feed, on the server and in the browser', async () => {
    const feed = (key: string) =>
      infiniteQueryOptions({
        queryKey: [key],
        queryFn: async ({pageParam}) => `page after ${pageParam}`,
        initialPageParam: 'start',
        getNextPageParam: () => undefined,
      });
    const queryClient = new QueryClient();

    await preload(queryClient, feed('browser'));
    await vi.waitFor(() => expect(queryClient.getQueryData(['browser'])).toEqual({pages: ['page after start'], pageParams: ['start']}));

    vi.stubGlobal('window', undefined);
    await preload(queryClient, feed('server'));
    expect(queryClient.getQueryData(['server'])).toEqual({pages: ['page after start'], pageParams: ['start']});
  });
});
