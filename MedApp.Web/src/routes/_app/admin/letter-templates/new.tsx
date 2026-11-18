import {createFileRoute} from '@tanstack/react-router';
import {requirePermission} from '../../../../auth/auth';
import {LetterTemplateEditorPage, letterTemplateTeamsQuery} from '../../../../pages/setup/LetterTemplateEditorPage';
import {preload} from '../../../../api/preload';
import {letterMergeFieldsQuery} from '../../../../api/letterTemplates';

export const Route = createFileRoute('/_app/admin/letter-templates/new')({
  beforeLoad: requirePermission('ManageSetup'),
  loader: ({context}) => preload(context.queryClient, letterTemplateTeamsQuery, letterMergeFieldsQuery),
  component: LetterTemplateEditorPage,
});
