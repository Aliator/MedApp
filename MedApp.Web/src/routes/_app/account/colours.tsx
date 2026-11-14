import {createFileRoute} from '@tanstack/react-router';
import {ColoursPage} from '../../../pages/account/ColoursPage';

export const Route = createFileRoute('/_app/account/colours')({
  component: ColoursPage,
});
