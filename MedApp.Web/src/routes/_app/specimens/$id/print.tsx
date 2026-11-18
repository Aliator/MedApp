import {createFileRoute} from '@tanstack/react-router';
import {requirePermission} from '../../../../auth/auth';
import {preload} from '../../../../api/preload';
import {specimenQuery, type SpecimenSheet} from '../../../../api/specimens';
import {SpecimenPrintPage} from '../../../../pages/specimens/SpecimenPrintPage';
import {lowerCaseId} from '../../../../utils/routeIds';

/** The label (the default) or the request sheet: `?sheet=request`. */
const parseSheet = (raw: Record<string, unknown>): {sheet?: SpecimenSheet} => (raw.sheet === 'request' ? {sheet: 'request'} : {});

/** A specimen's label or request sheet, to print (opening it is recorded as viewing the specimen). */
export const Route = createFileRoute('/_app/specimens/$id/print')({
  beforeLoad: requirePermission('ViewPatients'),
  params: lowerCaseId,
  validateSearch: parseSheet,
  loader: ({context, params}) => preload(context.queryClient, specimenQuery(params.id)),
  component: function SpecimenPrintRoute() {
    const {id} = Route.useParams();
    return <SpecimenPrintPage key={id} id={id} sheet={Route.useSearch().sheet ?? 'label'} />;
  },
});
