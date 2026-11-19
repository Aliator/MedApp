import {chainLinks, hasParallelRoutes, NO_PREDECESSOR_MESSAGE, NO_SUCCESSOR_MESSAGE, routeDepths, templateGraphProblems, UNNAMED_BRANCH_MESSAGE} from './pathwayRoutes';

const links = (pairs: [number, number][]) => pairs.map(([fromStageOrder, toStageOrder]) => ({fromStageOrder, toStageOrder}));

describe('pathway routes', () => {
  it('links a list stage to stage, and a branch point to each branch, joined at the stage after them', () => {
    expect(chainLinks([{stageOrder: 1}, {stageOrder: 2}, {stageOrder: 3}])).toEqual(links([[1, 2], [2, 3]]));
    const branched = [
      {stageOrder: 1, choosesBranch: true},
      {stageOrder: 2, branch: 'Biopsy first'},
      {stageOrder: 3, branch: 'Biopsy first'},
      {stageOrder: 4, branch: 'Treat directly'},
      {stageOrder: 5},
    ];
    expect(chainLinks(branched)).toEqual(links([[1, 2], [2, 3], [1, 4], [3, 5], [4, 5]]));
  });

  it('says a template has routes side by side only when its links aren’t its list’s', () => {
    const stages = [{stageOrder: 1}, {stageOrder: 2}, {stageOrder: 3}, {stageOrder: 4}];
    expect(hasParallelRoutes({stages, links: chainLinks(stages)})).toBe(false);
    expect(hasParallelRoutes({stages, links: links([[1, 2], [1, 3], [2, 4], [3, 4]])})).toBe(true);
  });
});

describe('route depths and graph rules (MC9)', () => {
  it('indents the routes after a fork, and comes back out at the join', () => {
    expect(routeDepths(5, links([[1, 2], [1, 3], [2, 4], [3, 4], [4, 5]])).slice(1)).toEqual([0, 1, 1, 0, 0]);
  });

  it('needs one start, one end and named branches after a branch point', () => {
    const plain = {};
    const problems = templateGraphProblems([{choosesBranch: true}, {branch: 'A'}, plain, plain], links([[1, 2], [1, 3], [2, 4]]));
    expect(problems.get(2)).toBe(UNNAMED_BRANCH_MESSAGE);
    expect(templateGraphProblems([plain, plain, plain], links([[1, 3]])).get(1)).toBe(NO_PREDECESSOR_MESSAGE);
    expect(templateGraphProblems([plain, plain, plain], links([[1, 2], [1, 3]])).get(1)).toBe(NO_SUCCESSOR_MESSAGE);
  });
});
