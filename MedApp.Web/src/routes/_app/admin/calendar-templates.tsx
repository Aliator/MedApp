import {createFileRoute, redirect} from '@tanstack/react-router';
import {parseCalendarTemplatesSearch} from '../../../pages/setup/calendarTemplatesSearch';

/**
 * The old admin Calendar Templates page. Templates are each person's own now, on `/calendar/templates`, so old links
 * go there, on the same tab. The tab is kept as it came (not parsed here), so the router doesn't first redirect to a
 * tidied copy of this address.
 */
export const Route = createFileRoute('/_app/admin/calendar-templates')({
  validateSearch: (raw: Record<string, unknown>): {tab?: string} => (typeof raw.tab === 'string' ? {tab: raw.tab} : {}),
  beforeLoad: ({search}) => {
    const {tab} = parseCalendarTemplatesSearch(search);
    throw redirect({to: '/calendar/templates', search: tab ? {tab} : {}, replace: true});
  },
});
