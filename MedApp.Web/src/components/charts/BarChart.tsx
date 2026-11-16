import {Card} from '@astryxdesign/core/Card';
import {HStack, StackItem, VStack} from '@astryxdesign/core/Stack';
import {Text} from '@astryxdesign/core/Text';
import {useCallback, useState} from 'react';
import {Bar, BarChart as Chart, CartesianGrid, LabelList, Tooltip, XAxis, YAxis} from 'recharts';
import {ColourSwatches} from '../forms/ColourField';
import {CHART_INK} from './chartColours';

/** One set of bars: its values are `item[key]`, named `label` in the legend, tooltip and table. */
export interface ChartSeries {
  key: string;
  label: string;
  /** A token from `SERIES_COLOURS`, in order. */
  colour: string;
}

export interface BarChartProps<T extends object> {
  data: readonly T[];
  /** The field naming each bar, or each group of bars: a category, or a week. */
  categoryKey: keyof T & string;
  series: readonly ChartSeries[];
  /**
   * `rows`: a bar per row, left to right, with its name beside it (names that can be long, e.g. people or stages).
   * `columns`: upright bars along the bottom (e.g. weeks).
   */
  layout?: 'rows' | 'columns';
  /** Stack the series into one bar per category (parts of a whole) rather than side by side. */
  isStacked?: boolean;
  /** A value in words, e.g. "3" or "2 h 30 min". */
  formatValue?: (value: number) => string;
  /** A category in words, e.g. a week's Monday as "8 Jun". */
  formatCategory?: (value: string) => string;
  /** Called with the item whose bar is clicked: e.g. to open the list it counts. Tables carry the same as links. */
  onSelect?: (item: T) => void;
  /**
   * What the chart shows, in a sentence: its name for screen readers, which read the chart as one image (its numbers
   * are in the card's table).
   */
  description: string;
}

const ROW_HEIGHT = 36;
const AXIS_BAND = 32;
const COLUMNS_HEIGHT = 240;
const NAME_WIDTH = 128;
const NAME_CHARACTERS = 18;

const ink = {fill: CHART_INK.axisText, fontSize: CHART_INK.fontSize};

const shorten = (text: string) => (text.length > NAME_CHARACTERS ? `${text.slice(0, NAME_CHARACTERS - 1)}…` : text);
const plain = (value: number) => String(value);

/**
 * The width of an element, kept up to date as it changes. Zero until it's measured: on the server, and in the first
 * render in the browser, so that matches the server's.
 */
function useWidth(): [(element: HTMLElement | null) => (() => void) | undefined, number] {
  const [width, setWidth] = useState(0);
  const ref = useCallback((element: HTMLElement | null) => {
    if (!element || typeof ResizeObserver === 'undefined') return;
    const observer = new ResizeObserver((entries) => setWidth(Math.floor(entries[0]?.contentRect.width ?? 0)));
    observer.observe(element);
    return () => observer.disconnect();
  }, []);
  return [ref, width];
}

interface TooltipEntry {
  name?: string | number;
  value?: number | string | readonly (number | string)[];
  color?: string;
}

export interface ChartTooltipProps {
  active?: boolean;
  payload?: readonly TooltipEntry[];
  label?: string | number;
  formatValue?: (value: number) => string;
  formatCategory?: (value: string) => string;
}

/** The box under the pointer: the category, and each series' value beside its colour. */
export function ChartTooltip({active, payload, label, formatValue = plain, formatCategory = String}: ChartTooltipProps) {
  if (!active || !payload?.length) return null;
  return (
    <Card padding={3}>
      <VStack gap={1}>
        <Text type="supporting" weight="semibold">
          {formatCategory(String(label ?? ''))}
        </Text>
        {payload.map((entry) => (
          <HStack key={String(entry.name)} gap={2} vAlign="center">
            {entry.color && <ColourSwatches colours={[entry.color]} />}
            <Text type="supporting">
              {entry.name}: {formatValue(Number(entry.value))}
            </Text>
          </HStack>
        ))}
      </VStack>
    </Card>
  );
}

/** The series' names beside their colours, under a chart of two or more. */
export function ChartLegend({series}: {series: readonly ChartSeries[]}) {
  return (
    <HStack gap={4} wrap="wrap" aria-hidden>
      {series.map((s) => (
        <HStack key={s.key} gap={2} vAlign="center">
          <ColourSwatches colours={[s.colour]} />
          <Text type="supporting">{s.label}</Text>
        </HStack>
      ))}
    </HStack>
  );
}

/**
 * A bar chart (Recharts) in the app's look: the theme's tokens for its colours, so it follows the light and dark
 * modes and the colour themes; thin bars with rounded ends and a gap between neighbours; recessive axes; a tooltip
 * under the pointer; and a legend for two series or more. It fills its container's width.
 *
 * Use it inside a `ChartCard`, which gives every chart its title, a summary in words, and a table of its numbers for
 * anyone who can't see or point at the chart. Screen readers read the chart as one image named by `description`.
 */
export function BarChart<T extends object>({
  data,
  categoryKey,
  series,
  layout = 'rows',
  isStacked = false,
  formatValue = plain,
  formatCategory = String,
  onSelect,
  description,
}: BarChartProps<T>) {
  const [ref, width] = useWidth();
  const isRows = layout === 'rows';
  const height = isRows ? data.length * ROW_HEIGHT + AXIS_BAND : COLUMNS_HEIGHT;
  // A bar's free end is rounded: the right in rows, the top in columns; stacked, only the last series' end.
  const end: [number, number, number, number] = isRows ? [0, 4, 4, 0] : [4, 4, 0, 0];
  const valueAxis = {type: 'number' as const, tick: ink, tickFormatter: formatValue, axisLine: false, tickLine: false, allowDecimals: false};
  const nameAxis = {
    type: 'category' as const,
    dataKey: categoryKey as string,
    tick: ink,
    tickFormatter: (value: unknown) => shorten(formatCategory(String(value))),
    axisLine: false,
    tickLine: false,
  };

  return (
    <VStack gap={3}>
      <StackItem ref={ref} role="img" aria-label={description} style={{blockSize: height}}>
        {width > 0 && (
          <Chart
            width={width}
            height={height}
            data={data as T[]}
            layout={isRows ? 'vertical' : 'horizontal'}
            margin={{top: 4, right: isRows ? 32 : 8, bottom: 0, left: 0}}
            barCategoryGap="25%"
            // The chart is one image to screen readers; its numbers are in the card's table, with its links.
            accessibilityLayer={false}>
            <CartesianGrid stroke={CHART_INK.grid} horizontal={!isRows} vertical={isRows} />
            {isRows ? (
              <>
                <XAxis {...valueAxis} />
                <YAxis {...nameAxis} width={NAME_WIDTH} />
              </>
            ) : (
              <>
                <XAxis {...nameAxis} interval="preserveStartEnd" />
                <YAxis {...valueAxis} width={40} />
              </>
            )}
            <Tooltip
              content={<ChartTooltip formatValue={formatValue} formatCategory={formatCategory} />}
              cursor={{fill: CHART_INK.grid}}
              isAnimationActive={false}
            />
            {series.map((s, index) => (
              <Bar
                key={s.key}
                dataKey={s.key}
                name={s.label}
                fill={s.colour}
                stackId={isStacked ? 'stack' : undefined}
                radius={!isStacked || index === series.length - 1 ? end : 0}
                stroke={CHART_INK.gap}
                strokeWidth={2}
                maxBarSize={24}
                isAnimationActive={false}
                cursor={onSelect ? 'pointer' : undefined}
                onClick={onSelect ? (_, i) => onSelect(data[i]) : undefined}>
                {/* One series in rows: each bar's value at its end, as the bars are few and the numbers matter. */}
                {isRows && series.length === 1 && (
                  <LabelList dataKey={s.key} position="right" fill={CHART_INK.axisText} fontSize={CHART_INK.fontSize} formatter={(v: unknown) => formatValue(Number(v))} />
                )}
              </Bar>
            ))}
          </Chart>
        )}
      </StackItem>
      {series.length > 1 && <ChartLegend series={series} />}
    </VStack>
  );
}
