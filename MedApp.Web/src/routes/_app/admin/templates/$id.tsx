import {createFileRoute} from '@tanstack/react-router';
import {requireAdmin} from '../../../../auth/auth';
import {TemplateEditorPage} from '../../../../pages/setup/TemplateEditorPage';

export const Route = createFileRoute('/_app/admin/templates/$id')({
  beforeLoad: requireAdmin,
  component: function EditTemplateRoute() {
    return <TemplateEditorPage id={Route.useParams().id} />;
  },
});
