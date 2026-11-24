import {Text} from '@astryxdesign/core/Text';
import {Page} from '../../components/pages/Page';
import {CardSection} from '../../components/views/CardSection';
import {ViewCard} from '../../components/views/ViewCard';

export function OrthoticPage({patientId}: {patientId: string}) {
  return (
    <Page title="Custom Orthotic" backHref={`/patients/${patientId}`} backLabel="Back to patient">
      <ViewCard showFooter={false}>
        <CardSection title="Orthotic Details">
          <Text type="body">This page is currently being built.</Text>
        </CardSection>
      </ViewCard>
    </Page>
  );
}
