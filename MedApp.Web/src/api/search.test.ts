import {QueryClient} from '@tanstack/react-query';
import {json, mockFetch} from '../test/render';
import {ApiValidationError} from './errors';
import {PAGE_SIZE, patientSearchQuery, userSearchQuery} from './search';

const patientPage = {
  items: [{id: 'p1', firstName: 'Freya', lastName: 'Ahmed', dateOfBirth: '1976-12-01', email: 'freya.ahmed18@example.com'}],
  page: 2,
  pageSize: PAGE_SIZE,
  totalCount: 11,
  totalPages: 2,
};

const queryClient = () => new QueryClient({defaultOptions: {queries: {retry: false}}});

describe('patientSearchQuery', () => {
  it('asks the API for one page with the filters, and returns it', async () => {
    const fetch = mockFetch(() => json(patientPage));

    const result = await queryClient().fetchQuery(
      patientSearchQuery({Page: 2, Sort: 'dateOfBirth', Ascending: false, LastName: 'Ahmed', DateOfBirthFrom: '1970-01-01'}),
    );

    expect(result).toEqual(patientPage);
    const url = new URL((fetch.mock.calls[0][0] as Request).url);
    expect(url.pathname).toBe('/api/patients/search');
    expect(Object.fromEntries(url.searchParams)).toEqual({
      Page: '2',
      PageSize: String(PAGE_SIZE),
      Sort: 'dateOfBirth',
      Ascending: 'false',
      LastName: 'Ahmed',
      DateOfBirthFrom: '1970-01-01',
    });
  });

  it('leaves out empty filters, so equal searches share a cache entry', () => {
    const a = patientSearchQuery({Page: 1, FirstName: '', Email: undefined});
    const b = patientSearchQuery({Page: 1});

    expect(a.queryKey).toEqual(b.queryKey);
    expect(a.queryKey[0]).toBe('patients');
  });

  it('turns a 400 into a validation error', async () => {
    mockFetch(() => json({title: 'One or more validation errors occurred.', errors: {PageSize: ['Page size must be between 1 and 100.']}}, 400));

    await expect(queryClient().fetchQuery(patientSearchQuery({PageSize: 500}))).rejects.toBeInstanceOf(ApiValidationError);
  });
});

describe('userSearchQuery', () => {
  it('asks the API for one page of users with the filters', async () => {
    const fetch = mockFetch(() => json({items: [], page: 1, pageSize: PAGE_SIZE, totalCount: 0, totalPages: 0}));

    await queryClient().fetchQuery(userSearchQuery({Username: 'hart', Role: 'Admin'}));

    const url = new URL((fetch.mock.calls[0][0] as Request).url);
    expect(url.pathname).toBe('/api/auth/users/search');
    expect(Object.fromEntries(url.searchParams)).toEqual({PageSize: String(PAGE_SIZE), Username: 'hart', Role: 'Admin'});
  });

  it('is keyed under users', () => {
    expect(userSearchQuery({}).queryKey[0]).toBe('users');
  });
});
