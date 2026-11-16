import {createFileRoute} from '@tanstack/react-router';
import {featuresQuery, myEmailQuery} from '../../../api/account';
import {preload} from '../../../api/preload';
import {EmailPage} from '../../../pages/account/EmailPage';

export const Route = createFileRoute('/_app/account/email')({
  loader: ({context}) => preload(context.queryClient, myEmailQuery, featuresQuery),
  component: EmailPage,
});
