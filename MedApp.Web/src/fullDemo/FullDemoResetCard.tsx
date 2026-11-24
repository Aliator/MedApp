import {Text} from '@astryxdesign/core/Text';
import {CardSection} from '../components/views/CardSection';
import {ViewCard} from '../components/views/ViewCard';
import {FullDemoResetButton} from './FullDemoResetButton';
import {useStartup} from './FullDemoProgress';

/** Account Settings' card in the full demo: when this browser's copy of the data was published, and Reset the Demo. */
export function FullDemoResetCard() {
  const {version} = useStartup();
  return (
    <ViewCard footerActions={<FullDemoResetButton path="/" />}>
      <CardSection title="This Demo">
        <Text type="body">
          {`MedApp runs in this browser, on its own copy of the demo data${version?.builtOn ? ` (published ${version.builtOn})` : ''}. Reset the Demo downloads the published data again, without your changes; your settings stay.`}
        </Text>
      </CardSection>
    </ViewCard>
  );
}
