import type {ReviewedTemplate} from './dermatologyReviewed';
import {PATIENTS} from './ids';

export const LOWER_LIMB_TEMPLATES: ReviewedTemplate[] = [
  {
    template: 'Lower limb biomechanics assessment',
    retentionClass: 'Standard',
    summary: 'Foot, ankle or knee pain from the way the leg moves: a gait assessment, then advice and exercises or custom orthotics, cast, made and fitted.',
    start: 'triage',
    stages: [
      {id: 'triage', name: 'Triage', kind: 'step', visit: 'none', next: ['biomechanics'], snomed: '225390008 | Triage', notes: 'Record the referral, where it hurts and what has been tried.'},
      {id: 'biomechanics', name: 'Biomechanical assessment', kind: 'assessment', visit: 'required', outcomes: [['Custom orthotics needed', ['casting']], ['Advice and exercises', ['discharge']]], notes: 'Gait analysis and a foot and leg examination, recorded as found.'},
      {id: 'casting', name: 'Orthotic casting', kind: 'step', visit: 'required', next: ['fitting'], notes: 'Foam box or scan of both feet for the orthotics lab.'},
      {id: 'fitting', name: 'Orthotic fitting', kind: 'step', visit: 'required', next: ['discharge'], notes: 'Fit the orthotics and explain how to wear them in.'},
      {id: 'discharge', name: 'Final letter to GP', kind: 'step', visit: 'none', next: [], endOutcome: 'discharge', needsLetter: 'Final letter to GP', snomed: '58000006 | Patient discharge', notes: 'What was found and what was supplied.'},
    ],
    addableRoutes: [],
  },
];

export interface DemoOrthotic {
  id: string;
  patientId: string;
  name: string;
  madeOn: string;
  status: 'In use' | 'Replaced' | 'Being made';
}

const orthotic = (n: number) => `000000da-0000-4000-8000-${n.toString(16).padStart(12, '0')}`;

export const ORTHOTICS: DemoOrthotic[] = [
  {id: orthotic(1), patientId: PATIENTS.joelBrannock, name: 'Full-length polypropylene, medial heel skive', madeOn: '2026-11-02', status: 'Being made'},
  {id: orthotic(2), patientId: PATIENTS.joelBrannock, name: 'Three-quarter EVA, 4° rearfoot post', madeOn: '2025-06-18', status: 'In use'},
  {id: orthotic(3), patientId: PATIENTS.joelBrannock, name: 'Sports insole, carbon fibre shell', madeOn: '2024-03-07', status: 'Replaced'},
  {id: orthotic(4), patientId: PATIENTS.joelBrannock, name: 'Full-length EVA, first ray cut-out', madeOn: '2022-09-21', status: 'Replaced'},
];

export const orthoticsFor = (patientId: string) => ORTHOTICS.filter((o) => o.patientId === patientId);
