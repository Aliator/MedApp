import {
  LAST_STAGE_MESSAGE,
  outcomesFromRows,
  PLAIN_STAGE,
  rowOutcomes,
  stageOptionProblems,
  stageOptionsToSend,
  type StageOptionsInput,
} from './templateBranches';

const plain = PLAIN_STAGE;
const choice: StageOptionsInput = {...plain, choiceKind: 'LabResult'};
const outcome = (name: string): StageOptionsInput => ({...plain, outcome: name});
const optional: StageOptionsInput = {...plain, isOptional: true};
const links = (pairs: [number, number][]) => pairs.map(([fromStageOrder, toStageOrder]) => ({fromStageOrder, toStageOrder}));

describe('template options and choice nodes', () => {
  it('finds nothing wrong with plain stages or an optional one, but keeps the last stage not optional', () => {
    expect(stageOptionProblems([plain, optional, plain]).size).toBe(0);
    expect(stageOptionProblems([plain, optional])).toEqual(new Map([[1, LAST_STAGE_MESSAGE]]));
  });

  it('makes a choice node’s outcomes from the names on the stages after it, one name leading to each stage given it', () => {
    const rows = [choice, outcome(' Benign '), outcome('Malignant'), outcome('benign'), plain];
    expect(outcomesFromRows(rows, links([[1, 2], [1, 3], [1, 4], [2, 5]]), 0)).toEqual([
      {name: 'Benign', nextStageOrders: [2, 4]},
      {name: 'Malignant', nextStageOrders: [3]},
    ]);
  });

  it('reads each row’s outcome back from a saved template', () => {
    const template = {stages: [{stageOrder: 1, outcomes: [{name: 'A', nextStageOrders: [2]}, {name: 'B', nextStageOrders: [3]}]}]};
    expect(rowOutcomes(template, 3)).toEqual(['', 'A', 'B']);
  });

  it('sends options only when a stage has one, a choice node with its outcomes', () => {
    expect(stageOptionsToSend([plain, outcome('A'), plain])).toBeUndefined();
    expect(stageOptionsToSend([choice, outcome('A'), outcome('B')], null, false, links([[1, 2], [1, 3]]))).toEqual([
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
