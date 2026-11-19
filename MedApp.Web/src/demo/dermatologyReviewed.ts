/**
 * The owner's reviewed Dermatology pathway templates: a TypeScript copy of `docs/pathways/dermatology.yaml` (the source
 * of truth), field for field, kept in step with it by hand. Defaults are left out: `owner` clinic, `optional`,
 * `needsConsent` and `mdt` false, the rest null. `outcomes` are `[name, next]`. The seed maps it onto the model
 * (`dermatologyTemplates` in `dermatology.ts`).
 */

export interface ReviewedStage {
  id: string;
  name: string;
  owner?: 'clinic' | 'lab';
  kind: 'step' | 'assessment' | 'labResult';
  visit: 'none' | 'suggested' | 'required';
  next?: string[];
  outcomes?: [string, string[]][];
  endOutcome?: 'discharge' | 'gpReferral' | 'hospitalReferral' | 'otherClinic' | 'monitoring';
  needsLetter?: string;
  repeat?: string;
  consult?: string;
  /** `code | term`. */
  snomed?: string;
  optional?: boolean;
  needsConsent?: boolean;
  mdt?: boolean;
  notes: string;
}

export interface ReviewedRoute {
  name: string;
  attachesAfter: string;
  reason: string;
  stages: ReviewedStage[];
}

export interface ReviewedTemplate {
  template: string;
  retentionClass: 'Standard' | 'Oncology';
  summary: string;
  start: string;
  stages: ReviewedStage[];
  addableRoutes: ReviewedRoute[];
}

export const DERMATOLOGY_REVIEWED: ReviewedTemplate[] = [
  {
    template: 'Suspected skin cancer',
    retentionClass: 'Oncology',
    summary: 'Urgent suspected skin cancer referral: dermoscopy, biopsy where the centre can take one, histology and the skin cancer MDT, ending in care completed or hospital treatment.',
    start: 'triage',
    stages: [
      {id: 'triage', name: 'Triage', kind: 'step', visit: 'none', next: ['dermoscopy'], snomed: '225390008 | Triage', notes: 'Urgent suspected cancer referral: record the referral date, lesion site, history and risk factors. Book within the urgent target.'},
      {id: 'dermoscopy', name: 'Dermoscopy assessment', kind: 'assessment', visit: 'required', outcomes: [['Looks benign', ['care-plan']], ['Actinic keratosis', ['refer-gp']], ['Suspected BCC', ['refer-hospital']], ['Suspected SCC', ['refer-hospital-urgent']], ['Suspected melanoma', ['refer-hospital-urgent']], ['Uncertain: biopsy', ['take-biopsy']]], notes: 'Full skin check, dermoscopy and clinical photographs; record lesion size and site.'},
      {id: 'take-biopsy', name: 'Take biopsy', kind: 'step', visit: 'required', next: ['histology'], snomed: '240977001 | Biopsy of skin', needsConsent: true, notes: 'Punch, shave or incisional biopsy. Never a partial biopsy of a possible melanoma: that is excised whole at the hospital.'},
      {id: 'histology', name: 'Histology', owner: 'lab', kind: 'step', visit: 'none', next: ['histology-result'], snomed: '252416005 | Histopathology test', notes: 'The lab processes the sample and enters the histology report.'},
      {id: 'histology-result', name: 'Histology result', kind: 'labResult', visit: 'suggested', outcomes: [['Benign', ['care-plan']], ['Actinic keratosis', ['refer-gp']], ["Bowen's disease", ['refer-hospital']], ['Low-risk BCC', ['refer-hospital']], ['High-risk BCC', ['mdt']], ['Squamous cell carcinoma', ['mdt']], ['Melanoma', ['mdt']], ['Not diagnostic', ['take-biopsy']]], notes: "The clinician records the histology diagnosis. BCC risk is the clinician's judgement from the report, recorded as given."},
      {id: 'mdt', name: 'Skin cancer MDT', kind: 'assessment', visit: 'none', outcomes: [['Hospital treatment', ['refer-hospital-urgent']], ['More tissue needed', ['take-biopsy']], ['Benign on review', ['care-plan']]], mdt: true, notes: "Local skin cancer MDT (dermatology and pathology). Record the team's decision."},
      {id: 'care-plan', name: 'Agree care plan', kind: 'step', visit: 'suggested', next: ['discharge'], notes: 'Self-examination and sun protection advice, with written information.'},
      {id: 'discharge', name: 'Final letter to GP', kind: 'step', visit: 'none', next: [], endOutcome: 'discharge', needsLetter: 'Final letter to GP', snomed: '58000006 | Patient discharge', notes: 'Histology, the care plan and self-examination advice in the letter.'},
      {id: 'refer-gp', name: 'Refer to GP', kind: 'step', visit: 'none', next: [], endOutcome: 'gpReferral', needsLetter: 'Referral letter to GP', snomed: '306206005 | Referral to service', notes: 'Actinic keratosis: topical treatment needs prescribing. Sun protection advice given; cryotherapy is not offered here.'},
      {id: 'refer-hospital', name: 'Refer to hospital', kind: 'step', visit: 'none', next: [], endOutcome: 'hospitalReferral', needsLetter: 'Referral letter to hospital', snomed: '306206005 | Referral to service', notes: "Routine referral for treatment: low-risk BCC or Bowen's disease (surgery, cryotherapy or photodynamic therapy)."},
      {id: 'refer-hospital-urgent', name: 'Urgent hospital referral', kind: 'step', visit: 'none', next: [], endOutcome: 'hospitalReferral', needsLetter: 'Urgent referral letter to hospital', snomed: '306206005 | Referral to service', notes: 'Urgent suspected cancer referral: excision biopsy of a suspected melanoma or SCC, or treatment as the MDT agreed.'},
    ],
    addableRoutes: [
      {name: 'Biopsy another lesion', attachesAfter: 'dermoscopy', reason: 'A second lesion found at the skin check also needs a biopsy.', stages: [
        {id: 'biopsy-another', name: 'Take biopsy', kind: 'step', visit: 'required', next: ['histology'], snomed: '240977001 | Biopsy of skin', needsConsent: true, notes: "Record the second lesion's site; it goes to the lab as its own sample."},
      ]},
    ],
  },
  {
    template: 'Changing mole or lesion check',
    retentionClass: 'Standard',
    summary: 'Routine check of a changing or uncertain mole: dermoscopy, then reassurance, photo monitoring with recall, or urgent referral for excision.',
    start: 'triage',
    stages: [
      {id: 'triage', name: 'Triage', kind: 'step', visit: 'none', next: ['dermoscopy'], snomed: '225390008 | Triage', notes: 'Routine referral: record the reported change, its duration and history.'},
      {id: 'dermoscopy', name: 'Dermoscopy assessment', kind: 'assessment', visit: 'required', outcomes: [['Looks benign', ['care-plan']], ['Uncertain: monitor', ['take-photos']], ['Suspected skin cancer', ['refer-hospital-urgent']]], notes: 'Dermoscopy of the mole and a skin check; record size and site.'},
      {id: 'take-photos', name: 'Take photographs', kind: 'step', visit: 'required', next: ['mole-review'], needsConsent: true, notes: 'Baseline clinical and dermoscopy photographs, for comparison at review.'},
      {id: 'mole-review', name: 'Mole review', kind: 'assessment', visit: 'required', outcomes: [['No change', ['care-plan']], ['Keep monitoring', ['mole-review']], ['Mole has changed', ['refer-hospital-urgent']]], repeat: 'Every 3 months (short-term) or 6 to 12 months (several atypical moles); stops when care is completed or at referral.', notes: 'Compare with the earlier photographs; record what changed, if anything.'},
      {id: 'care-plan', name: 'Agree care plan', kind: 'step', visit: 'suggested', next: ['discharge'], notes: 'Self-check advice (what change to look for) and sun protection.'},
      {id: 'discharge', name: 'Final letter to GP', kind: 'step', visit: 'none', next: [], endOutcome: 'discharge', needsLetter: 'Final letter to GP', snomed: '58000006 | Patient discharge', notes: 'Diagnosis, care plan and self-care advice in the letter.'},
      {id: 'refer-hospital-urgent', name: 'Urgent hospital referral', kind: 'step', visit: 'none', next: [], endOutcome: 'hospitalReferral', needsLetter: 'Urgent referral letter to hospital', snomed: '306206005 | Referral to service', notes: 'Urgent suspected cancer referral for excision biopsy at the hospital: a possible melanoma is removed whole, not biopsied here.'},
    ],
    addableRoutes: [],
  },
  {
    template: 'Skin cancer surveillance',
    retentionClass: 'Oncology',
    summary: "Regular skin checks after hospital treatment for skin cancer, or for people at high risk, following the hospital's follow-up plan.",
    start: 'triage',
    stages: [
      {id: 'triage', name: 'Triage', kind: 'step', visit: 'none', next: ['skin-check'], snomed: '225390008 | Triage', notes: "Record the hospital's diagnosis, treatment and follow-up plan, including its review interval and end date."},
      {id: 'skin-check', name: 'Skin check', kind: 'assessment', visit: 'required', outcomes: [['No new concerns', ['skin-check']], ['Plan complete', ['care-plan']], ['Sun damage found', ['gp-letter']], ['New lesion: biopsy', ['take-biopsy']], ['Suspected melanoma', ['refer-hospital-urgent']], ['Suspected recurrence', ['refer-hospital-urgent']]], repeat: "Every 3 to 12 months, as the hospital's plan sets; stops when the plan ends.", notes: "Scar, lymph nodes and a full skin check. Intervals follow the hospital's plan (for example NICE NG14 for melanoma), recorded as given."},
      {id: 'gp-letter', name: 'Letter to GP', kind: 'step', visit: 'none', next: ['skin-check'], needsLetter: 'Letter to GP', notes: 'Actinic keratoses for the GP to treat; surveillance carries on.'},
      {id: 'take-biopsy', name: 'Take biopsy', kind: 'step', visit: 'required', next: ['histology'], snomed: '240977001 | Biopsy of skin', needsConsent: true, notes: 'Punch or shave biopsy of a new non-pigmented lesion. A possible melanoma goes to the hospital for excision instead.'},
      {id: 'histology', name: 'Histology', owner: 'lab', kind: 'step', visit: 'none', next: ['histology-result'], snomed: '252416005 | Histopathology test', notes: 'The lab processes the sample and enters the histology report.'},
      {id: 'histology-result', name: 'Histology result', kind: 'labResult', visit: 'suggested', outcomes: [['Benign', ['skin-check']], ['Actinic keratosis', ['gp-letter']], ['Low-risk BCC', ['refer-hospital']], ['High-risk BCC', ['mdt']], ['Squamous cell carcinoma', ['mdt']], ['Melanoma', ['mdt']], ['Not diagnostic', ['take-biopsy']]], notes: 'The clinician records the histology diagnosis.'},
      {id: 'mdt', name: 'Skin cancer MDT', kind: 'assessment', visit: 'none', outcomes: [['Hospital treatment', ['refer-hospital-urgent']], ['More tissue needed', ['take-biopsy']], ['Benign on review', ['skin-check']]], mdt: true, notes: "Local skin cancer MDT (dermatology and pathology). Record the team's decision."},
      {id: 'care-plan', name: 'Agree care plan', kind: 'step', visit: 'suggested', next: ['discharge'], notes: 'Self-examination and sun protection advice; how to get back in quickly.'},
      {id: 'discharge', name: 'Final letter to GP', kind: 'step', visit: 'none', next: [], endOutcome: 'discharge', needsLetter: 'Final letter to GP', snomed: '58000006 | Patient discharge', notes: 'Surveillance history and self-examination advice in the letter.'},
      {id: 'refer-hospital', name: 'Refer to hospital', kind: 'step', visit: 'none', next: [], endOutcome: 'hospitalReferral', needsLetter: 'Referral letter to hospital', snomed: '306206005 | Referral to service', notes: 'Routine referral for treatment of a new low-risk BCC.'},
      {id: 'refer-hospital-urgent', name: 'Urgent hospital referral', kind: 'step', visit: 'none', next: [], endOutcome: 'hospitalReferral', needsLetter: 'Urgent referral letter to hospital', snomed: '306206005 | Referral to service', notes: 'Urgent referral back to the treating team: suspected melanoma or recurrence, or treatment as the MDT agreed.'},
    ],
    addableRoutes: [],
  },
  {
    template: 'Inflammatory skin assessment',
    retentionClass: 'Standard',
    summary: 'A rash or itch whose cause is unclear (not eczema, psoriasis or acne): assessment, blood tests or a biopsy where they help, then a care plan or referral.',
    start: 'triage',
    stages: [
      {id: 'triage', name: 'Triage', kind: 'step', visit: 'none', next: ['skin-assessment'], snomed: '225390008 | Triage', notes: 'Record symptoms, duration, sites, medicines and allergies as given, and what has been tried.'},
      {id: 'skin-assessment', name: 'Skin assessment', kind: 'assessment', visit: 'required', outcomes: [['Self-care is enough', ['care-plan']], ['Needs prescribed treatment', ['refer-gp']], ['Blood tests needed', ['take-bloods']], ['Biopsy needed', ['take-biopsy']], ['Needs specialist care', ['refer-hospital']], ['Needs same-day care', ['refer-hospital-urgent']]], notes: 'Examine the skin, record distribution and appearance; photographs if useful.'},
      {id: 'take-bloods', name: 'Take bloods', kind: 'step', visit: 'required', next: ['blood-tests'], snomed: '82078001 | Collection of blood specimen for laboratory', notes: 'Only tests that help the assessment, listed by the clinician (for itch: full blood count, ferritin, kidney, liver and thyroid tests).'},
      {id: 'blood-tests', name: 'Blood tests', owner: 'lab', kind: 'step', visit: 'none', next: ['blood-results'], notes: 'The lab runs the requested tests and enters the results.'},
      {id: 'blood-results', name: 'Blood results', kind: 'labResult', visit: 'suggested', outcomes: [['No cause found', ['care-plan']], ['Cause for GP to treat', ['refer-gp']], ['Needs specialist care', ['refer-hospital']], ['Biopsy needed', ['take-biopsy']]], notes: 'The clinician reads the results and records the outcome.'},
      {id: 'take-biopsy', name: 'Take biopsy', kind: 'step', visit: 'required', next: ['histology'], snomed: '240977001 | Biopsy of skin', needsConsent: true, notes: 'Punch biopsy for diagnosis; record the site and the question for the pathologist.'},
      {id: 'histology', name: 'Histology', owner: 'lab', kind: 'step', visit: 'none', next: ['histology-result'], snomed: '252416005 | Histopathology test', notes: 'The lab processes the sample and enters the histology report.'},
      {id: 'histology-result', name: 'Histology result', kind: 'labResult', visit: 'suggested', outcomes: [['Self-care is enough', ['care-plan']], ['Needs prescribed treatment', ['refer-gp']], ['Needs specialist care', ['refer-hospital']], ['Unexpected cancer', ['refer-hospital-urgent']], ['Not diagnostic', ['take-biopsy']]], notes: 'The clinician records the histology diagnosis and the outcome.'},
      {id: 'care-plan', name: 'Agree care plan', kind: 'step', visit: 'suggested', next: ['discharge'], notes: 'Advice only, no prescribing: emollient use, skin care, triggers, sun protection.'},
      {id: 'discharge', name: 'Final letter to GP', kind: 'step', visit: 'none', next: [], endOutcome: 'discharge', needsLetter: 'Final letter to GP', snomed: '58000006 | Patient discharge', notes: 'Diagnosis, care plan and self-care advice in the letter.'},
      {id: 'refer-gp', name: 'Refer to GP', kind: 'step', visit: 'none', next: [], endOutcome: 'gpReferral', needsLetter: 'Referral letter to GP', snomed: '306206005 | Referral to service', notes: 'Prescribing needed, or a cause found in the bloods for the GP to manage.'},
      {id: 'refer-hospital', name: 'Refer to hospital', kind: 'step', visit: 'none', next: [], endOutcome: 'hospitalReferral', needsLetter: 'Referral letter to hospital', snomed: '306206005 | Referral to service', notes: 'For example patch testing, a suspected blistering or connective tissue disease, phototherapy or systemic treatment.'},
      {id: 'refer-hospital-urgent', name: 'Urgent hospital referral', kind: 'step', visit: 'none', next: [], endOutcome: 'hospitalReferral', needsLetter: 'Urgent referral letter to hospital', snomed: '306206005 | Referral to service', notes: 'Phone the on-call hospital team the same day; the letter follows. For example widespread blistering or red skin all over; or an unexpected cancer on histology.'},
    ],
    addableRoutes: [],
  },
  {
    template: 'Eczema assessment',
    retentionClass: 'Standard',
    summary: 'Assessment of eczema and dermatitis: self-care education with a review, or referral for prescribed treatment, patch testing or specialist care.',
    start: 'triage',
    stages: [
      {id: 'triage', name: 'Triage', kind: 'step', visit: 'none', next: ['eczema-assessment'], snomed: '225390008 | Triage', notes: 'Record history, sites, triggers, what has been tried, and allergies as given.'},
      {id: 'eczema-assessment', name: 'Eczema assessment', kind: 'assessment', visit: 'required', outcomes: [['Self-care is enough', ['care-plan']], ['Needs prescribed treatment', ['refer-gp']], ['Possible contact allergy', ['refer-hospital']], ['Severe or not controlled', ['refer-hospital']], ['Needs same-day care', ['refer-hospital-urgent']], ['Diagnosis unclear', ['take-biopsy']]], notes: 'Record sites and appearance; severity measures (EASI, POEM, DLQI) as given, never calculated by MedApp.'},
      {id: 'care-plan', name: 'Agree care plan', kind: 'step', visit: 'suggested', next: ['eczema-review'], notes: 'Advice only, no prescribing: emollient use, skin care, triggers, sun protection. Show how to apply emollients.'},
      {id: 'eczema-review', name: 'Eczema review', kind: 'assessment', visit: 'required', outcomes: [['Improved', ['discharge']], ['Needs prescribed treatment', ['refer-gp']], ['Severe or not controlled', ['refer-hospital']]], notes: 'One review about 6 to 8 weeks after the care plan.'},
      {id: 'take-biopsy', name: 'Take biopsy', kind: 'step', visit: 'required', next: ['histology'], snomed: '240977001 | Biopsy of skin', needsConsent: true, notes: 'Punch biopsy where the diagnosis is uncertain (for example to rule out another condition in an adult).'},
      {id: 'histology', name: 'Histology', owner: 'lab', kind: 'step', visit: 'none', next: ['histology-result'], snomed: '252416005 | Histopathology test', notes: 'The lab processes the sample and enters the histology report.'},
      {id: 'histology-result', name: 'Histology result', kind: 'labResult', visit: 'suggested', outcomes: [['Eczema confirmed', ['care-plan']], ['Other condition', ['refer-hospital']], ['Not diagnostic', ['take-biopsy']]], notes: 'The clinician records the histology diagnosis.'},
      {id: 'discharge', name: 'Final letter to GP', kind: 'step', visit: 'none', next: [], endOutcome: 'discharge', needsLetter: 'Final letter to GP', snomed: '58000006 | Patient discharge', notes: 'Diagnosis, care plan and self-care advice in the letter.'},
      {id: 'refer-gp', name: 'Refer to GP', kind: 'step', visit: 'none', next: [], endOutcome: 'gpReferral', needsLetter: 'Referral letter to GP', snomed: '306206005 | Referral to service', notes: 'Prescribing needed (for example topical steroids, or antibiotics for infected eczema).'},
      {id: 'refer-hospital', name: 'Refer to hospital', kind: 'step', visit: 'none', next: [], endOutcome: 'hospitalReferral', needsLetter: 'Referral letter to hospital', snomed: '306206005 | Referral to service', notes: 'Patch testing for a possible contact allergy, or specialist care for severe or uncontrolled eczema (phototherapy, systemic or biologic treatment).'},
      {id: 'refer-hospital-urgent', name: 'Urgent hospital referral', kind: 'step', visit: 'none', next: [], endOutcome: 'hospitalReferral', needsLetter: 'Urgent referral letter to hospital', snomed: '306206005 | Referral to service', notes: 'Phone the on-call hospital team the same day; the letter follows. For example suspected eczema herpeticum.'},
    ],
    addableRoutes: [],
  },
  {
    template: 'Psoriasis assessment',
    retentionClass: 'Standard',
    summary: 'Assessment of psoriasis and its impact: self-care with a review, or referral for prescribed, specialist or joint care.',
    start: 'triage',
    stages: [
      {id: 'triage', name: 'Triage', kind: 'step', visit: 'none', next: ['psoriasis-assessment'], snomed: '225390008 | Triage', notes: 'Record history, sites, nails, joints and what has been tried.'},
      {id: 'psoriasis-assessment', name: 'Psoriasis assessment', kind: 'assessment', visit: 'required', outcomes: [['Self-care is enough', ['care-plan']], ['Needs prescribed treatment', ['refer-gp']], ['Severe or not controlled', ['refer-hospital']], ['Joint symptoms', ['refer-hospital']], ['Needs same-day care', ['refer-hospital-urgent']], ['Diagnosis unclear', ['take-biopsy']]], notes: 'Record sites, nails and joint symptoms; PASI, BSA, DLQI and PEST as given, never calculated by MedApp.'},
      {id: 'care-plan', name: 'Agree care plan', kind: 'step', visit: 'suggested', next: ['psoriasis-review'], notes: 'Advice only, no prescribing: emollient use, skin care, triggers, sun protection. Lifestyle advice (smoking, alcohol, weight).'},
      {id: 'psoriasis-review', name: 'Psoriasis review', kind: 'assessment', visit: 'required', outcomes: [['Improved', ['discharge']], ['Needs prescribed treatment', ['refer-gp']], ['Severe or not controlled', ['refer-hospital']]], notes: 'One review about 8 to 12 weeks after the care plan.'},
      {id: 'take-biopsy', name: 'Take biopsy', kind: 'step', visit: 'required', next: ['histology'], snomed: '240977001 | Biopsy of skin', needsConsent: true, notes: 'Punch biopsy where the diagnosis is uncertain.'},
      {id: 'histology', name: 'Histology', owner: 'lab', kind: 'step', visit: 'none', next: ['histology-result'], snomed: '252416005 | Histopathology test', notes: 'The lab processes the sample and enters the histology report.'},
      {id: 'histology-result', name: 'Histology result', kind: 'labResult', visit: 'suggested', outcomes: [['Psoriasis confirmed', ['care-plan']], ['Other condition', ['refer-hospital']], ['Not diagnostic', ['take-biopsy']]], notes: 'The clinician records the histology diagnosis.'},
      {id: 'discharge', name: 'Final letter to GP', kind: 'step', visit: 'none', next: [], endOutcome: 'discharge', needsLetter: 'Final letter to GP', snomed: '58000006 | Patient discharge', notes: 'Diagnosis, care plan and self-care advice in the letter.'},
      {id: 'refer-gp', name: 'Refer to GP', kind: 'step', visit: 'none', next: [], endOutcome: 'gpReferral', needsLetter: 'Referral letter to GP', snomed: '306206005 | Referral to service', notes: 'Prescribing needed (topical treatment); the GP offers the annual review.'},
      {id: 'refer-hospital', name: 'Refer to hospital', kind: 'step', visit: 'none', next: [], endOutcome: 'hospitalReferral', needsLetter: 'Referral letter to hospital', snomed: '306206005 | Referral to service', notes: 'Specialist dermatology for phototherapy or systemic or biologic treatment, or rheumatology for possible psoriatic arthritis.'},
      {id: 'refer-hospital-urgent', name: 'Urgent hospital referral', kind: 'step', visit: 'none', next: [], endOutcome: 'hospitalReferral', needsLetter: 'Urgent referral letter to hospital', snomed: '306206005 | Referral to service', notes: 'Phone the on-call hospital team the same day; the letter follows. For example red skin all over or widespread pustules.'},
    ],
    addableRoutes: [],
  },
  {
    template: 'Acne assessment',
    retentionClass: 'Standard',
    summary: 'Assessment of acne, scarring and its impact: skin care advice with a review, or referral for prescribed treatment, specialist care or psychological support.',
    start: 'triage',
    stages: [
      {id: 'triage', name: 'Triage', kind: 'step', visit: 'none', next: ['acne-assessment'], snomed: '225390008 | Triage', notes: 'Record onset, sites, what has been tried and the effect on daily life.'},
      {id: 'acne-assessment', name: 'Acne assessment', kind: 'assessment', visit: 'required', outcomes: [['Self-care is enough', ['care-plan']], ['Needs prescribed treatment', ['refer-gp']], ['Severe or scarring', ['refer-hospital']], ['Needs psychological support', ['refer-psychology']], ['Needs same-day care', ['refer-hospital-urgent']]], notes: 'Record lesion types, sites and scarring; photographs with consent. Hormonal features go to the GP to investigate.'},
      {id: 'care-plan', name: 'Agree care plan', kind: 'step', visit: 'suggested', next: ['acne-review'], notes: 'Skin care advice: gentle cleansing, non-greasy products, no picking. Advice only, no prescribing.'},
      {id: 'acne-review', name: 'Acne review', kind: 'assessment', visit: 'required', outcomes: [['Improved', ['discharge']], ['Needs prescribed treatment', ['refer-gp']], ['Severe or scarring', ['refer-hospital']]], notes: 'One review about 12 weeks after the care plan.'},
      {id: 'discharge', name: 'Final letter to GP', kind: 'step', visit: 'none', next: [], endOutcome: 'discharge', needsLetter: 'Final letter to GP', snomed: '58000006 | Patient discharge', notes: 'Diagnosis, care plan and self-care advice in the letter.'},
      {id: 'refer-gp', name: 'Refer to GP', kind: 'step', visit: 'none', next: [], endOutcome: 'gpReferral', needsLetter: 'Referral letter to GP', snomed: '306206005 | Referral to service', notes: 'Prescribing needed, or hormonal features for the GP to investigate.'},
      {id: 'refer-hospital', name: 'Refer to hospital', kind: 'step', visit: 'none', next: [], endOutcome: 'hospitalReferral', needsLetter: 'Referral letter to hospital', snomed: '306206005 | Referral to service', notes: 'Consultant-led dermatology: severe acne, scarring, or treatment only they can start.'},
      {id: 'refer-psychology', name: 'Refer to psychology', kind: 'step', visit: 'none', next: [], endOutcome: 'otherClinic', needsLetter: 'Referral letter to psychology', snomed: '306206005 | Referral to service', notes: 'Persistent distress, low mood or body image concerns.'},
      {id: 'refer-hospital-urgent', name: 'Urgent hospital referral', kind: 'step', visit: 'none', next: [], endOutcome: 'hospitalReferral', needsLetter: 'Urgent referral letter to hospital', snomed: '306206005 | Referral to service', notes: 'Phone the on-call hospital team the same day; the letter follows. For example suspected acne fulminans.'},
    ],
    addableRoutes: [],
  },
  {
    template: 'Suspected skin infection',
    retentionClass: 'Standard',
    summary: 'Assessment of a suspected fungal, bacterial or viral skin or nail infection, with a sample for the lab where it changes management.',
    start: 'triage',
    stages: [
      {id: 'triage', name: 'Triage', kind: 'step', visit: 'none', next: ['infection-assessment'], snomed: '225390008 | Triage', notes: 'Record symptoms, duration, sites and what has been tried. Spreading redness with fever is for same-day care, not triage.'},
      {id: 'infection-assessment', name: 'Infection assessment', kind: 'assessment', visit: 'required', outcomes: [['Sample needed', ['take-sample']], ['Treat without tests', ['refer-gp']], ['Not an infection', ['care-plan']], ['Needs same-day care', ['refer-hospital-urgent']]], notes: 'Record appearance, sites and the suspected infection.'},
      {id: 'take-sample', name: 'Take skin sample', kind: 'step', visit: 'required', next: ['microbiology'], notes: 'Skin scrapings or nail clippings for fungus, a swab for bacteria, or a viral swab. Record the sample type and site.'},
      {id: 'microbiology', name: 'Microbiology', owner: 'lab', kind: 'step', visit: 'none', next: ['microbiology-result'], notes: 'Microscopy first, then culture; fungal culture can take 2 to 4 weeks.'},
      {id: 'microbiology-result', name: 'Microbiology result', kind: 'labResult', visit: 'suggested', outcomes: [['Fungal infection', ['refer-gp']], ['Bacterial infection', ['refer-gp']], ['Viral infection', ['refer-gp']], ['No infection found', ['care-plan']], ['Sample not adequate', ['take-sample']]], notes: 'The clinician reads the report and records the outcome.'},
      {id: 'care-plan', name: 'Agree care plan', kind: 'step', visit: 'suggested', next: ['discharge'], notes: 'Skin care and hygiene advice. Advice only, no prescribing.'},
      {id: 'discharge', name: 'Final letter to GP', kind: 'step', visit: 'none', next: [], endOutcome: 'discharge', needsLetter: 'Final letter to GP', snomed: '58000006 | Patient discharge', notes: 'Diagnosis, care plan and self-care advice in the letter.'},
      {id: 'refer-gp', name: 'Refer to GP', kind: 'step', visit: 'none', next: [], endOutcome: 'gpReferral', needsLetter: 'Referral letter to GP', snomed: '306206005 | Referral to service', notes: 'Treatment needs prescribing; include the organism as reported.'},
      {id: 'refer-hospital-urgent', name: 'Urgent hospital referral', kind: 'step', visit: 'none', next: [], endOutcome: 'hospitalReferral', needsLetter: 'Urgent referral letter to hospital', snomed: '306206005 | Referral to service', notes: 'Phone the on-call hospital team the same day; the letter follows. For example spreading cellulitis with fever, or infection near the eye.'},
    ],
    addableRoutes: [],
  },
  {
    template: 'Hair loss assessment',
    retentionClass: 'Standard',
    summary: 'Assessment of hair loss and scalp conditions, with blood tests, a scalp sample or a scalp biopsy where they help, then a care plan or referral.',
    start: 'triage',
    stages: [
      {id: 'triage', name: 'Triage', kind: 'step', visit: 'none', next: ['hair-assessment'], snomed: '225390008 | Triage', notes: 'Record onset, pattern, recent illness or stress, medicines as given and family history.'},
      {id: 'hair-assessment', name: 'Hair assessment', kind: 'assessment', visit: 'required', outcomes: [['Watch and review', ['care-plan']], ['Blood tests needed', ['take-bloods']], ['Scalp sample needed', ['take-sample']], ['Possible scarring loss', ['take-biopsy']], ['Needs prescribed treatment', ['refer-gp']], ['Needs psychological support', ['refer-psychology']]], notes: 'Examine the scalp and hair (dermoscopy, pull test); scalp photographs with consent.'},
      {id: 'take-bloods', name: 'Take bloods', kind: 'step', visit: 'required', next: ['blood-tests'], snomed: '82078001 | Collection of blood specimen for laboratory', notes: 'Only tests that help, listed by the clinician (for example full blood count, ferritin and thyroid function for diffuse shedding).'},
      {id: 'blood-tests', name: 'Blood tests', owner: 'lab', kind: 'step', visit: 'none', next: ['blood-results'], notes: 'The lab runs the requested tests and enters the results.'},
      {id: 'blood-results', name: 'Blood results', kind: 'labResult', visit: 'suggested', outcomes: [['No cause found', ['care-plan']], ['Cause for GP to treat', ['refer-gp']], ['Needs specialist care', ['refer-hospital']]], notes: 'The clinician reads the results and records the outcome.'},
      {id: 'take-sample', name: 'Take skin sample', kind: 'step', visit: 'required', next: ['microbiology'], notes: 'Scalp scrapings and plucked hairs for suspected scalp ringworm.'},
      {id: 'microbiology', name: 'Microbiology', owner: 'lab', kind: 'step', visit: 'none', next: ['microbiology-result'], notes: 'Microscopy first, then culture; fungal culture can take 2 to 4 weeks.'},
      {id: 'microbiology-result', name: 'Microbiology result', kind: 'labResult', visit: 'suggested', outcomes: [['Fungal infection', ['refer-gp']], ['No infection found', ['care-plan']], ['Sample not adequate', ['take-sample']]], notes: 'The clinician reads the report and records the outcome.'},
      {id: 'take-biopsy', name: 'Take biopsy', kind: 'step', visit: 'required', next: ['histology'], snomed: '240977001 | Biopsy of skin', needsConsent: true, notes: 'Scalp punch biopsy from an active edge; record the sites.'},
      {id: 'histology', name: 'Histology', owner: 'lab', kind: 'step', visit: 'none', next: ['histology-result'], snomed: '252416005 | Histopathology test', notes: 'The lab processes the sample and enters the histology report.'},
      {id: 'histology-result', name: 'Histology result', kind: 'labResult', visit: 'suggested', outcomes: [['Scarring alopecia', ['refer-hospital']], ['Non-scarring alopecia', ['care-plan']], ['Not diagnostic', ['take-biopsy']]], notes: 'The clinician records the histology diagnosis.'},
      {id: 'care-plan', name: 'Agree care plan', kind: 'step', visit: 'suggested', next: ['hair-review'], notes: 'Advice on what to expect, hair care, wigs and camouflage. Advice only, no prescribing.'},
      {id: 'hair-review', name: 'Hair review', kind: 'assessment', visit: 'required', outcomes: [['Regrowing or stable', ['discharge']], ['No change: keep watching', ['hair-review']], ['Needs prescribed treatment', ['refer-gp']], ['Needs specialist care', ['refer-hospital']]], repeat: 'Every 3 to 6 months; stops when care is completed or at referral.', notes: 'Compare with earlier scalp photographs.'},
      {id: 'discharge', name: 'Final letter to GP', kind: 'step', visit: 'none', next: [], endOutcome: 'discharge', needsLetter: 'Final letter to GP', snomed: '58000006 | Patient discharge', notes: 'Diagnosis, care plan and self-care advice in the letter.'},
      {id: 'refer-gp', name: 'Refer to GP', kind: 'step', visit: 'none', next: [], endOutcome: 'gpReferral', needsLetter: 'Referral letter to GP', snomed: '306206005 | Referral to service', notes: 'Prescribing needed, or a cause found in the bloods for the GP to manage.'},
      {id: 'refer-hospital', name: 'Refer to hospital', kind: 'step', visit: 'none', next: [], endOutcome: 'hospitalReferral', needsLetter: 'Referral letter to hospital', snomed: '306206005 | Referral to service', notes: 'Specialist hair clinic: scarring alopecia, or extensive or fast hair loss.'},
      {id: 'refer-psychology', name: 'Refer to psychology', kind: 'step', visit: 'none', next: [], endOutcome: 'otherClinic', needsLetter: 'Referral letter to psychology', snomed: '306206005 | Referral to service', notes: 'Distress about hair loss affecting daily life.'},
    ],
    addableRoutes: [],
  },
  {
    template: 'Skin pigmentation assessment',
    retentionClass: 'Standard',
    summary: 'Assessment of patchy loss or gain of skin colour (such as vitiligo or melasma), not moles: advice, tests where they help, or referral.',
    start: 'triage',
    stages: [
      {id: 'triage', name: 'Triage', kind: 'step', visit: 'none', next: ['pigmentation-assessment'], snomed: '225390008 | Triage', notes: 'Record onset, spread, triggers and family history. A changing mole goes on its own pathway.'},
      {id: 'pigmentation-assessment', name: 'Pigmentation assessment', kind: 'assessment', visit: 'required', outcomes: [['Self-care is enough', ['care-plan']], ['Blood tests needed', ['take-bloods']], ['Diagnosis unclear', ['take-biopsy']], ['Needs prescribed treatment', ['refer-gp']], ['Needs specialist care', ['refer-hospital']], ['Needs psychological support', ['refer-psychology']]], notes: "Examine the skin (Wood's lamp if available); photographs with consent."},
      {id: 'take-bloods', name: 'Take bloods', kind: 'step', visit: 'required', next: ['blood-tests'], snomed: '82078001 | Collection of blood specimen for laboratory', notes: 'For example thyroid function in vitiligo, as the clinician lists.'},
      {id: 'blood-tests', name: 'Blood tests', owner: 'lab', kind: 'step', visit: 'none', next: ['blood-results'], notes: 'The lab runs the requested tests and enters the results.'},
      {id: 'blood-results', name: 'Blood results', kind: 'labResult', visit: 'suggested', outcomes: [['No cause found', ['care-plan']], ['Cause for GP to treat', ['refer-gp']]], notes: 'The clinician reads the results and records the outcome.'},
      {id: 'take-biopsy', name: 'Take biopsy', kind: 'step', visit: 'required', next: ['histology'], snomed: '240977001 | Biopsy of skin', needsConsent: true, notes: 'Punch biopsy where the diagnosis is uncertain.'},
      {id: 'histology', name: 'Histology', owner: 'lab', kind: 'step', visit: 'none', next: ['histology-result'], snomed: '252416005 | Histopathology test', notes: 'The lab processes the sample and enters the histology report.'},
      {id: 'histology-result', name: 'Histology result', kind: 'labResult', visit: 'suggested', outcomes: [['Self-care is enough', ['care-plan']], ['Needs prescribed treatment', ['refer-gp']], ['Needs specialist care', ['refer-hospital']], ['Unexpected cancer', ['refer-hospital-urgent']], ['Not diagnostic', ['take-biopsy']]], notes: 'The clinician records the histology diagnosis and the outcome.'},
      {id: 'care-plan', name: 'Agree care plan', kind: 'step', visit: 'suggested', next: ['discharge'], notes: 'Sun protection, skin camouflage advice and support groups. Advice only, no prescribing.'},
      {id: 'discharge', name: 'Final letter to GP', kind: 'step', visit: 'none', next: [], endOutcome: 'discharge', needsLetter: 'Final letter to GP', snomed: '58000006 | Patient discharge', notes: 'Diagnosis, care plan and self-care advice in the letter.'},
      {id: 'refer-gp', name: 'Refer to GP', kind: 'step', visit: 'none', next: [], endOutcome: 'gpReferral', needsLetter: 'Referral letter to GP', snomed: '306206005 | Referral to service', notes: 'Prescribing needed, or a cause found in the bloods for the GP to manage.'},
      {id: 'refer-hospital', name: 'Refer to hospital', kind: 'step', visit: 'none', next: [], endOutcome: 'hospitalReferral', needsLetter: 'Referral letter to hospital', snomed: '306206005 | Referral to service', notes: 'Specialist dermatology, for example phototherapy for vitiligo.'},
      {id: 'refer-psychology', name: 'Refer to psychology', kind: 'step', visit: 'none', next: [], endOutcome: 'otherClinic', needsLetter: 'Referral letter to psychology', snomed: '306206005 | Referral to service', notes: 'Distress about appearance affecting daily life.'},
      {id: 'refer-hospital-urgent', name: 'Urgent hospital referral', kind: 'step', visit: 'none', next: [], endOutcome: 'hospitalReferral', needsLetter: 'Urgent referral letter to hospital', snomed: '306206005 | Referral to service', notes: 'Urgent suspected cancer referral after an unexpected cancer on histology.'},
    ],
    addableRoutes: [],
  },
];

/** The reviewed file's gaps: what these templates need that the model doesn't have yet (`docs/todo.md`). */
export const DERMATOLOGY_GAPS: {group: string; items: string[]}[] = [
  {group: 'Lesions and photographs', items: [
    'Lesion register: several lesions per patient, each with a body-map site and its own photos, samples and results (skin cancer, mole check, surveillance).',
    'Clinical and dermoscopy photographs stored with consent and compared side by side over time (mole check, hair loss, acne, pigmentation).',
  ]},
  {group: 'Samples and results', items: [
    'Several samples on one pathway, each tracked through the lab on its own (two biopsies, or bloods and a biopsy taken at one visit).',
    'Structured histology fields recorded as the pathologist gives them (tumour type, Breslow thickness, margins), never calculated.',
    'Microbiology reports in two parts (microscopy now, culture weeks later), with organism and sensitivities as reported.',
  ]},
  {group: 'Cancer pathways', items: [
    'Urgent suspected cancer clocks (28-day Faster Diagnosis Standard, 62-day treatment target), time-based only.',
    'MDT list: cases queued for the next meeting, with attendees and the decision recorded.',
    'Oncology retention set by diagnosis, so a cancer found on a Standard template (inflammatory, pigmentation) is kept as long as a cancer record.',
    'Start a linked pathway in the same clinic (for example mole check to suspected skin cancer), keeping the history.',
  ]},
  {group: 'Recording without deciding', items: [
    'Severity measures recorded as given and shown over time (PASI, BSA, DLQI, EASI, POEM, PEST), never calculated or scored by MedApp.',
    'Current medicines and allergies recorded as given by the GP, without prescribing.',
    'A condition code for the template itself (for example 9014002 | Psoriasis, 43116000 | Eczema, 88616000 | Acne vulgaris, 56727007 | Vitiligo), as stage codes describe the activity.',
  ]},
  {group: 'Recall and referrals', items: [
    "Recall driven by a stage's repeat rule (interval and stop date, often from the hospital's plan), with follow-up when a patient does not attend.",
    'Same-day hospital referral by phone, recorded with its time, before the letter.',
  ]},
];
