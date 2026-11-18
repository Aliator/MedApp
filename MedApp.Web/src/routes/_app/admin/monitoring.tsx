import {createFileRoute} from '@tanstack/react-router';
import {requireAdmin} from '../../../auth/auth';
import {preload} from '../../../api/preload';
import {monitoringPeopleQuery, monitoringRulesQuery} from '../../../api/monitoring';
import {allTemplatesQuery, stageDefinitionsQuery} from '../../../api/pathwaySetup';
import {MonitoringPage} from '../../../pages/monitoring/MonitoringPage';

/** Admin only: the signed-in admin's monitoring rules. */
export const Route = createFileRoute('/_app/admin/monitoring')({
  beforeLoad: requireAdmin,
  loader: ({context}) =>
    preload(context.queryClient, monitoringRulesQuery, monitoringPeopleQuery, allTemplatesQuery, stageDefinitionsQuery),
  component: MonitoringPage,
});
