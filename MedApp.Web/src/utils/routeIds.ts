/**
 * A route's `$id`, as the app's own links write it: lower case. IDs are GUIDs, which the API reads in any case, but the
 * query cache keys on the text, so `/procedures/327460EC-…` (typed, or pasted from elsewhere) would read the procedure
 * into a different cache entry from the one a save updates (by the ID the API returns, in lower case), and the page
 * would never show the change. Give a `$id` route `params: lowerCaseId`.
 */
export const lowerCaseId = {
  parse: ({id}: {id: string}) => ({id: id.toLowerCase()}),
  stringify: ({id}: {id: string}) => ({id}),
};
