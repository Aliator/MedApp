import {
  chainLinks,
  grownLinks,
  grownStageOrders,
  hasRoutes,
  NO_OUTCOMES_MESSAGE,
  NO_PREDECESSOR_MESSAGE,
  routeDepths,
  templateGraphProblems,
  UNNAMED_OUTCOME_MESSAGE,
} from './pathwayRoutes';

const links = (pairs: [number, number][]) => pairs.map(([fromStageOrder, toStageOrder]) => ({fromStageOrder, toStageOrder}));

describe('pathway routes', () => {
  it('links a list stage to stage', () => {
    expect(chainLinks([{stageOrder: 1}, {stageOrder: 2}, {stageOrder: 3}])).toEqual(links([[1, 2], [2, 3]]));
  });

  it('says a template has routes when its links aren’t its list’s, or it has a choice node', () => {
    const stages = [{stageOrder: 1}, {stageOrder: 2}, {stageOrder: 3}, {stageOrder: 4}];
    expect(hasRoutes({stages, links: chainLinks(stages)})).toBe(false);
    expect(hasRoutes({stages, links: links([[1, 2], [1, 3], [2, 4], [3, 4]])})).toBe(true);
    expect(hasRoutes({stages: [{stageOrder: 1, choiceKind: 'Assessment'}, {stageOrder: 2}], links: links([[1, 2]])})).toBe(true);
  });
});

describe('choice nodes: a pathway grows (the owner’s model)', () => {
  // 1 Visit, 2 Biopsy, 3 Histology, 4 Result (lab result: Benign → 5, BCC → 7, Melanoma → 8), 5 Care plan, 6 Discharge,
  // 7 Photographs, 8 Referral (shared by BCC and Melanoma).
  const stages = [
    {stageOrder: 1},
    {stageOrder: 2},
    {stageOrder: 3},
    {
      stageOrder: 4,
      choiceKind: 'LabResult',
      outcomes: [
        {name: 'Benign', nextStageOrders: [5]},
        {name: 'Basal cell carcinoma', nextStageOrders: [7]},
        {name: 'Melanoma', nextStageOrders: [8]},
      ],
    },
    {stageOrder: 5},
    {stageOrder: 6},
    {stageOrder: 7},
    {stageOrder: 8},
  ];
  const tree = links([[1, 2], [2, 3], [3, 4], [4, 5], [5, 6], [4, 7], [7, 8], [4, 8]]);

  it('holds the stages up to the first choice node, before any outcome is recorded', () => {
    expect(grownStageOrders(stages, tree, new Map())).toEqual([1, 2, 3, 4]);
  });

  it('adds only the recorded outcome’s stages, never another outcome’s', () => {
    expect(grownStageOrders(stages, tree, new Map([[4, 'Benign']]))).toEqual([1, 2, 3, 4, 5, 6]);
    expect(grownStageOrders(stages, tree, new Map([[4, 'Basal cell carcinoma']]))).toEqual([1, 2, 3, 4, 7, 8]);
    // The shared referral is reached straight from the result: the photographs' link into it isn't there.
    const melanoma = new Map([[4, 'Melanoma']]);
    expect(grownStageOrders(stages, tree, melanoma)).toEqual([1, 2, 3, 4, 8]);
    expect(grownLinks(stages, tree, [1, 2, 3, 4, 8], melanoma)).toEqual(links([[1, 2], [2, 3], [3, 4], [4, 8]]));
  });

  it('keeps a join waiting while a choice node that may lead into it isn’t recorded', () => {
    // A fork (2 and 3), where 2 is a choice node whose Benign leads to the join 4; 3 leads to it too.
    const forked = [
      {stageOrder: 1},
      {stageOrder: 2, choiceKind: 'LabResult', outcomes: [{name: 'Benign', nextStageOrders: [4]}, {name: 'Malignant', nextStageOrders: [5]}]},
      {stageOrder: 3},
      {stageOrder: 4},
      {stageOrder: 5},
    ];
    const forkLinks = links([[1, 2], [1, 3], [2, 4], [3, 4], [2, 5]]);
    expect(grownStageOrders(forked, forkLinks, new Map())).toEqual([1, 2, 3]);
    expect(grownStageOrders(forked, forkLinks, new Map([[2, 'Benign']]))).toEqual([1, 2, 3, 4]);
  });
});

describe('route depths and graph rules (MC9)', () => {
  it('indents the routes after a fork, and comes back out at the join', () => {
    expect(routeDepths(5, links([[1, 2], [1, 3], [2, 4], [3, 4], [4, 5]])).slice(1)).toEqual([0, 1, 1, 0, 0]);
  });

  it('needs one start, and a choice node with two named outcomes leading to every stage after it', () => {
    const plain = {};
    const choice = {choiceKind: 'Assessment', outcomes: [{name: 'A', nextStageOrders: [2]}]};
    const problems = templateGraphProblems([choice, plain, plain, plain], links([[1, 2], [1, 3], [2, 4]]));
    expect(problems.get(2)).toBe(UNNAMED_OUTCOME_MESSAGE);
    expect(problems.get(0)).toBe(NO_OUTCOMES_MESSAGE);
    // An outcome that ends the route counts: a discharge recorded at the choice node.
    const ending = {...choice, outcomes: [...choice.outcomes, {name: 'Discharge', nextStageOrders: []}]};
    expect(templateGraphProblems([ending, plain, plain], links([[1, 2], [2, 3]])).size).toBe(0);
    expect(templateGraphProblems([plain, plain, plain], links([[1, 3]])).get(1)).toBe(NO_PREDECESSOR_MESSAGE);
    // Routes may end anywhere: a referral, a discharge.
    expect(templateGraphProblems([plain, plain, plain], links([[1, 2], [1, 3]])).size).toBe(0);
  });
});
