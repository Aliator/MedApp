import {
  BRANCH_POINT_OPTIONS_MESSAGE,
  BRANCH_TOO_LONG_MESSAGE,
  BRANCH_WITHOUT_POINT_MESSAGE,
  branchesAfter,
  isAfterBranchPoint,
  LAST_STAGE_MESSAGE,
  PLAIN_STAGE,
  stageOptionProblems,
  stageOptionsToSend,
  TOO_FEW_BRANCHES_MESSAGE,
  type StageOptionsInput,
} from './templateBranches';

const plain = PLAIN_STAGE;
const point: StageOptionsInput = {...plain, choosesBranch: true};
const inBranch = (branch: string, isOptional = false): StageOptionsInput => ({...plain, branch, isOptional});
const optional: StageOptionsInput = {...plain, isOptional: true};

describe('template branches', () => {
  it('finds nothing wrong with plain stages, an optional one, or a branch point with two branches', () => {
    expect(stageOptionProblems([plain, optional, plain]).size).toBe(0);
    expect(stageOptionProblems([plain, point, inBranch('Benign'), inBranch('Treatment', true), inBranch('treatment'), plain]).size).toBe(0);
  });

  it('keeps the last stage plain', () => {
    expect(stageOptionProblems([plain, optional])).toEqual(new Map([[1, LAST_STAGE_MESSAGE]]));
    expect(stageOptionProblems([point, inBranch('A'), inBranch('B')]).get(2)).toBe(LAST_STAGE_MESSAGE);
  });

  it('needs two different branches after a branch point, ignoring case, before the next one', () => {
    expect(stageOptionProblems([point, inBranch('A'), inBranch('a'), plain]).get(0)).toBe(TOO_FEW_BRANCHES_MESSAGE);
    expect(stageOptionProblems([point, inBranch('A'), point, inBranch('B'), inBranch('C'), plain]).get(0)).toBe(TOO_FEW_BRANCHES_MESSAGE);
  });

  it("won't have a branch point optional or in a branch, nor a branch before any branch point", () => {
    expect(stageOptionProblems([{...point, isOptional: true}, inBranch('A'), inBranch('B'), plain]).get(0)).toBe(BRANCH_POINT_OPTIONS_MESSAGE);
    expect(stageOptionProblems([{...point, branch: 'X'}, inBranch('A'), inBranch('B'), plain]).get(0)).toBe(BRANCH_POINT_OPTIONS_MESSAGE);
    expect(stageOptionProblems([inBranch('A'), plain]).get(0)).toBe(BRANCH_WITHOUT_POINT_MESSAGE);
  });

  it('keeps branch names short', () => {
    expect(stageOptionProblems([point, inBranch('x'.repeat(51)), inBranch('B'), plain]).get(1)).toBe(BRANCH_TOO_LONG_MESSAGE);
  });

  it('lists the branches after a branch point, as first written, and says which stages come after one', () => {
    const rows = [plain, point, inBranch(' Benign '), inBranch('Treatment'), inBranch('benign'), plain];
    expect(branchesAfter(rows, 1)).toEqual(['Benign', 'Treatment']);
    expect(isAfterBranchPoint(rows, 1)).toBe(false);
    expect(isAfterBranchPoint(rows, 2)).toBe(true);
  });

  it('sends options only when a stage has one, each trimmed, with no branch as none', () => {
    expect(stageOptionsToSend([plain, {...plain, branch: '  '}, plain])).toBeUndefined();
    expect(stageOptionsToSend([point, inBranch(' A '), inBranch('B', true), plain])).toEqual([
      {isOptional: false, choosesBranch: true, branch: null},
      {isOptional: false, choosesBranch: false, branch: 'A'},
      {isOptional: true, choosesBranch: false, branch: 'B'},
      {isOptional: false, choosesBranch: false, branch: null},
    ]);
  });
});
