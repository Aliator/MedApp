import type {BaseState} from '../state';

/** The light demo's state for specimens, specimen types, labs and results (and the rest of referrals: handlers/referrals.ts). */
// eslint-disable-next-line @typescript-eslint/no-empty-object-type -- filled in by the slice's records
export interface LabSlice {}

/** Seeds this slice, after the base seed (`state`: read it, or add to it, as the clinic's other records). */
export function seedLab(_state: BaseState): LabSlice {
  return {};
}
