import {sortRows} from './sorting';

interface Row {
  name: string | null;
  age?: number;
}

const rows: Row[] = [{name: 'bravo', age: 30}, {name: 'Alpha', age: 4}, {name: null, age: 12}, {name: 'charlie'}];
const value = (row: Row, column: string) => row[column as keyof Row];

describe('sortRows', () => {
  it('sorts text ascending, ignoring case, with empty values last', () => {
    expect(sortRows(rows, {column: 'name', ascending: true}, value).map((r) => r.name)).toEqual(['Alpha', 'bravo', 'charlie', null]);
  });

  it('sorts descending with empty values still last', () => {
    expect(sortRows(rows, {column: 'name', ascending: false}, value).map((r) => r.name)).toEqual(['charlie', 'bravo', 'Alpha', null]);
  });

  it('sorts numbers numerically', () => {
    expect(sortRows(rows, {column: 'age', ascending: true}, value).map((r) => r.age)).toEqual([4, 12, 30, undefined]);
  });

  it('keeps the original order of equal values', () => {
    const tied = [{name: 'a', id: 1}, {name: 'a', id: 2}, {name: 'a', id: 3}];
    expect(sortRows(tied, {column: 'name', ascending: false}, (r) => r.name).map((r) => r.id)).toEqual([1, 2, 3]);
  });

  it('returns a copy in the original order when there is no sort', () => {
    const result = sortRows(rows, null, value);
    expect(result).toEqual(rows);
    expect(result).not.toBe(rows);
  });
});
