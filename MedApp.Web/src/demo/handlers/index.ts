import {registerAccount} from './account';
import {registerCalendar} from './calendar';
import {registerClinical} from './clinical';
import {registerLab} from './lab';
import {registerPathways} from './pathways';
import {registerPatients} from './patients';
import {registerReferrals} from './referrals';
import {registerSetup} from './setup';

let registered = false;

/** Registers every handler of the stand-in API, once. */
export function registerHandlers() {
  if (registered) return;
  registered = true;
  registerAccount();
  registerPatients();
  registerPathways();
  registerReferrals();
  registerCalendar();
  registerLab();
  registerClinical();
  registerSetup();
}
