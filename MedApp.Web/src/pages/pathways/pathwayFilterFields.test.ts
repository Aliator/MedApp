import {configFor} from '../../components/tables/ListFilterBar';
import {specialtyField, stageReachedField} from './pathwayFilterFields';

const SKIN = '0000005a-0000-0000-0000-0000000000aa';
const FEET = '0000005a-0000-0000-0000-0000000000bb';
const HISTOLOGY = '0000000d-0000-0000-0000-000000000001';

describe('pathway filter fields', () => {
  it('offers specialties by name, archived ones marked, by their IDs in lower case', () => {
    const field = specialtyField(
      [
        {id: SKIN.toUpperCase(), name: 'Dermatology', isArchived: false},
        {id: FEET, name: 'Biomechanics', isArchived: true},
      ],
      undefined,
    );

    expect(field).toMatchObject({kind: 'choice', key: 'specialty', label: 'Specialty', isMultiple: true, canExclude: false});
    expect(field.kind === 'choice' && field.options).toEqual([
      {value: FEET, label: 'Biomechanics (archived)'},
      {value: SKIN, label: 'Dermatology'},
    ]);
  });

  it('offers stages as the stage reached, and keeps chosen ones it does not know (yet) as "Selected stage"', () => {
    const unknown = '0000000d-0000-0000-0000-0000000000ff';
    const field = stageReachedField([{id: HISTOLOGY, name: 'Histology (lab)'}], `${HISTOLOGY},${unknown}`);

    expect(field).toMatchObject({key: 'stage', label: 'Stage reached'});
    expect(field.kind === 'choice' && field.options).toEqual([
      {value: HISTOLOGY, label: 'Histology (lab)'},
      {value: unknown, label: 'Selected stage'},
    ]);
  });

  it('offers "is" and "is any of", but not "is not any of", which the API has no way to ask', () => {
    const operators = configFor([specialtyField([], undefined)]).fields[0].operators.map((o) => o.label);

    expect(operators).toEqual(['is', 'is any of']);
  });

  it('names one with no name by an empty label rather than failing', () => {
    const field = specialtyField([{id: SKIN, name: null}, {}], undefined);

    expect(field.kind === 'choice' && field.options.map((o) => o.label)).toEqual(['', '']);
  });
});
