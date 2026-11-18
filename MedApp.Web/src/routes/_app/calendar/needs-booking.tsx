import {createFileRoute, redirect} from '@tanstack/react-router';
import {parseRequiresAppointmentSearch, type RequiresAppointmentSearch} from '../../../pages/calendar/calendarSearch';

/**
 * The Requires Appointment page's old address (it was Needs booking). Old links (a bookmark, a half view kept in the
 * tab) go to its new one, with the same page, sort and search.
 */
export const Route = createFileRoute('/_app/calendar/needs-booking')({
  validateSearch: (raw: Record<string, unknown>): RequiresAppointmentSearch => parseRequiresAppointmentSearch(raw),
  beforeLoad: ({search}) => {
    throw redirect({to: '/calendar/requires-appointment', search, replace: true});
  },
});
