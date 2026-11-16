import {createFileRoute} from '@tanstack/react-router';
import {twoFactorStatusQuery} from '../../../api/account';
import {preload} from '../../../api/preload';
import {SecurityPage} from '../../../pages/account/SecurityPage';

export const Route = createFileRoute('/_app/account/security')({
  loader: ({context}) => preload(context.queryClient, twoFactorStatusQuery),
  component: SecurityPage,
});
