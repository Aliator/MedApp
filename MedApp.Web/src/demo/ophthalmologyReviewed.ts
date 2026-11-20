import type {ReviewedTemplate} from './dermatologyReviewed';

/**
 * The Eye clinic's pathway templates: a TypeScript copy of `docs/pathways/ophthalmology.yaml` (the source of truth, a
 * draft not yet reviewed by a clinician), field for field, kept in step with it by hand, with the same defaults left
 * out as `dermatologyReviewed.ts`. The seed maps it onto the model (`reviewedTemplates` in `clinicTemplates.ts`).
 */
export const OPHTHALMOLOGY_REVIEWED: ReviewedTemplate[] = [
  {
    template: 'Cataract assessment',
    retentionClass: 'Standard',
    summary: 'Cataract referral from an optometrist: assessment and, when surgery is wanted and suitable, biometry before referral to the hospital for the operation.',
    start: 'triage',
    stages: [
      {id: 'triage', name: 'Triage', kind: 'step', visit: 'none', next: ['assessment'], snomed: '225390008 | Triage', notes: 'Referral from the optometrist: record the visual acuity given and the reason for referral.'},
      {id: 'assessment', name: 'Cataract assessment', kind: 'assessment', visit: 'required', outcomes: [['Wants surgery and suitable', ['biometry']], ['Not affecting daily life', ['discharge']], ['Other eye problem found', ['refer-hospital']]], notes: 'Slit lamp and dilated examination; talk through the benefits and risks of surgery. The patient decides whether they want it.'},
      {id: 'biometry', name: 'Biometry', kind: 'step', visit: 'required', next: ['refer-surgery'], notes: 'Measurements for the lens implant, taken as the hospital asks, recorded as measured.'},
      {id: 'refer-surgery', name: 'Refer for cataract surgery', kind: 'step', visit: 'none', next: [], endOutcome: 'hospitalReferral', needsLetter: 'Referral letter to hospital', snomed: '306206005 | Referral to service', notes: 'Referral for the operation, with the biometry and which eye.'},
      {id: 'discharge', name: 'Final letter to GP', kind: 'step', visit: 'none', next: [], endOutcome: 'discharge', needsLetter: 'Final letter to GP', snomed: '58000006 | Patient discharge', notes: 'Cataract not affecting daily life: advice to return through the optometrist if vision changes.'},
      {id: 'refer-hospital', name: 'Refer to hospital eye service', kind: 'step', visit: 'none', next: [], endOutcome: 'hospitalReferral', needsLetter: 'Referral letter to hospital', snomed: '306206005 | Referral to service', notes: 'Another eye problem found at the assessment, for the hospital eye service.'},
    ],
    addableRoutes: [],
  },
  {
    template: 'Glaucoma assessment',
    retentionClass: 'Standard',
    summary: 'Suspected glaucoma or raised eye pressure from an optometrist: assessment, then treatment or a monitoring plan, reviewed in clinic, ending back with the optometrist, discharged or at the hospital.',
    start: 'triage',
    stages: [
      {id: 'triage', name: 'Triage', kind: 'step', visit: 'none', next: ['glaucoma-assessment'], snomed: '225390008 | Triage', notes: 'Referral from the optometrist: record the pressures and findings they gave.'},
      {id: 'glaucoma-assessment', name: 'Glaucoma assessment', kind: 'assessment', visit: 'required', outcomes: [['No glaucoma', ['discharge']], ['Ocular hypertension', ['monitoring-plan']], ['Glaucoma', ['start-treatment']]], notes: "Eye pressure, visual fields and optic nerve imaging, recorded as measured. The diagnosis is the clinician's."},
      {id: 'start-treatment', name: 'Start eye drops', kind: 'step', visit: 'suggested', next: ['review'], notes: 'Drops chosen and explained by the clinician; the GP is asked to prescribe them.'},
      {id: 'monitoring-plan', name: 'Agree monitoring plan', kind: 'step', visit: 'suggested', next: ['review'], notes: 'How often pressures and fields are checked, and by whom.'},
      {id: 'review', name: 'Glaucoma review', kind: 'assessment', visit: 'required', outcomes: [['Stable', ['back-to-optometrist']], ['Not controlled', ['refer-hospital']]], notes: 'Pressures and fields again, compared with the first visit.'},
      {id: 'back-to-optometrist', name: 'Back to optometrist monitoring', kind: 'step', visit: 'none', next: [], endOutcome: 'monitoring', needsLetter: 'Final letter to GP', snomed: '58000006 | Patient discharge', notes: 'Stable: the optometrist continues the monitoring plan; the GP is told.'},
      {id: 'discharge', name: 'Final letter to GP', kind: 'step', visit: 'none', next: [], endOutcome: 'discharge', needsLetter: 'Final letter to GP', snomed: '58000006 | Patient discharge', notes: 'No glaucoma found: advice to keep up routine sight tests.'},
      {id: 'refer-hospital', name: 'Refer to hospital eye service', kind: 'step', visit: 'none', next: [], endOutcome: 'hospitalReferral', needsLetter: 'Referral letter to hospital', snomed: '306206005 | Referral to service', notes: 'Not controlled on treatment, for the hospital eye service.'},
    ],
    addableRoutes: [],
  },
  {
    template: 'Red eye assessment',
    retentionClass: 'Standard',
    summary: "A red or sore eye referred the same week: examination, an eye swab to the lab where it's needed, and treatment, discharge or urgent hospital referral.",
    start: 'triage',
    stages: [
      {id: 'triage', name: 'Triage', kind: 'step', visit: 'none', next: ['eye-exam'], snomed: '225390008 | Triage', notes: 'Same-week referral: record the symptoms, contact lens use and how long it has lasted. Sight-threatening signs go to the hospital the same day.'},
      {id: 'eye-exam', name: 'Eye examination', kind: 'assessment', visit: 'required', outcomes: [['Settling: advice', ['discharge']], ['Swab needed', ['take-swab']], ['Sight-threatening', ['refer-hospital-urgent']]], notes: 'Slit lamp examination, recorded as found.'},
      {id: 'take-swab', name: 'Take eye swab', kind: 'step', visit: 'required', next: ['microbiology'], notes: 'A swab of the eye, labelled with which eye, to the lab.'},
      {id: 'microbiology', name: 'Microbiology', owner: 'lab', kind: 'step', visit: 'none', next: ['microbiology-result'], notes: 'The lab cultures the swab and enters the report.'},
      {id: 'microbiology-result', name: 'Microbiology result', kind: 'labResult', visit: 'suggested', outcomes: [['No growth', ['discharge']], ['Bacterial infection', ['treatment-plan']], ['Unusual organism', ['refer-hospital-urgent']]], notes: 'The clinician records the result as the lab reported it.'},
      {id: 'treatment-plan', name: 'Agree treatment plan', kind: 'step', visit: 'suggested', next: ['discharge'], notes: 'Treatment chosen by the clinician; the GP is asked to prescribe it.'},
      {id: 'discharge', name: 'Final letter to GP', kind: 'step', visit: 'none', next: [], endOutcome: 'discharge', needsLetter: 'Final letter to GP', snomed: '58000006 | Patient discharge', notes: 'Settled or treated: advice on when to seek help again.'},
      {id: 'refer-hospital-urgent', name: 'Urgent hospital eye referral', kind: 'step', visit: 'none', next: [], endOutcome: 'hospitalReferral', needsLetter: 'Urgent referral letter to hospital', snomed: '306206005 | Referral to service', notes: 'Sight-threatening or an unusual organism: same-day referral to the hospital eye service.'},
    ],
    addableRoutes: [],
  },
  {
    template: 'Macular assessment',
    retentionClass: 'Standard',
    summary: 'A change in central vision referred by an optometrist: a macular scan, then advice, a routine or a fast-track hospital referral.',
    start: 'triage',
    stages: [
      {id: 'triage', name: 'Triage', kind: 'step', visit: 'none', next: ['oct'], snomed: '225390008 | Triage', notes: 'Referral from the optometrist: record the symptoms and when they started. Sudden distortion is seen fast.'},
      {id: 'oct', name: 'Macular OCT assessment', kind: 'assessment', visit: 'required', outcomes: [['Dry macular degeneration: advice', ['discharge']], ['Suspected wet macular degeneration', ['refer-hospital-urgent']], ['Diabetic macular change', ['refer-hospital']]], notes: "Scan of the macula and examination, recorded as found. The diagnosis is the clinician's."},
      {id: 'discharge', name: 'Final letter to GP', kind: 'step', visit: 'none', next: [], endOutcome: 'discharge', needsLetter: 'Final letter to GP', snomed: '58000006 | Patient discharge', notes: 'Dry macular degeneration: advice and an Amsler chart to watch for change.'},
      {id: 'refer-hospital', name: 'Refer to hospital eye service', kind: 'step', visit: 'none', next: [], endOutcome: 'hospitalReferral', needsLetter: 'Referral letter to hospital', snomed: '306206005 | Referral to service', notes: 'Diabetic macular change, for the hospital eye service.'},
      {id: 'refer-hospital-urgent', name: 'Urgent hospital eye referral', kind: 'step', visit: 'none', next: [], endOutcome: 'hospitalReferral', needsLetter: 'Urgent referral letter to hospital', snomed: '306206005 | Referral to service', notes: 'Suspected wet macular degeneration: fast-track referral for treatment.'},
    ],
    addableRoutes: [],
  },
];
