import {createFileRoute} from '@tanstack/react-router';
import {requireAdmin} from '../../../../auth/auth';
import {TemplateEditorPage} from '../../../../pages/setup/TemplateEditorPage';
import {preload} from '../../../../api/preload';
import {templateQuery} from '../../../../api/procedureSetup';
import {stageDefinitionsQuery} from '../../../../api/procedureSetup';

export const Route = createFileRoute('/_app/admin/templates/$id')({
  beforeLoad: requireAdmin,
  loader: ({context, params}) => preload(context.queryClient, templateQuery(params.id), stageDefinitionsQuery),
  component: function EditTemplateRoute() {
    return <TemplateEditorPage id={Route.useParams().id} />;
  },
});
