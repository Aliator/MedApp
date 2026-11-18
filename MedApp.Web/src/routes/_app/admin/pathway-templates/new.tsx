import {createFileRoute} from '@tanstack/react-router';
import {requireAdmin} from '../../../../auth/auth';
import {PathwayTemplateEditorPage, templateSpecialtiesQuery} from '../../../../pages/setup/PathwayTemplateEditorPage';
import {preload} from '../../../../api/preload';
import {stageDefinitionsQuery} from '../../../../api/pathwaySetup';

export const Route = createFileRoute('/_app/admin/pathway-templates/new')({
  beforeLoad: requireAdmin,
  loader: ({context}) => preload(context.queryClient, stageDefinitionsQuery, templateSpecialtiesQuery),
  component: PathwayTemplateEditorPage,
});
