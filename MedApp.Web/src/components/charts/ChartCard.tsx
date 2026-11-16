import {Button} from '@astryxdesign/core/Button';
import {Icon} from '@astryxdesign/core/Icon';
import {VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import type {ReactNode} from 'react';
import {AppIcon} from '../shared/icons';
import {PanelCard} from '../views/PanelCard';

/** Whether a chart card shows its chart, or the same numbers as a table. */
export type ChartView = 'chart' | 'table';

/**
 * Every chart card's height, in pixels, whichever view it shows: tall enough for a column chart and its summary, so
 * the usual charts fit, and a longer chart or table scrolls inside the card.
 */
export const CHART_CARD_HEIGHT = 480;

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
 * A chart in a card (a `PanelCard`): its title in the header bar with a button to show it as a table (or the chart
 * again), a summary in words,
 * then the chart, or its numbers as a table. Every chart goes in one, so each has a text summary and a table of its
 * numbers (WCAG 1.1.1), and the table's links do what clicking a bar does, by keyboard too.
 *
 * The card keeps one height (`CHART_CARD_HEIGHT`) in both views, and what doesn't fit scrolls inside it, so showing a
 * long table never makes the page longer, and cards side by side stay level.
 *
 * The page keeps `view`, so the choice survives a change of layout (e.g. opening a half view).
 */
export function ChartCard({title, summary, view, onViewChange, children}: ChartCardProps) {
  return (
    <VStack height={CHART_CARD_HEIGHT}>
      <PanelCard
        title={title}
        headerActions={
          view === 'chart' ? (
            <Button label="Show Table" aria-label={`Show Table of ${title}`} variant="secondary" size="sm" icon={<Icon icon={AppIcon.Table} />} onClick={() => onViewChange('table')} />
          ) : (
            <Button label="Show Chart" aria-label={`Show Chart of ${title}`} variant="secondary" size="sm" icon={<Icon icon={AppIcon.Chart} />} onClick={() => onViewChange('chart')} />
          )
        }>
        <VStack gap={4}>
          <Text type="body" color="secondary">
            {summary}
          </Text>
          {children}
        </VStack>
      </PanelCard>
    </VStack>
  );
}
