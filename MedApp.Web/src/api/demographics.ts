import {queryOptions, useMutation} from '@tanstack/react-query';
import {normaliseNhsNumber} from '../utils/nhsNumber';
import {api, unwrap, type Schemas} from './client';

export type DemographicsStatus = Schemas['DemographicsStatusResponse'];
export type DemographicsResult = Schemas['DemographicsLookupResponse'];

/**
 * Whether NHS number look-ups are set up (`GET api/demographics/status`): the national demographics service (PDS),
 * or a stand-in for it. It changes only with the API's settings, so it's fetched once a session.
 */
export const demographicsStatusQuery = () =>
  queryOptions({
    queryKey: ['demographics', 'status'],
    queryFn: () => unwrap(api.GET('/api/demographics/status')),
    staleTime: Infinity,
  });

/**
 * Looks a person up by NHS number (`POST api/demographics/lookup`, the number in the body so it's never in a URL
 * or a log of URLs): their name and date of birth. A mutation
 * rather than a query, because each look-up is something someone chose to do, and the API records it in the
 * activity log: it's never cached, refetched or retried. A 404 means nobody has that number (or look-ups aren't set
 * up); a 400, that the number isn't valid.
 */
export function useLookUpDemographics() {
  return useMutation({
    mutationFn: (nhsNumber: string) =>
      unwrap(api.POST('/api/demographics/lookup', {body: {nhsNumber: normaliseNhsNumber(nhsNumber)}})),
  });
}
