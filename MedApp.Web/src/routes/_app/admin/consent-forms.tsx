import {createFileRoute} from '@tanstack/react-router';
import {preload} from '../../../api/preload';
import {requirePermission} from '../../../auth/auth';
import {ConsentFormsPage, consentFormsPageQuery} from '../../../pages/setup/ConsentFormsPage';

/** For holders of ManageSetup: the consent forms consent is recorded on. */
export const Route = createFileRoute('/_app/admin/consent-forms')({
  beforeLoad: requirePermission('ManageSetup'),
  loader: ({context}) => preload(context.queryClient, consentFormsPageQuery),
  component: ConsentFormsPage,
});
