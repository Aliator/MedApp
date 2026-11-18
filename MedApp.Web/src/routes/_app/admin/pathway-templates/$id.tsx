import {createFileRoute} from '@tanstack/react-router';
import {requirePermission} from '../../../../auth/auth';
import {PathwayTemplateEditorPage} from '../../../../pages/setup/PathwayTemplateEditorPage';
import {preload} from '../../../../api/preload';
import {templateQuery} from '../../../../api/pathwaySetup';
import {stageDefinitionsQuery} from '../../../../api/pathwaySetup';

export const Route = createFileRoute('/_app/admin/pathway-templates/$id')({
  beforeLoad: requirePermission('ManageSetup'),
  loader: ({context, params}) => preload(context.queryClient, templateQuery(params.id), stageDefinitionsQuery),
  component: function EditTemplateRoute() {
    return <PathwayTemplateEditorPage id={Route.useParams().id} />;
  },
});
