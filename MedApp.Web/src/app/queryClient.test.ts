import {createQueryClient} from './queryClient';

describe('createQueryClient', () => {
  it('retries a failed query once, and does not refetch when the window gets focus', () => {
    const options = createQueryClient().getDefaultOptions().queries;

    expect(options).toMatchObject({retry: 1, refetchOnWindowFocus: false});
    expect(options?.staleTime).toBeUndefined();
  });

  it('takes data from a page rendered on the server as fresh for a while', () => {
    expect(createQueryClient({isSsr: true}).getDefaultOptions().queries?.staleTime).toBe(30_000);
  });
});
