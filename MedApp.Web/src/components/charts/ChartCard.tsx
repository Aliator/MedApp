import {Icon} from '@astryxdesign/core/Icon';
import {SegmentedControl, SegmentedControlItem} from '@astryxdesign/core/SegmentedControl';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import type {ReactNode} from 'react';
import {AppIcon} from '../shared/icons';
import {PanelCard} from '../views/PanelCard';

/** Whether a chart card shows its chart, or the same numbers as a table. */
export type ChartView = 'chart' | 'table';

export interface ChartCardProps {
  /** The header bar's title, e.g. "Overdue by assignee". */
  title: string;
  /** What the chart says, in a sentence or two, above it: the headline for everyone, and for screen readers. */
  summary: string;
  view: ChartView;
  onViewChange: (view: ChartView) => void;
  /** The chart (`BarChart`) or the table (`ChartTable`), whichever `view` says. */
  children: ReactNode;
}

/**
 * A chart in a card (a `PanelCard`): its title in the header bar with a Chart and Table switch, a summary in words,
 * then the chart, or its numbers as a table. Every chart goes in one, so each has a text summary and a table of its
 * numbers (WCAG 1.1.1), and the table's links do what clicking a bar does, by keyboard too.
 *
 * The page keeps `view`, so the choice survives a change of layout (e.g. opening a half view).
 */
export function ChartCard({title, summary, view, onViewChange, children}: ChartCardProps) {
  return (
    <PanelCard
      title={title}
      headerActions={
        <SegmentedControl label={`Show ${title} as`} value={view} onChange={(next) => onViewChange(next as ChartView)} size="sm">
          <SegmentedControlItem value="chart" label="Chart" icon={<Icon icon={AppIcon.Chart} />} />
          <SegmentedControlItem value="table" label="Table" icon={<Icon icon={AppIcon.Table} />} />
        </SegmentedControl>
      }>
      <VStack gap={4}>
        <Text type="body" color="secondary">
          {summary}
        </Text>
        {children}
      </VStack>
    </PanelCard>
  );
}
