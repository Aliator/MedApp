import {createFileRoute} from '@tanstack/react-router';
import {requireAdmin} from '../../../../auth/auth';
import {TemplateEditorPage} from '../../../../pages/setup/TemplateEditorPage';

export const Route = createFileRoute('/_app/admin/templates/new')({
  beforeLoad: requireAdmin,
  component: TemplateEditorPage,
});
