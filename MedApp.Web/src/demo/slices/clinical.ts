import type {BaseState} from '../state';

/** The light demo's state for letters, letter templates, the MDT list, consent forms and consent records. */
// eslint-disable-next-line @typescript-eslint/no-empty-object-type -- filled in by the slice's records
export interface ClinicalSlice {}

/** Seeds this slice, after the base seed (`state`: read it, or add to it, as the clinic's other records). */
export function seedClinical(_state: BaseState): ClinicalSlice {
  return {};
}
