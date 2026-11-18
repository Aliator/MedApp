import {registerAccount} from './account';
import {registerCalendar} from './calendar';
import {registerDataRequests} from './dataRequests';
import {registerPathways} from './pathways';
import {registerPatients} from './patients';
import {registerReferrals} from './referrals';

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
  registerDataRequests();
}
