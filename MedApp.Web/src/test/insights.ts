import type {PathwayInsights} from '../api/insights';

/** Synthetic data for Insights' tests. */
export const TEMPLATE_ID = '0000000e-0000-0000-0000-000000000001';
export const STAFF_ID = '0000000a-0000-0000-0000-000000000001';
export const TEAM_ID = '0000005a-0000-0000-0000-0000000000aa';
export const OTHER_TEAM_ID = '0000005a-0000-0000-0000-0000000000bb';

export const teams = [
  {id: OTHER_TEAM_ID, name: 'Allergy', isArchived: true},
  {id: TEAM_ID, name: 'Biomechanics', isArchived: false},
];

export const templates = [
  {id: TEMPLATE_ID, name: 'Knee review', isArchived: false, teamId: TEAM_ID, pathwayCount: 4, stages: [], stageDefinitionIdsInOrder: []},
  {
    id: '0000000e-0000-0000-0000-000000000002',
    name: 'Allergy check',
    isArchived: true,
    teamId: OTHER_TEAM_ID,
    pathwayCount: 1,
    stages: [],
    stageDefinitionIdsInOrder: [],
  },
];

export const insights: PathwayInsights = {
  weeks: 12,
  templateId: null,
  from: '2026-08-31',
  to: '2026-11-16',
  open: {current: 14, lastWeek: 12},
  inProgress: {current: 6, lastWeek: 6},
  overdue: {current: 5, lastWeek: 7},
  completedThisWeek: {current: 3, lastWeek: 1},
  overdueByAssignee: [
    {userId: STAFF_ID, username: 'k.ashford', count: 3},
    {userId: null, username: null, count: 2},
  ],
  overdueByPriority: [
    {priority: 'Urgent', count: 1},
    {priority: 'High', count: 2},
    {priority: 'Medium', count: 2},
    {priority: 'Low', count: 0},
  ],
  throughput: [
    {weekStart: '2026-11-02', created: 4, completed: 2},
    {weekStart: '2026-11-09', created: 3, completed: 5},
    {weekStart: '2026-11-16', created: 1, completed: 3},
  ],
  stageTimes: [
    {
      templateId: TEMPLATE_ID,
      templateName: 'Knee review',
      stages: [
        {stageOrder: 1, stageName: 'Consultation', finishedCount: 4, averageWaitingHours: 2.5, averageWorkingHours: 1},
        {stageOrder: 2, stageName: 'Procedure', finishedCount: 3, averageWaitingHours: 50, averageWorkingHours: 4},
      ],
    },
  ],
};
