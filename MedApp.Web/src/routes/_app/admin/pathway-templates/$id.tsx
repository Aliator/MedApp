import {createFileRoute} from '@tanstack/react-router';
import {requireAdmin} from '../../../../auth/auth';
import {PathwayTemplateEditorPage, templateSpecialtiesQuery} from '../../../../pages/setup/PathwayTemplateEditorPage';
import {preload} from '../../../../api/preload';
import {templateQuery} from '../../../../api/pathwaySetup';
import {stageDefinitionsQuery} from '../../../../api/pathwaySetup';

export const Route = createFileRoute('/_app/admin/pathway-templates/$id')({
  beforeLoad: requireAdmin,
  loader: ({context, params}) => preload(context.queryClient, templateQuery(params.id), stageDefinitionsQuery, templateSpecialtiesQuery),
  component: function EditTemplateRoute() {
    return <PathwayTemplateEditorPage id={Route.useParams().id} />;
  },
});
