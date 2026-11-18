import type {DemoPathway, DemoStage} from '../state';

/** Specimens, the Lab page, specimen types, labs and results. */
export function registerLab() {}

/**
 * The lab's stage gates (`StageGates.WhyCantFinish` and `WhyResultsAwaiting`), called by finishing a stage: why
 * `stage` can't be finished yet, or null. `isLast`: finishing it leaves no stage to do.
 */
export function labGate(_pathway: DemoPathway, _stage: DemoStage, _isLast: boolean): string | null {
  return null;
}

/** Whether the signed-in person can record a specimen at this stage (the stage's `canRecordSpecimen`). */
export function canRecordSpecimen(_pathway: DemoPathway, _stage: DemoStage): boolean {
  return false;
}
