import {createFileRoute} from '@tanstack/react-router';
import {requirePermission} from '../../../auth/auth';
import {AddPatientPage} from '../../../pages/patients/AddPatientPage';

export const Route = createFileRoute('/_app/patients/add')({
  beforeLoad: requirePermission('EditPatients'),
  component: AddPatientPage,
});
