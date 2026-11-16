import {createFileRoute} from '@tanstack/react-router';
import {preload} from '../../api/preload';
import {dayCalendarQuery} from '../../components/calendar/DayCalendar';
import {HomePage} from '../../pages/home/HomePage';
import {ukToday} from '../../utils/calendarSlots';

export const Route = createFileRoute('/_app/')({
  loader: ({context}) => preload(context.queryClient, dayCalendarQuery(ukToday())),
  component: HomePage,
});
