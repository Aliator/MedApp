import {Icon} from '@astryxdesign/core/Icon';
import {IconButton} from '@astryxdesign/core/IconButton';
import {List, ListItem} from '@astryxdesign/core/List';
import {VStack} from '@astryxdesign/core/Stack';
import {useState} from 'react';
import {Token} from '@astryxdesign/core/Token';
import {Text} from '@astryxdesign/core/Text';
import {useColorMode, type ColorMode} from '../../app/colorMode';
import {COLOUR_THEMES, readabilityWarning, themeOf, useColours, type ColourPart, type ModeColours} from '../../app/colours';
import {ColourField, ColourSwatches} from '../../components/forms/ColourField';
import {Page} from '../../components/pages/Page';
import {TwoColumns} from '../../components/pages/TwoColumns';
import {AppIcon} from '../../components/shared/icons';
import {PanelCard} from '../../components/views/PanelCard';

const PARTS: {part: ColourPart; label: string; description: string}[] = [
  {part: 'page', label: 'Page background', description: 'Behind the cards.'},
  {
    part: 'card',
    label: 'Cards',
    description: 'Their header bars and footers follow: slightly darker in light mode, a step lighter in dark.',
  },
  {part: 'nav', label: 'Side nav', description: 'Its text stays light, so a dark colour reads best.'},
];

const swatches = (colours: ModeColours) => [colours.page, colours.card, colours.nav];
/** The chosen option's tick. Every option keeps its space (hidden where not chosen), so their names line up. */
const chosen = (isChosen: boolean) => (
  <Icon icon={AppIcon.Check} style={isChosen ? undefined : {visibility: 'hidden'}} />
);

/**
 * The app's colours (Account Settings), a card for light mode and one for dark, with the same choices: a colour
 * theme (Standard is the app's own), or Custom to pick the page, card and side nav colours yourself. Clicking a
 * card's header switches the app to that mode. Custom folds the themes away and
 * shows its colour pickers; choosing it again folds them and shows the themes. The app recolours at once, and the
 * choice is kept in this browser. A custom colour that leaves text hard to read (under 4.5:1) is warned about on its
 * field.
 */
export function ColoursPage() {
  const {colours} = useColours();
  const {mode: activeMode, setMode} = useColorMode();
  // Custom is open where the colours are no theme's (they were picked by hand).
  const [isCustomOpen, setIsCustomOpen] = useState<Record<ColorMode, boolean>>(() => ({
    light: themeOf('light', colours.light) === null,
    dark: themeOf('dark', colours.dark) === null,
  }));

  const card = (mode: ColorMode, title: string) => (
    // Clicking a card's header switches the app to its mode, so its colours show (as does choosing anything in it,
    // which is the way from the keyboard). The mode in use is marked Active.
    <PanelCard
      title={title}
      onHeaderClick={() => setMode(mode)}
      headerActions={mode === activeMode && <Token label="Active" color="blue" />}>
      <ModeColoursPicker
        mode={mode}
        isCustomOpen={isCustomOpen[mode]}
        onCustomOpenChange={(isOpen) => setIsCustomOpen((current) => ({...current, [mode]: isOpen}))}
      />
    </PanelCard>
  );

  return (
    <Page
      title="Colours"
      subtitle="Changes show as you pick them"
      backHref="/account"
      backLabel="Back to account settings"
      fill>
      <TwoColumns start={card('light', 'Light Mode')} end={card('dark', 'Dark Mode')} />
    </Page>
  );
}

interface ModeColoursPickerProps {
  mode: ColorMode;
  isCustomOpen: boolean;
  onCustomOpenChange: (isOpen: boolean) => void;
}

/** One mode's colours: the themes and Custom, or (Custom open) Custom alone with its colour pickers. */
/**
 * An option's colours with its name under them, centred: stacked, so a narrow card (a half view) has room for
 * both, and the name never runs into the colours.
 */
function SwatchesLabel({name, colours}: {name: string; colours: readonly string[]}) {
  return (
    <VStack gap={2} hAlign="center">
      <ColourSwatches colours={colours} size="lg" />
      <Text type="large">{name}</Text>
    </VStack>
  );
}

function ModeColoursPicker({mode, isCustomOpen, onCustomOpenChange}: ModeColoursPickerProps) {
  const {colours, set, setAll} = useColours();
  // Choosing anything in a mode's card switches the app to that mode, so the change shows at once.
  const {setMode} = useColorMode();
  const current = colours[mode];
  const theme = themeOf(mode, current);

  const custom = (
    <ListItem
      key="custom"
      itemKey="custom"
      label={<SwatchesLabel name="Custom" colours={swatches(current)} />}
      startContent={
        // Open, it leads back to the themes, so it shows a back arrow.
        isCustomOpen && (
          <IconButton
            label="Back to the themes"
            icon={<Icon icon={AppIcon.Back} />}
            variant="ghost"
            onClick={() => onCustomOpenChange(false)}
          />
        )
      }
      endContent={chosen(isCustomOpen || !theme)}
      isSelected={isCustomOpen || !theme}
      onClick={() => {
        setMode(mode);
        onCustomOpenChange(!isCustomOpen);
      }}
    />
  );

  return (
    <VStack gap={4}>
      <List hasDividers density="spacious" aria-label={`${mode === 'light' ? 'Light' : 'Dark'} mode colour themes`}>
        {!isCustomOpen &&
          COLOUR_THEMES.map((t) => (
            <ListItem
              key={t.slug}
              itemKey={t.slug}
              label={<SwatchesLabel name={t.name} colours={swatches(t[mode])} />}
              endContent={chosen(theme?.slug === t.slug)}
              isSelected={theme?.slug === t.slug}
              onClick={() => {
                setMode(mode);
                setAll(mode, t[mode]);
              }}
            />
          ))}
        {custom}
      </List>
      {isCustomOpen &&
        PARTS.map(({part, label, description}) => (
          <ColourField
            key={part}
            label={label}
            description={description}
            value={current[part]}
            warning={readabilityWarning(mode, part, current[part])}
            onChange={(colour) => {
              setMode(mode);
              set(mode, part, colour);
            }}
          />
        ))}
    </VStack>
  );
}
