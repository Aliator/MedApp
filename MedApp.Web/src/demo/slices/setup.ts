import type {BaseState} from '../state';

/** The light demo's state for setup and admin: practices, sites, rooms, hours, clinics, specialties, roles, users, monitoring, logs, jobs, privacy. */
// eslint-disable-next-line @typescript-eslint/no-empty-object-type -- filled in by the slice's records
export interface SetupSlice {}

/** Seeds this slice, after the base seed (`state`: read it, or add to it, as the clinic's other records). */
export function seedSetup(_state: BaseState): SetupSlice {
  return {};
}
