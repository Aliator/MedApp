import {createFileRoute} from '@tanstack/react-router';
import {requirePermission} from '../../../auth/auth';
import {preload} from '../../../api/preload';
import {monitoringPeopleQuery, monitoringRulesQuery} from '../../../api/monitoring';
import {allTemplatesQuery, stageDefinitionsQuery} from '../../../api/pathwaySetup';
import {MonitoringPage} from '../../../pages/monitoring/MonitoringPage';

/** For holders of ManageMonitoring: their own monitoring rules. */
export const Route = createFileRoute('/_app/admin/monitoring')({
  beforeLoad: requirePermission('ManageMonitoring'),
  loader: ({context}) =>
    preload(context.queryClient, monitoringRulesQuery, monitoringPeopleQuery, allTemplatesQuery, stageDefinitionsQuery),
  component: MonitoringPage,
});
