import type {DemoPathway, DemoStage} from '../state';

/** Letters, letter templates, the MDT list, consent forms and consent records. */
export function registerClinical() {}

/**
 * The letter and consent gates (`StageGates.WhyLetterMissing` and `WhyConsentMissing`), called by finishing a stage:
 * why `stage` can't be finished yet, or null.
 */
export function clinicalGate(_pathway: DemoPathway, _stage: DemoStage): string | null {
  return null;
}

/** Whether the signed-in person can record consent at this stage (the stage's `canRecordConsent`). */
export function canRecordConsent(_pathway: DemoPathway, _stage: DemoStage): boolean {
  return false;
}
