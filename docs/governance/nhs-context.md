# The NHS context: Caldicott, the National Data Opt-out and certification

**Draft prepared for review. It is not legal advice and must be reviewed and approved by a qualified data protection professional and the organisation's Data Protection Officer before use.**

| | |
|---|---|
| **Version** | 0.1 (draft), 16 November 2026 |
| **Owner** | [DPO name], with [SIRO name] |
| **Next review** | Every 12 months |

What NHS customers' information governance teams will ask, beyond UK GDPR.

## 1. The Caldicott Principles

The eight Caldicott Principles guide the use of confidential information in health and care ([GOV.UK](https://www.gov.uk/government/publications/the-caldicott-principles)). NHS customers' Caldicott Guardians apply them. How MedApp supports each, today and planned:

| Principle | How MedApp supports it | Gaps and plans |
|---|---|---|
| **1. Justify the purpose** for using confidential information | MedApp has one purpose: coordinating and recording patients' procedures for their direct care. The supplier uses the data for nothing else ([roles-and-lawful-bases.md](roles-and-lawful-bases.md)). | Any new purpose needs a DPIA update first. |
| **2. Use confidential information only when it's necessary** | Coordinating a procedure for a person needs to know who they are. | |
| **3. Use the minimum necessary** | A deliberately small patient record: name, date of birth, email. | Free text is unlimited; guidance for staff, and in-app guidance (step 7). Copies of notes in the audit (step 7 replaces them with note versions). |
| **4. Access on a strict need-to-know basis** | Signed-in staff only; admin-only functions and audit log. | **Every staff user sees every patient.** Access by site or team (step 8); reads are recorded and each patient's page shows admins who has accessed it (step 7); alerts and periodic reviews still to come. |
| **5. Everyone with access understands their responsibilities** | Staff rules in [privacy-notice-staff-template.md](privacy-notice-staff-template.md); the supplier's training and support access policy. | A sign-in notice that access is monitored (step 7). |
| **6. Comply with the law** | This governance pack. | Not yet approved. |
| **7. The duty to share for individual care is as important as the duty to protect confidentiality** | All of a customer's care team can see what they need. | Sharing with other organisations (EPR integration, letters to GPs) comes with step 9. |
| **8. Inform patients and service users** about how their information is used | The patient privacy notice template ([privacy-notice-patients-template.md](privacy-notice-patients-template.md)). | The customer publishes it. |

## 2. The National Data Opt-out

- **What it is:** a patient's choice that their confidential patient information shouldn't be used for research and planning. It applies to data from publicly funded care in England ([NHS England](https://digital.nhs.uk/services/national-data-opt-out/operational-policy-guidance-document/which-organisations-does-the-opt-out-apply-to)).
- **It doesn't apply to MedApp's use today.** MedApp is used for **direct care**, and the opt-out doesn't apply to direct care.
- **It would apply** if MedApp's data were ever used for research or planning, for example the pseudonymised exports in "Features worth adding" (item 13 of the handbook's list). Before any such feature, the supplier and the customer must:
  1. update the DPIA and the lawful bases
  2. check each patient's opt-out with NHS England's service, through the customer
  3. record the result
- **The supplier itself** makes no use of customer data for research or planning, so it needs no opt-out compliance today. It confirms this in its DSPT submission.

## 3. Certification and registration

| Item | Decision | Status |
|---|---|---|
| **ICO fee and registration** | Pay as a controller of the supplier's own data | To do; the number goes in DTAC |
| **DPO** | An external DPO service | To do |
| **DSPT** | Register as a supplier. With under 50 staff and £10 million turnover, the supplier is a small supplier, not a large one needing an independent audit ([DSPT](https://www.dsptoolkit.nhs.uk/Help/Org-Types)). Complete the 2026-27 assessment, due by **30 June 2027** ([DSPT](https://www.dsptoolkit.nhs.uk/News/169)). | To do; readiness in [dspt-readiness.md](dspt-readiness.md) |
| **Cyber Essentials** | Certify the company's own IT (devices, GitHub, Azure, email) to v3.3: cloud services are in scope, and MFA is required on them ([IASME](https://iasme.co.uk/articles/important-update-changes-to-cyber-essentials-for-april-2026/)). Needed for government and NHS contracts that handle personal data ([PPN 014](https://www.gov.uk/government/publications/ppn-014-cyber-essentials-scheme/ppn-014-cyber-essentials-scheme-html)). | To do (step 3); Cyber Essentials Plus in step 10 |
| **DTAC** | Complete version 2.0 for each NHS buyer. Its data protection section draws on this pack: the DPIA, the ICO number, data location (UK), the DSPT status. | Draft in step 6, complete in step 10 |
| **DCB0129** | A clinical risk management system, with a Clinical Safety Officer | Step 6 |
| **ISO/IEC 27001** | Start the ISMS now ([isms-risk-register.md](isms-risk-register.md)); certify in step 10 | Started |
| **NHS Cyber Security Charter for suppliers** | Sign when its commitments are met (step 10) | Not yet |

## 4. Incident reporting for NHS customers

NHS customers report personal data breaches and cyber incidents through the DSPT's incident reporting tool, which also notifies the ICO where needed ([DSPT](https://www.dsptoolkit.nhs.uk/Help/incident-reporting)). The supplier's job is to tell them within 48 hours (24 once out-of-hours cover exists, step 5), with the facts they need for it ([breach-response.md](breach-response.md)).
