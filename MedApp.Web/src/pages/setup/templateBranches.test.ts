import {
  LAST_STAGE_MESSAGE,
  outcomeRow,
  outcomeRows,
  outcomesFromRows,
  PLAIN_STAGE,
  stageOptionProblems,
  stageOptionsToSend,
  type OutcomeRow,
  type StageOptionsInput,
} from './templateBranches';

const plain = PLAIN_STAGE;
const optional: StageOptionsInput = {...plain, isOptional: true};
const keyed = (rows: StageOptionsInput[]) => rows.map((r, i) => ({...r, key: `r${i + 1}`}));
const choice = (outcomes: [string, number[]][]): StageOptionsInput => ({
  ...plain,
  choiceKind: 'LabResult',
  outcomes: outcomes.map(([name, next]) => outcomeRow(name, next.map((n) => `r${n}`))),
});
const links = (pairs: [number, number][]) => pairs.map(([fromStageOrder, toStageOrder]) => ({fromStageOrder, toStageOrder}));
const named = (outcomes: OutcomeRow[]) => outcomes.map((o) => [o.name, o.nextKeys]);

describe('template options and choice nodes', () => {
  it('finds nothing wrong with plain stages or an optional one, but keeps the last stage not optional', () => {
    expect(stageOptionProblems([plain, optional, plain]).size).toBe(0);
    expect(stageOptionProblems([plain, optional])).toEqual(new Map([[1, LAST_STAGE_MESSAGE]]));
  });

  it('sends a choice node’s outcomes in their order, trimmed, each to the stages after it that it leads to', () => {
    const rows = keyed([choice([[' Malignant ', [3, 2]], ['Benign', [4]], ['Uncertain', [2]], ['Discharge', []], ['', [5]]]), plain, plain, plain, plain]);
    // Stage 5 doesn't come after the choice node, and the unnamed outcome isn't sent.
    expect(outcomesFromRows(rows, links([[1, 2], [1, 3], [1, 4], [2, 5]]), 0)).toEqual([
      {name: 'Malignant', nextStageOrders: [2, 3]},
      {name: 'Benign', nextStageOrders: [4]},
      {name: 'Uncertain', nextStageOrders: [2]},
      {name: 'Discharge', nextStageOrders: []},
    ]);
  });

  it('reads a saved choice node’s outcomes onto the rows by key, several to one stage and one to none', () => {
    const rows = keyed([plain, plain, plain]);
    const outcomes = [
      {name: 'A', nextStageOrders: [2, 3]},
      {name: 'B', nextStageOrders: [3]},
      {name: 'C', nextStageOrders: []},
    ];
    expect(named(outcomeRows(outcomes, rows))).toEqual([
      ['A', ['r2', 'r3']],
      ['B', ['r3']],
      ['C', []],
    ]);
  });

  it('sends options only when a stage has one, a choice node with its outcomes', () => {
    expect(stageOptionsToSend(keyed([plain, plain, plain]))).toBeUndefined();
    expect(stageOptionsToSend(keyed([choice([['A', [2]], ['B', [3]]]), plain, plain]), null, false, links([[1, 2], [1, 3]]))).toEqual([
      {
        isOptional: false,
        choiceKind: 'LabResult',
        outcomes: [
          {name: 'A', nextStageOrders: [2]},
          {name: 'B', nextStageOrders: [3]},
        ],
      },
      {isOptional: false, choiceKind: null, outcomes: null},
      {isOptional: false, choiceKind: null, outcomes: null},
    ]);
  });
});
