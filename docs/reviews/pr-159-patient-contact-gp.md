# Review: PR #159, patient contact details, GP practices and staff names

**PR:** [#159](https://github.com/Aliator/MedApp/pull/159), branch `feature/patient-contact-gp`, reviewed at `1f32ea1` on 18 November 2026. A quick privacy review of the patient and staff handlers, anonymiser, export, FHIR mapper, practices controller and migration; nothing was run. CI (full tier) passed: https://github.com/Aliator/MedApp/actions/runs/44327663948. No finding is high or critical.

Adds optional patient address, postcode, mobile and registered GP practice; a GP practice directory (`api/practices`) that referrals can link to; staff full name and job title; and address, telecom and generalPractitioner on the FHIR Patient.

**Checked:** `UpdatePatientHandler.cs` logs field names only and runs `PatientRules.EnsureCanChange` first, so restricted or anonymised records can't change; staff details and the referral's practice link log names and IDs only; nothing sends a notification or email. `PatientAnonymiser` clears every new field and referrals' practice links; the export carries them; FHIR sends an anonymised record through `ToAnonymisedResource`, without the new fields. Practices: `ViewPatients` reads, `EditPatients` changes; someone else's staff details need `ManageUsers`. The migration only adds nullable columns, a table, indexes and restrict keys. Test and seed values are synthetic (Ofcom drama-range mobiles, `ZZ` postcodes, `Z999xx` ODS codes, made-up practice names).

**Finding, low, for PR 26:** `ReminderChannel` isn't in the export (`PatientExportMapper.cs` about line 324); it's always `None` until reminders are built.
