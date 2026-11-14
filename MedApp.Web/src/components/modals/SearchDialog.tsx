import {Button} from '@astryxdesign/core/Button';
import type {DateRange} from '@astryxdesign/core/DateRangeInput';
import {FormLayout} from '@astryxdesign/core/FormLayout';
import {Icon} from '@astryxdesign/core/Icon';
import {Selector} from '@astryxdesign/core/Selector';
import {TextInput} from '@astryxdesign/core/TextInput';
import {DateOfBirthRangeInput} from '../forms/DateOfBirthRangeInput';
import {AppIcon} from '../shared/icons';
import {DialogFrame} from './DialogFrame';

/** One search field (replaces Blazor's SearchFieldDefinition). */
export type SearchField =
  | {type: 'text'; key: string; label: string; placeholder?: string}
  | {type: 'select'; key: string; label: string; options: {value: string; label: string}[]; placeholder?: string}
  | {type: 'dateRange'; key: string; label: string};

/** The values of a search, by field key: text and select fields hold strings, date ranges a DateRange or null. */
export type SearchValues = Record<string, string | DateRange | null | undefined>;

export interface SearchDialogProps {
  isOpen: boolean;
  title?: string;
  fields: SearchField[];
  values: SearchValues;
  onValuesChange: (values: SearchValues) => void;
  /** Runs the search; also on Enter in a text field. */
  onSearch: () => void;
  /** Shows a Clear button that empties every field. */
  onClear?: () => void;
  onClose: () => void;
}

/**
 * A search form in a dialog, built from field definitions (replaces Blazor's SearchModal), so the patients
 * and users lists share it. Values are controlled, so the page can keep them in the URL.
 */
export function SearchDialog({isOpen, title = 'Search', fields, values, onValuesChange, onSearch, onClear, onClose}: SearchDialogProps) {
  const set = (key: string, value: string | DateRange | null) => onValuesChange({...values, [key]: value});
  const text = (key: string) => {
    const value = values[key];
    return typeof value === 'string' ? value : '';
  };

  return (
    <DialogFrame
      isOpen={isOpen}
      title={title}
      onClose={onClose}
      width={480}
      footer={
        <>
          {onClear && <Button label="Clear" variant="secondary" onClick={onClear} />}
          <Button label="Search" variant="primary" icon={<Icon icon={AppIcon.Search} />} onClick={onSearch} />
        </>
      }>
      <FormLayout defaultOptionality="optional">
        {fields.map((field) => {
          switch (field.type) {
            case 'text':
              return (
                <TextInput
                  key={field.key}
                  label={field.label}
                  placeholder={field.placeholder}
                  value={text(field.key)}
                  onChange={(value) => set(field.key, value)}
                  onEnter={onSearch}
                />
              );
            case 'select':
              return (
                <Selector
                  key={field.key}
                  label={field.label}
                  placeholder={field.placeholder}
                  options={field.options}
                  value={text(field.key) || null}
                  onChange={(value: string | null) => set(field.key, value ?? '')}
                  hasClear
                />
              );
            case 'dateRange': {
              const value = values[field.key];
              return (
                <DateOfBirthRangeInput
                  key={field.key}
                  label={field.label}
                  value={value && typeof value === 'object' ? value : null}
                  onChange={(range) => set(field.key, range)}
                />
              );
            }
          }
        })}
      </FormLayout>
    </DialogFrame>
  );
}
