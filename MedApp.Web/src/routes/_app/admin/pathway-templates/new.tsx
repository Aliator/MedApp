import {createFileRoute} from '@tanstack/react-router';
import {requirePermission} from '../../../../auth/auth';
import {PathwayTemplateEditorPage, templateTeamsQuery} from '../../../../pages/setup/PathwayTemplateEditorPage';
import {preload} from '../../../../api/preload';
import {stageDefinitionsQuery} from '../../../../api/pathwaySetup';

export const Route = createFileRoute('/_app/admin/pathway-templates/new')({
  beforeLoad: requirePermission('ManageSetup'),
  loader: ({context}) => preload(context.queryClient, stageDefinitionsQuery, templateTeamsQuery),
  component: PathwayTemplateEditorPage,
});
