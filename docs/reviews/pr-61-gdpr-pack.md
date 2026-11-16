# Review: PR #61, the data protection and information governance pack (drafts)

**PR:** [#61](https://github.com/Aliator/MedApp/pull/61), branch `docs/gdpr-pack`, reviewed at `b780080` on top of `main` at `88dc1fa` (the merge of #56). `main` has since moved to `864c900` (#57 and #58), which changes none of this PR's files.

**Reviewed:** 16 November 2026, independently of the PR description's conclusions. Every claim below was checked against the documents, the code, the infrastructure files, git, the GitHub API or the source the documents cite. **No finding is high or critical;** 7 are medium and 8 low.

## Summary

The PR adds `docs/governance/`: 22 draft documents that UK data protection law, the NHS security toolkit and buyers' governance teams ask for, from who is responsible for the data to a contract, privacy notices, a risk assessment and a retention schedule. They're careful, honest about what isn't built, and mostly accurate against the code; the problems are a few places where a promise outruns the facts, and a go-live rule that doesn't match the risk table beside it.

## What changed, and why

24 files, 2,231 lines added, none removed. Roadmap step 3 in `docs/architecture.md` asks for this pack before any real patient data, so that steps 4 to 9 build features to settled decisions instead of guessing.

- **Who is responsible** (`roles-and-lawful-bases.md`): each customer (a clinic, hospital or NHS trust) is the **controller**, the organisation that decides why and how patients' data is used. The supplier that runs MedApp is its **processor**, acting only on the customer's instructions, and a controller only of its own business data. Think of a removals firm: it carries the furniture where it's told, and doesn't get to keep any.
- **The legal grounds:** guidance for customers on the Article 6 basis (public task for NHS bodies, contract or legitimate interests for private ones) and the Article 9 condition for health data (9(2)(h), with paragraph 2 of Part 1 of Schedule 1 to the Data Protection Act 2018), plus the common law duty of confidentiality.
- **What data exists, and where** (`data-inventory.md`): every stored field, from the entities in `MedApp.Domain`, and every other place data goes: the browser, logs, traces, Valkey, backups, GitHub. Plus a data flow diagram.
- **Records of processing** (`ropa.md`, `ropa-customer-template.md`): the written record UK GDPR Article 30 requires, for the supplier and as a template for customers.
- **A product DPIA** (`dpia.md`): a **data protection impact assessment**, the written risk assessment the law requires for large-scale health data. It scores 20 risks, links each measure to the code, and says what must exist before real patient data (the "go-live gate").
- **A retention schedule** (`retention-schedule.md`): how long each kind of data is kept, with clinical records following the NHS Records Management Code of Practice and reviewed rather than deleted automatically.
- **Contracts:** a data processing agreement template with the terms Article 28(3) requires (`dpa-template.md`), the sub-processor list (Microsoft Azure only), the UK-only hosting decision, and a support access policy.
- **Procedures:** breach response (the customer told within 24 hours; the regulator, the ICO, within 72 hours for the supplier's own data), data subject rights, and complaints under the new duty from the Data (Use and Access) Act 2025.
- **Notices:** privacy notice templates for patients and staff, the supplier's own notice, and a cookies and storage statement.
- **Assurance:** security policies, the NHS context (the Caldicott Principles and the National Data Opt-out), a map against the NHS Data Security and Protection Toolkit (DSPT), and a first ISO/IEC 27001 risk register.
- **Links:** step 3 of `docs/architecture.md` gets a status line and a row in its documents table; `docs/reviews/overview.md` gets a short "Data protection paperwork" section.

## Design choices

- **A product DPIA that customers adapt, rather than leaving each customer to write its own:**
  - **Instead:** give customers only the facts (the inventory and the security measures) and let each write its own DPIA.
  - **Chosen because:** NHS buyers ask suppliers for a product DPIA, and most clinics can't write one from scratch.
  - **Worse at:** a customer can adopt the summary without reading the risk table, so a mismatch between the two (finding 1) travels into every customer's copy.
- **Processor with strict "no own use" commitments:**
  - **Instead:** be a controller of usage data, which would allow product analytics.
  - **Chosen because:** it's the simplest position for buyers and keeps the supplier out of patients' data.
  - **Worse at:** any future analytics or AI feature needs every contract changed first.
- **UK-only hosting, written into the contract:**
  - **Instead:** UK or EEA hosting, which NHS guidance allows.
  - **Chosen because:** "UK only" is the simplest answer for the NHS assessment form (DTAC) and for patients.
  - **Worse at:** it rules out EU-only providers for later services, and as written it also forbids the supplier's own staff from working on the service from abroad, even in an emergency (finding 4).
- **Clinical records reviewed at the end of their period, not deleted automatically:**
  - **Instead:** automatic deletion at the end of the period.
  - **Chosen because:** the Records Management Code says records shouldn't be destroyed automatically.
  - **Worse at:** records pile up unless each customer's records manager acts on the review list.
- **Markdown in the public repository, not a document system:**
  - **Instead:** a document management system with approvals and signatures.
  - **Chosen because:** the pack is versioned with the code it describes, and reviewed in pull requests like it.
  - **Worse at:** there's no approval workflow or signature, and the DPIA's list of unfixed weaknesses is public, as the reviews already are.
- **Placeholders, not names:** `[Company name]`, `[DPO name]` and the like. Safe for a public repository; nothing is usable until they're filled.

## How it was checked

- **Read:** all 22 documents in full, the PR description, the two handbook changes, and roadmap step 3 of `docs/architecture.md` (lines 1597-1695).
- **The inventory against the code:** every entity in `MedApp.Domain` and `ApplicationUser`; what the audit `Details` JSON stores (`ProcedureChange.cs:79`, `UpdatePatientProcedureHandler.cs:26-58`); what notifications store (`AssignPatientProcedureHandler.cs:42-51`) and push (`NotificationMapper.cs`); sessions (`SessionService.cs`: 8 hours, never deleted, roles read on every request); the cookie (`SessionCookieService.cs`); every `medapp.*` storage key in `MedApp.Web/src`; the log levels (`appsettings.json`), the app's own log lines, OpenTelemetry's set-up (`MedApp.ServiceDefaults/Extensions.cs`) and that no deployment sets `OTEL_EXPORTER_OTLP_ENDPOINT`. All match, except where a finding says otherwise.
- **The controls against the infrastructure:** `deploy/azure/main.bicep` (public network access off, TLS 1.2, backup storage `Local`, no retention set so Azure's 7-day default applies, the app on the SQL admin login), `deploy.sh` (defaults to `uksouth`), the chart's network policies, hardening and replicas, the Argo CD applications, and the workflows (actions pinned by tag).
- **The GitHub ruleset,** through the API: `main` requires a pull request, the `web`, `helm`, `unit-tests` and `image` checks, no approvals, and has no bypass actors. As `security-policies.md` says.
- **The draft line:** line 3 of all 22 documents is the same draft and not-legal-advice statement (a scripted comparison).
- **Nothing personal:** searched for email addresses, phone numbers and names. Only placeholders.
- **Links:** every relative link resolves, and every anchor into `docs/architecture.md` matches a heading.
- **The law:** checked the controller and processor reasoning, Articles 6, 9, 28(3), 30(5), 33, 34, 35, 36 and 37(1)(c), the Schedule 1 conditions, and the complaints duty against the provisions cited. Fetched two sources: the ICO's statement of 12 November 2026 on record snooping (it confirms "over 200" health leaders and "over 300 reports" from 1 January to mid-October 2026), and the ICO's brief guide to international transfers (its three-step test: a restricted transfer needs a receiver that is a separate legal entity).
- **The NHS retention periods:** compared with the Code's schedule as cited by the handbook (adults 8 years, children to the 25th or 26th birthday, mental health 20 years, maternity 25 years, oncology 30 years). Not re-fetched; the schedule itself flags two periods to check.
- **CI:** run [43827245599](https://github.com/Aliator/MedApp/actions/runs/43827245599) on `b780080` passed (`web`, `helm`, `unit-tests`, `image` and `coverage-comment`).
- **Merging with `main`:** the PR's own files don't overlap what `main` changed since `88dc1fa`. This review's row in `docs/reviews/README.md` does: `main` added the #57 and #58 rows at the same place, so the index conflicts until `main` is merged in. Keep all three rows, #57, #58, then #61.
- **Not done:** no local build or test run (docs only; CI ran both), the DTAC form and the 2026-27 DSPT assertions weren't downloaded, and this is not legal advice.

## Findings

Most severe first. None is high or critical. None was fixed in the PR at the time of this review; all 15 were fixed afterwards in `76455c2`, before merging, as each finding's last line says.

### 1. The go-live gate leaves the wrong-patient risk high, but the summary says every risk ends low or medium

- **Severity:** medium. **Confidence:** high.
- **What could go wrong:** the DPIA's summary says that after roadmap steps 2, 4, 5 and 7, "every risk in this assessment is reduced to low or medium, so prior consultation with the ICO (Article 36) isn't expected". But risk 8, notes written on the wrong patient's record, is reduced by a patient banner in step 6, which isn't in the gate, and its own table says it stays high until then. The handbook plans the first pilot straight after step 7. A clinic adopts the product DPIA at that point, reads the summary, and starts processing without consulting the ICO while its own risk table shows a high risk. **Article 36** requires that consultation when a DPIA leaves a high risk unreduced. The summary's last sentence ("If any high residual risk remains at go-live …") catches it only when the DPIA is read closely. It's like a pre-flight checklist whose last line says "all green" while one gauge, on the facing page, is still red.
- **Evidence:**
  - `docs/governance/dpia.md:19`: the summary's claim
  - `docs/governance/dpia.md:111`: risk 8, possible and severe, high
  - `docs/governance/dpia.md:218-221`: the banner in steps 6 and 9; "now high; after step 6 medium"
  - `docs/governance/dpia.md:351` and `docs/governance/README.md:79`: the gate is steps 2, 4, 5 and 7
  - `docs/architecture.md:1436`: "After step 7, MedApp is ready for a first paying pilot"
- **Fix:** either bring the patient banner (name, date of birth and a check before writing notes) forward into the gate, as a "before go-live" measure under risk 8, or say in the summary that risk 8 stays high until step 6 and that the gate therefore includes step 6's banner, or consultation. Then make `README.md:79` match.
- **What happened to it:** **fixed** (`76455c2`). The owner chose to bring the patient banner into the gate: `dpia.md`'s summary now says the gate is steps 2, 4, 5 and 7 plus step 6's patient banner, because risk 8 stays high without it; risk 8's banner is a "before go-live" measure; the sign-off row, `README.md` and the handbook's step 3 status line say the same.

### 2. Confidentiality clauses and the supplier's MFA are described as in place, but they're to-dos

- **Severity:** medium. **Confidence:** high.
- **What could go wrong:** the processing agreement's security schedule, which becomes part of a signed contract, lists "Confidentiality clauses; a support access policy" as **in place**. The rest of the pack says the clauses are still to be written and the policy is an unapproved draft. The supplier's record of processing likewise gives "Company accounts with MFA" as the security for every business activity, while the DSPT map calls the supplier's MFA "policy now", a gap. A customer's governance team relies on the schedule in its own DPIA, and on the clauses to meet the professional secrecy condition (Article 9(3)) for the supplier's people. An audit under clause 12.2 then finds no such clause in a contractor's agreement: a misstatement in a signed contract, and a condition the customer's lawful basis leans on that isn't met. The schedule's own note to check items before signing helps, but only if someone does.
- **Evidence:**
  - `docs/governance/dpa-template.md:175`: "People: In place: Confidentiality clauses; a support access policy"
  - `docs/governance/dspt-readiness.md:47`: "Staff contracts with confidentiality clauses: Gap (step 3)"
  - `docs/governance/ropa.md:82`: "(all planned)"; `docs/governance/README.md:107`: "Put confidentiality clauses in every contract"
  - `docs/governance/ropa.md:94-103`: "Company accounts with MFA" for each activity; `docs/governance/dspt-readiness.md:73`: "Supplier accounts: policy now"
- **Fix:** move both items in `dpa-template.md:175` to the Planned column with a date, and write the security column in `ropa.md` part B as "MFA on company accounts [to confirm before approval]" until it's true.
- **What happened to it:** **fixed** (`76455c2`). Confidentiality clauses and supplier MFA moved to Planned with a target date in `dpa-template.md` Schedule 2, `support-access-policy.md` and `security-policies.md`; MFA is marked "to confirm", including in every row of `ropa.md` part B; the support access policy stays in place as a draft to approve before signing.

### 3. The patient notice says data never leaves the UK and only the customer's staff see it; the pack itself says otherwise

- **Severity:** medium. **Confidence:** high.
- **What could go wrong:** the template a customer gives its patients says "Your information isn't sent outside the UK" and "Only our staff who have an account can see your record". But the transfers document says Microsoft's engineers, some outside the UK, may access customer data for service operations, and the support access policy lets the supplier's authorised people view records on the customer's instruction. A patient who later reads a breach notice, or a subject access response that mentions Microsoft support access, finds the notice was wrong. Privacy information must say accurately who receives data and whether it leaves the UK (Articles 13 and 14), so the customer, as controller, carries a transparency complaint built on the supplier's own template. The same absolute wording is offered for procurement questionnaires, and the customer record template repeats it.
- **Evidence:**
  - `docs/governance/privacy-notice-patients-template.md:23`, `:25` and `:48`
  - `docs/governance/international-transfers.md:38` (Microsoft's access from outside the UK) against `:60` ("It isn't sent outside the UK")
  - `docs/governance/support-access-policy.md:35`: the supplier's people may look at records on instruction
  - `docs/governance/ropa-customer-template.md:41` and `docs/governance/ropa.md:61`
- **Fix:** say what the pack says. For example: "Your information is stored in the UK. [Company name], which runs MedApp for us, and its hosting provider, Microsoft, can reach it only to run and support the system, under contracts that bind them to our instructions and to confidentiality. In rare cases Microsoft's support engineers may do so from outside the UK, under the safeguards UK law requires." Use the same sense at `international-transfers.md:60`.
- **What happened to it:** **fixed** (`76455c2`). The patient notice now uses the review's wording: data is stored in the UK; the supplier and Microsoft reach it only to run and support the system; the supplier's support staff only when the customer asks, under a written policy; Microsoft's engineers in rare cases from outside the UK under UK safeguards. The same sense is in `international-transfers.md` (its customer wording and its opening line), `ropa.md` A4 and `ropa-customer-template.md`.

### 4. The contract promises things a supplier with one engineer and no out-of-hours cover can't reliably keep

- **Severity:** medium. **Confidence:** medium (partly judgement; the legal point in the second bullet is medium too).
- **What could go wrong:** several terms are fine for a large supplier but brittle for this one:
  - **24 hours, by phone:** clause 6.1 promises breach notice within 24 hours of awareness, by phone and email, with updates every 24 hours. The breach procedure adds containment within an hour and an assessment within 12 hours. But the same procedure, and the DPIA, say there's no out-of-hours cover until step 5.
  - **No access from abroad, even in an emergency:** clause 8.1 forbids any access to customer data from outside the UK without each customer's prior written approval. The support policy calls such access by the supplier's own staff a restricted transfer. Under the ICO's three-step test a restricted transfer needs a receiver that is a separate legal entity, so the supplier's own employee working abroad isn't one (a contractor is). The rule is a contractual choice that goes further than the law, with no emergency exception.
  - **Free manual work on a deadline:** clause 9.3 promises exports, corrections and erasures within 5 working days "[at no extra charge]", done by hand with export queries that don't exist yet.
  - **Dated promises:** clause 5.3 turns each planned measure into a contractual date.

  The scenario: an alert fires on a Friday evening of a bank holiday weekend while the only engineer is on leave abroad. Logging in to contain it breaches clause 8.1, and not phoning the customer by Saturday evening breaches clause 6.1. Two contract breaches in one incident, before any data is lost.
- **Evidence:**
  - `docs/governance/dpa-template.md:60` (5.3), `:64` (6.1), `:66` (6.2), `:84` (8.1), `:94` (9.3)
  - `docs/governance/breach-response.md:35` (no out-of-hours cover yet) and `:45-49`
  - `docs/governance/dpia.md:279`: "one maintainer limits round-the-clock response"
  - `docs/governance/support-access-policy.md:45` and `docs/governance/international-transfers.md:18`
  - `docs/governance/data-subject-rights.md:61`: the export queries are "[to be written … with step 5]"
- **Fix:**
  - 6.1: "without undue delay, and in any case within [48] hours of becoming aware of it", moving to 24 hours as a dated planned measure once out-of-hours cover exists, with a named deputy and phone calls in working hours.
  - 8.1: allow the supplier's own employees emergency access from outside the UK on company-managed devices, logged and reported to the customer within 24 hours, and keep the prior-approval rule for anyone else. Reword `support-access-policy.md:45` so it doesn't call an employee's access a restricted transfer.
  - 9.3: "within 5 working days, or as agreed", charged as the Main Agreement says, and write the export queries before the first customer.
  - 5.3: "will use reasonable endeavours to put it in place by the date shown".
- **What happened to it:** **fixed** (`76455c2`), as the review suggested. Clause 6.1 is 48 hours (by email, by phone in working hours, with a named deputy), moving to 24 hours once out-of-hours cover exists; clause 8.2 adds a logged emergency exception for the supplier's own employees abroad, reported within 24 hours; the support access policy and the transfers document now say, under the ICO's test, that an employee's access isn't a restricted transfer (a contractor's is); clause 9.3 is "within 5 working days, or as agreed", charged as the Main Agreement says, and the export queries are due before the first customer; clause 5.3 is "reasonable endeavours". The breach procedure, the README, the DSPT map and the NHS context follow the 48 hours. Still for the solicitor's review.

### 5. The breach procedure's "take the service offline" is undone within seconds by Argo CD

- **Severity:** medium. **Confidence:** high.
- **What could go wrong:** the containment step says to "take the service offline (scale the API to zero)". But Argo CD, the tool that keeps the cluster matching git, runs MedApp with `selfHeal: true`, and the chart sets the API's replica count. **Self-heal** means any live change to something Argo CD manages is put back. A responder scales the API to zero, sees the pods stop, and moves on; Argo CD restores two copies within seconds, and the leak the responder thinks is stopped carries on. It's like switching off a machine whose timer switches it back on. Deleting the Ingress or editing the Service would be reverted the same way.
- **Evidence:**
  - `docs/governance/breach-response.md:46`
  - `deploy/argocd/medapp-azure.yaml:27-30` and `deploy/argocd/medapp-kind.yaml:37-39`: `automated`, `prune: true`, `selfHeal: true`
  - `deploy/helm/medapp/templates/api.yaml:30`: `replicas: {{ .Values.api.replicas }}`, so the count is part of what Argo CD compares
- **Fix:** make the step "turn off automatic sync first, then scale the API to zero", with the commands (Detail), and the reverse at recovery. Put it in the step 5 runbook for a suspected breach, and practise it in the tabletop exercise.
- **What happened to it:** **fixed** (`76455c2`). `breach-response.md` step 2 now turns off Argo CD's automated sync before scaling down, with the review's commands in a new section 3a; step 9 turns it back on during recovery; the section says to put the commands in the step 5 runbook and practise them in the tabletop exercise.

### 6. Browser history isn't in the inventory, and patient search terms land there

- **Severity:** medium. **Confidence:** high that the filters are in the URL; medium on exactly how each browser records them.
- **What could go wrong:** list pages keep their filters in the URL, and the patient list's filters are first name, last name, email and a date of birth range. Every search becomes a URL like `/patients?last=…&dobFrom=…`, which the browser keeps in its history and offers back in the address bar. On a shared ward or reception computer, the next person types `/patients` and sees who was searched for, and that they're a patient here. Signing out doesn't clear history, and step 7's planned fix clears only `localStorage`, so the DPIA's "after: low" for shared computers is too optimistic. The inventory already knows filters are in URLs, but only mentions it for traces.
- **Evidence:**
  - `MedApp.Web/src/pages/listSearch.ts:49-58` (`first`, `last`, `email`, `dobFrom`, `dobTo`) and `MedApp.Web/src/routes/_app/patients/index.tsx:9`; `AGENTS.md:163`: "List pages keep their page, sort and filters in the URL"
  - `docs/governance/data-inventory.md:39-41`: the browser rows, with no history; `:46`: filters in URLs, for traces only
  - `docs/governance/dpia.md:113`, `:239` and `:242`: risk 10's sources, its fix (clear `localStorage`) and "after **low**"
  - `docs/governance/cookies-and-storage.md:43`
- **Fix:** add a "browser history" row to the inventory and to DPIA risk 10, and a line to the staff rules. In step 7, keep identifying filters (name, email, date of birth) out of the URL, in session storage or the router's state, while page, sort and status stay in it.
- **What happened to it:** **fixed** (`76455c2`). A browser history row in `data-inventory.md`; risk 10 in `dpia.md` names history as a source, with a "before go-live" step 7 measure to keep identifying filters out of the URL and a staff rule; the same in the README's step 7 build list, the staff notice's rules and the cookies statement.

### 7. Nothing makes the next change update the pack

- **Severity:** medium. **Confidence:** high.
- **What could go wrong:** the pack's rule is that any change adding personal data, a purpose, a recipient or a sub-processor updates the inventory and the DPIA before it's merged. But that rule lives only in the pack. `AGENTS.md`, which every change follows, has no such step in its feature checklist or in what a merge review looks for. The to-do list on `main` already has a patient timeline built from the audit log, and the #57 review warned it would show admin-only history to all staff. Built to `AGENTS.md`, it merges without touching the DPIA, whose risk 4 still says "The audit trail is admin-only". The pack then describes a control that no longer exists, which is the opposite of what it sets out to do. A recipe card pinned in a different kitchen doesn't get followed.
- **Evidence:**
  - `docs/governance/README.md:113` and `docs/governance/security-policies.md:62` ("From now on")
  - `AGENTS.md:75-82`, the feature checklist, and `:296`, what a review looks for: no mention of `docs/governance`
  - `docs/todo.md:23` on `main` (the timeline); `docs/reviews/pr-57-todo-features.md`, finding 3, on `main`
  - `docs/governance/dpia.md:176`
- **Fix:** add a step to `AGENTS.md`'s checklist: "If the change adds or changes personal data, a purpose, a recipient, a sub-processor or a hosting region, update `docs/governance/data-inventory.md` and `docs/governance/dpia.md` in the same PR." Add "the governance documents still true" to what a review looks for.
- **What happened to it:** **fixed** (`76455c2`). `AGENTS.md` gains step 8 in "To add a feature end to end": a change that alters what personal data is held, who can see it, where it goes or how long it's kept updates `docs/governance/` (at least the inventory and the DPIA) in the same PR; the merge flow's step 3 "What to look for" checks that the governance documents are still true. `README.md` and `security-policies.md` point to it.

### 8. On Azure, any pod in the cluster can reach the database, not only the API and the migrations Job

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the DPIA says the network policies mean "only the API and the migrations Job reach the database". That's true on kind, where SQL Server runs in the cluster and has its own policy. On Azure the database is outside the cluster, behind a private endpoint. The chart's policies limit only MedApp's own pods, and nothing limits the other pods: Traefik (which faces the internet), Argo CD, cert-manager and External Secrets can all open a connection to the endpoint on 1433. A flaw in Traefik then gives an attacker a network path to the database, still needing a password; a responder who trusts the DPIA rules that path out. The DSPT map's "network segmentation: Have" leans on the same claim.
- **Evidence:**
  - `docs/governance/dpia.md:262` and `docs/governance/dspt-readiness.md:131`
  - `deploy/helm/medapp/templates/networkpolicy.yaml:16`, `:60`, `:81` and `:109`: each policy selects one of MedApp's pods; the database's own policy exists only when `sql.enabled` (`:99`)
  - `deploy/azure/values.yaml:32-42`: Azure SQL is reached by an egress rule to `10.40.4.0/24`
- **Fix:** reword the claim for Azure ("MedApp's own pods reach only what they need; on Azure other pods in the cluster aren't restricted yet"), and add a step 5 measure: a cluster-wide Cilium policy that denies traffic to the private endpoint subnet from every pod except MedApp's API and migrations Job.
- **What happened to it:** **fixed** (`76455c2`). `dpia.md` risk 12 now says MedApp's own pods are limited, but on Azure other pods can reach the private endpoint, and adds a step 5, "before go-live" measure: a cluster-wide Cilium policy allowing only the API and the migrations Job. The DSPT map's network segmentation is "Partly", with the gap; the README's step 5 list includes the policy.

### 9. A few legal statements are looser than the law

- **Severity:** low. **Confidence:** medium to high, as given for each.
- **What could go wrong:** a customer's DPO who checks the reasoning finds slips, and trusts the rest less. None changes a decision.
  - **"Part 1 conditions don't need" an appropriate policy document** (high): paragraph 1 of Part 1 (employment) does, as the same document says 35 lines later. Paragraph 2, the one customers use, doesn't, so that conclusion stands.
  - **Article 6(1)(c) for "the Records Management Code" and "the DSPT"** (medium): a legal obligation must be laid down in law. The Code is guidance and the DSPT a requirement of NHS contracts; for NHS bodies the legal duty is the Public Records Act 1958. Public task (6(1)(e)) covers these anyway.
  - **Implied consent "applies to its use by the customer's staff"** (high): it covers staff caring for the patient. Read as written, it seems to bless every staff member seeing every patient, the DPIA's top risk.
  - **"Article 22 (as amended by the DUAA)"** (medium): the DUAA replaced Article 22 with Articles 22A to 22D.
- **Evidence:** `docs/governance/roles-and-lawful-bases.md:70` against `:105`; `:64` and `:87`; `:80`; `:73` (and `docs/governance/data-subject-rights.md:11` and `:43`).
- **Fix:** "Paragraph 2 doesn't need one"; cite the Public Records Act 1958 for NHS records, or drop 6(1)(c) where 6(1)(e) applies; "applies to access by staff involved in the patient's care"; "Articles 22A to 22D".
- **What happened to it:** **fixed** (`76455c2`). `roles-and-lawful-bases.md`: paragraph 2 needs no appropriate policy document, though paragraph 1 does; public task covers the Code's record keeping, with the Public Records Act 1958 as the NHS legal duty, and 6(1)(c) only for duties in law (also for the DSPT on the staff row); implied consent covers access by staff involved in the patient's care; Articles 22A to 22D, also in `data-subject-rights.md`.

### 10. The DPIA scores the same likelihood and severity two ways

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** risk 18 (remote, severe) is scored low; risk 20 (remote, severe) is scored medium. An assessor comparing rows can't tell which rule the DPIA uses, and a customer copying it may apply the lower score to its own remote-but-severe risks.
- **Evidence:** `docs/governance/dpia.md:121` and `:123`.
- **Fix:** add the matrix under "Scoring" (`dpia.md:95-98`) and rescore one of the two, or give the reason for the exception.
- **What happened to it:** **fixed** (`76455c2`). The matrix is under "Scoring" in `dpia.md` (remote and severe is medium, so risk 20 stays medium); risk 18 is the stated exception, low because it can't happen yet, to rescore at step 8.

### 11. The handbook still says there's no DPIA, ROPA or contract

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the PR adds a status line to step 3, but the handbook's risk table and compliance map still say "None" or "Nothing written" for each document this PR adds. A buyer reading section 11 or the compliance map concludes there's no paperwork at all; one reading step 3 sees drafts.
- **Evidence:** `docs/architecture.md:1276` (S4: "no DPIA, retention policy, subject access or erasure process"); `:1418` (red flags, a dated snapshot); the compliance map at `:2142`, `:2147`, `:2151-2154` and `:2156`.
- **Fix:** S4 and the map's rows to "Drafts in `docs/governance/`, not approved" (the step stays 3), and leave the dated red flags list as it is.
- **What happened to it:** **fixed** (`76455c2`). S4 and the compliance map's rows (lawful basis and notices, storage limitation, the ROPA, Article 28, breaches, the DPIA, the DPO, transfers, complaints, PECR, the Records Management Code) now say "Drafts in `docs/governance/`, not approved"; the dated red flags list is unchanged.

### 12. The staff notice states retention periods MedApp doesn't apply yet

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the staff notice template tells staff their sign-in records are kept 12 months and notifications 6 months. Today both are kept for ever. The template holds back the access-audit sentence until step 7, with an italic note, but not this one. A customer that publishes the notice before step 7 tells its staff something untrue. The go-live gate makes that unlikely, which is why this is low.
- **Evidence:** `docs/governance/privacy-notice-staff-template.md:36` against `:22`; `docs/governance/data-inventory.md:29` ("Rows are never deleted today").
- **Fix:** put the periods in brackets with the same italic note as line 22.
- **What happened to it:** **fixed** (`76455c2`). The staff notice's periods are in brackets, with an italic note to include them only once step 7 deletes on schedule.

### 13. If a customer doesn't answer at the end of the contract, the agreement deletes its health records

- **Severity:** low. **Confidence:** medium (a contract choice).
- **What could go wrong:** clause 11.3 deletes all of a customer's data 30 days after the 30-day window if the customer doesn't answer. A clinic that closes or goes into administration misses the emails, and records it must keep for 8 to 25 years are destroyed, with patients unable to get them. Article 28(3)(g) allows deletion as the default, but for health records it's the worse default.
- **Evidence:** `docs/governance/dpa-template.md:102-106`.
- **Fix:** on no answer, send reminders, then return an export to the last named contact by the agreed secure route, and delete only after confirmation or a longer period [to agree].
- **What happened to it:** **fixed** (`76455c2`). New clause 11.3A: no deletion when the customer doesn't answer; two reminders, then an export to the last named contact, and deletion only after confirmation or [6 months]. Schedule 4, `retention-schedule.md` section 5 and the README match. Still for the solicitor's review.

### 14. The review and approval claims read stronger than they are

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** the secure development policy lists "an independent review of every change" as in place, and the DSPT map says "every merge has a written adversarial review". The review is by a separate AI agent that isn't given the PR's own conclusions; there's no second human reviewer, as the next row of the policy admits. And per-PR reviews exist from #51 onwards, not for #1 to #50. An assessor reads "independent" as a second person, and "every merge" as the whole history.
- **Evidence:** `docs/governance/security-policies.md:53` and `:57`; `docs/governance/dspt-readiness.md:84`; `AGENTS.md`, "Effort for each stage of a change"; `docs/reviews/README.md` (per-PR reviews from #51).
- **Fix:** "a written review of every merge since #51, by a separate AI agent not given the PR's own conclusions; no second human reviewer yet".
- **What happened to it:** **fixed** (`76455c2`). `security-policies.md`, `dspt-readiness.md` and `ropa.md` now say a written review of every merge since #51, by a separate AI agent not given the PR's own conclusions, not yet approved by a human.

### 15. Smaller statements that don't match the code or each other

- **Severity:** low. **Confidence:** high.
- **What could go wrong:** each is small; together they cost the pack the trust its candour earns.
  - **The audit can be filtered by patient:** the DPIA and the rights procedure say it can be searched "by procedure, not by patient". The API has a `PatientId` filter; only the screen lacks it. (`dpia.md:109`, `data-subject-rights.md:37`; `SearchProcedureAuditRequest.cs:15`, `ProcedureAuditSearch.cs:17-18`, `AuditLogPage.tsx:47-58`.)
  - **Not every endpoint needs a session:** "Every API endpoint requires a signed-in session and a role"; sign-in, sign-out and `whoami` don't. (`dpia.md:138`; `AuthController.cs:40`, `:61`, `:334`.)
  - **Exports carry health data:** the record of processing lists special category data for A3.1, A3.2, A3.4 and A3.6, but not A3.7, the exports at exit. (`ropa.md:53-55`.)
  - **Confirm or deny:** the rights procedure tells the requester the request went to the customer "which holds their record", then says not to confirm or deny that the data is held. (`data-subject-rights.md:19-20`.)
  - **"Another branch":** two documents say another branch is building privacy features now. It isn't named, can't be checked from the repository, and will be wrong once it merges. (`README.md:83`, `retention-schedule.md:103`.)
  - **"Push-only":** the GitHub identity has `AcrPush`, which can pull too; the point that matters (it can't reach the database or Key Vault) holds. (`data-inventory.md:82`, `sub-processors.md:38`; `main.bicep`, `githubAcrPush`.)
- **Fix:** correct each sentence as above; name the other work by its PR once there is one, or drop the sentence.
- **What happened to it:** **fixed** (`76455c2`). The audit can be filtered by patient in the API but not on screen (`dpia.md` risk 6, `data-subject-rights.md`); sign-in, sign-out and `whoami` are named as the endpoints without a session (`dpia.md`, `ropa.md`, `security-policies.md`, `dspt-readiness.md`, Schedule 2); A3.7 is listed as special category; the reply to a requester no longer confirms the customer holds a record; "another branch" is now "the privacy features work on roadmap step 7" (`README.md`, `retention-schedule.md`); the GitHub identity is described as limited to the registry (`AcrPush`, which can push and pull).

## Remaining risks

- **Drafts aren't controls:** the DPO, the ICO fee, the solicitor, Cyber Essentials, the DSPT and the tabletop exercise are all still to do. Until they are, the pack is a plan, and the governance gaps in the DPIA are as real as the technical ones.
- **Other work in progress will make parts stale:** a forced password change, a patient timeline and admin insights are in progress or planned, and each changes what the DPIA and inventory say. Finding 7's rule is what keeps that from drifting.
- **Unchecked sources:** the 2026-27 DSPT assertions, the DTAC form, Microsoft's current data protection terms and the current Records Management Code weren't read for this review; the pack marks each to check.
- **A public DPIA:** the risk table lists unfixed weaknesses in a public repository. The reviews already do, so little is new, but it stays true until the repository is private (step 1).
- **Legal judgement:** this review checks the reasoning at the level the provisions allow. It doesn't replace the DPO's and the solicitor's review, which the pack rightly requires.

## Say it like this

"PR #61 adds the data protection paperwork MedApp needs before it holds real patient data: who's responsible, the legal grounds, a full data inventory taken from the code, a risk assessment, retention periods from the NHS records code, a customer contract, privacy notices and breach and rights procedures. It's honest about what isn't built yet. The fixes it needs are where a promise outruns the facts: the risk assessment's go-live rule leaves one high risk it says it removes, the patient notice says data never leaves the UK when the pack itself says Microsoft might reach it from abroad, the contract promises round-the-clock response the supplier can't yet give, and the breach procedure's way of taking the service offline is undone by the deploy tool in seconds."

## Detail

- **The go-live arithmetic for finding 1:** the risks whose reduction to low or medium needs a step outside the gate (2, 4, 5 and 7):

  | Risk | After the gate | Needs |
  |---|---|---|
  | 8, wrong patient | High | Step 6 (banner), step 9 (NHS number) |
  | 15, supply chain | Low to medium | Step 1 too, but step 2 depends on step 1, so it's done |
  | 14, loss of service | Low | The downtime procedure is step 6, but the risk is already low without it |

  Only risk 8 stays high.

- **Taking the API offline under Argo CD (finding 5),** on AKS (on kind, the same with its own context):

  ```bash
  # Stop Argo CD putting things back, then scale down.
  kubectl -n argocd patch application medapp --type merge -p '{"spec":{"syncPolicy":{"automated":null}}}'
  kubectl -n medapp scale deployment medapp-api --replicas=0

  # Afterwards: scale back (or let Argo CD do it) by restoring automatic sync.
  kubectl -n argocd patch application medapp --type merge \
    -p '{"spec":{"syncPolicy":{"automated":{"prune":true,"selfHeal":true}}}}'
  ```

  The deployment's name comes from the chart's `medapp.fullname` with `-api`; check it with `kubectl -n medapp get deploy` in the runbook. A PodDisruptionBudget doesn't stop scaling to zero; it only limits evictions.

- **The ICO's test for a restricted transfer (findings 3 and 4):** the brief guide's step 3 asks "Is the organisation receiving the personal information a separate legal entity to us?" Microsoft's engineers are a separate entity, so their access from abroad is a restricted transfer, covered by Microsoft's terms. The supplier's own employee isn't; a contractor is.

- **The patient filters in the URL (finding 6),** from `MedApp.Web/src/pages/listSearch.ts:49-58`:

  ```ts
  export interface PatientsListSearch {
    page?: number;
    sort?: PatientSortColumn;
    asc?: boolean;
    first?: string;
    last?: string;
    email?: string;
    dobFrom?: string;
    dobTo?: string;
  }
  ```

- **The suggested `AGENTS.md` step (finding 7),** after step 6 of "To add a feature end to end":

  ```markdown
  7. If the change adds or changes personal data, a purpose, a recipient, a sub-processor or a hosting region, update `docs/governance/data-inventory.md` and `docs/governance/dpia.md` in the same PR.
  ```

- **What was found accurate,** worth keeping as it is:
  - the inventory's fields, including the copies of notes in audit `Details` and the titles in notifications and Valkey
  - "no analytics, tracking or third-party scripts": no external URLs load in the web app, and the only cookie is `medapp.session`
  - logging: EF Core logs SQL without parameter values, ASP.NET Core's request logs are below the configured level, and the app's own log lines carry paths and IDs only
  - OpenTelemetry isn't exported in any deployment
  - the Azure facts: backups locally redundant with the 7-day default, public network access off, TLS 1.2, UK South by default, the app on the SQL admin login
  - the ruleset's required checks, and the absence of required approvals
  - the controller and processor analysis, the Article 9 and Schedule 1 route for customers, the Article 28(3) terms (every point (a) to (h) is there), the 72-hour rule, the processor's duty to tell the controller without undue delay, and the DUAA complaints duty
