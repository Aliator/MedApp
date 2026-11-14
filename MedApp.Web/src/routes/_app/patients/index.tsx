import {createFileRoute} from '@tanstack/react-router';
import {NotPortedPage} from '../../../pages/placeholder/NotPortedPage';

export const Route = createFileRoute('/_app/patients/')({
  component: () => <NotPortedPage title="Patients" />,
});
