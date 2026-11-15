import {createFileRoute} from '@tanstack/react-router';
import {AddPatientPage} from '../../../pages/patients/AddPatientPage';

export const Route = createFileRoute('/_app/patients/add')({
  component: AddPatientPage,
});
