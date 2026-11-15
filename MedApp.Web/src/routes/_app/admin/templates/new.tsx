import {createFileRoute} from '@tanstack/react-router';
import {requireAdmin} from '../../../../auth/auth';
import {TemplateEditorPage} from '../../../../pages/setup/TemplateEditorPage';
import {preload} from '../../../../api/preload';
import {stageDefinitionsQuery} from '../../../../api/procedureSetup';

export const Route = createFileRoute('/_app/admin/templates/new')({
  beforeLoad: requireAdmin,
  loader: ({context}) => preload(context.queryClient, stageDefinitionsQuery),
  component: TemplateEditorPage,
});
