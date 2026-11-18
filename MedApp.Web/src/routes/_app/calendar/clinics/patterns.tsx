import {createFileRoute} from '@tanstack/react-router';
import {patternsQuery} from '../../../../api/clinics';
import {preload} from '../../../../api/preload';
import {requirePermission} from '../../../../auth/auth';
import {ClinicPatternsPage} from '../../../../pages/calendar/ClinicPatternsPage';

/** For holders of ManageClinics: the clinic patterns, with Create sessions. Anyone else is told the page needs it. */
export const Route = createFileRoute('/_app/calendar/clinics/patterns')({
  beforeLoad: requirePermission('ManageClinics'),
  loader: ({context}) => preload(context.queryClient, patternsQuery),
  component: ClinicPatternsPage,
});
