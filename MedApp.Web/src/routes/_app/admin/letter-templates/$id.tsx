import {createFileRoute} from '@tanstack/react-router';
import {requirePermission} from '../../../../auth/auth';
import {LetterTemplateEditorPage, letterTemplateTeamsQuery} from '../../../../pages/setup/LetterTemplateEditorPage';
import {preload} from '../../../../api/preload';
import {letterMergeFieldsQuery, letterTemplateQuery} from '../../../../api/letterTemplates';

export const Route = createFileRoute('/_app/admin/letter-templates/$id')({
  beforeLoad: requirePermission('ManageSetup'),
  loader: ({context, params}) =>
    preload(context.queryClient, letterTemplateQuery(params.id), letterTemplateTeamsQuery, letterMergeFieldsQuery),
  component: function EditLetterTemplateRoute() {
    return <LetterTemplateEditorPage id={Route.useParams().id} />;
  },
});
