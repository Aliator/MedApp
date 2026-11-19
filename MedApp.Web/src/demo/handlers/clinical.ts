import type {DemoPathway, DemoStage} from '../state';
import {canRecordConsentAt, registerConsent, whyConsentMissing} from './consent';
import {registerLetters, whyLetterMissing} from './letters';
import {registerMdt} from './mdt';

/** Letters, letter templates, the MDT list, consent forms and consent records (`letters.ts`, `mdt.ts`, `consent.ts`). */
export function registerClinical() {
  registerLetters();
  registerMdt();
  registerConsent();
}

/**
 * The letter and consent gates (`StageGates.WhyLetterMissing` and `WhyConsentMissing`), called by finishing a stage:
 * why `stage` can't be finished yet, or null. No override.
 */
export function clinicalGate(pathway: DemoPathway, stage: DemoStage): string | null {
  return whyLetterMissing(pathway.id, stage.needsLetterKind) ?? whyConsentMissing(stage, pathway.id);
}

/** Whether the signed-in person can record consent at this stage (the stage's `canRecordConsent`). */
export function canRecordConsent(pathway: DemoPathway, stage: DemoStage): boolean {
  return canRecordConsentAt(pathway, stage);
}
