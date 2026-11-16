# Personal data breach response

**Draft prepared for review. It is not legal advice and must be reviewed and approved by a qualified data protection professional and the organisation's Data Protection Officer before use.**

| | |
|---|---|
| **Version** | 0.1 (draft), 16 November 2026 |
| **Owner** | [SIRO name], with [DPO name] |
| **Applies to** | Everyone at [Company name], employees and contractors |
| **Tested** | Not yet. A tabletop exercise is due before the first customer goes live, then every 12 months. |
| **Next review** | After every breach and every exercise; at least every 12 months |

What to do when personal data may have been lost, seen, changed or made unavailable without authority. It covers both of the supplier's roles:
- **As processor** for customers' data in MedApp: tell the customer **without undue delay** (UK GDPR Article 33(2)), aiming for 24 hours and in any case **within 48 hours** (the commitment in [dpa-template.md](dpa-template.md), clause 6). The firm limit becomes 24 hours once out-of-hours cover exists (roadmap step 5). The customer, as controller, decides whether to tell the ICO and the people affected.
- **As controller** of its own business data: tell the ICO within **72 hours** of becoming aware, unless the breach is unlikely to result in a risk to people (Article 33(1)), and tell the people affected without undue delay if it's likely to result in a high risk (Article 34).

## 1. What counts

A **personal data breach** is a breach of security leading to the accidental or unlawful destruction, loss, alteration, unauthorised disclosure of, or access to, personal data. It doesn't have to be an attack. Examples for MedApp:
- **Confidentiality:** a staff user reads records they shouldn't (once reads are audited, this shows in the access reports); a page, export or downloaded file reaches the wrong person; a file is attached to the wrong patient; patient data is pasted into a support ticket, an email, a chat or an AI tool; a credential or database backup is exposed; one customer sees another's data.
- **Integrity:** records changed or deleted without authority; a bug that writes notes to the wrong patient or procedure.
- **Malware:** a malicious file uploaded to MedApp, and opened on a customer's computer (uploads aren't scanned yet: [dpia.md](dpia.md), risk 21). Contain it by deleting the file (an admin can delete any file), and give the customer the list of who downloaded it, from the audit log.
- **Availability:** a ransomware attack; data lost and not recoverable from backups; an outage that stops clinicians seeing records they need for care.

**Security incidents without personal data** (for example a failed attack, or an outage with no data effect) follow the same first steps, and are logged, but aren't breaches.

## 2. Roles

| Role | Who | Does |
|---|---|---|
| **Incident lead** | [SIRO name]; deputy [name] | Runs the response, decides on containment, makes the calls to customers |
| **Technical lead** | [Lead developer name] | Investigates, contains and recovers, keeps evidence |
| **DPO** | [DPO name], [DPO contact] | Advises on risk and notification; checks the notices; keeps the breach log |
| **Customer contact** | The incident lead, or a named deputy | Tells each affected customer, and keeps them updated |

With one developer today, the same person may hold several roles. The **external DPO service** gives the independent check. The supplier should contract **out-of-hours cover** (roadmap step 5) before customers rely on MedApp around the clock.

**Contacts:** [incident phone], [incident email]. Customers' breach contacts are in Schedule 1 of each data processing agreement.

## 3. The procedure

The clock starts when the supplier is **aware**: when it has a reasonable degree of certainty that a security incident has led to personal data being compromised. A suspicion is investigated at once; it doesn't wait for certainty.

| Step | When | What |
|---|---|---|
| **1. Report** | At once | Anyone who suspects a breach tells the incident lead by phone, then by email. Don't investigate alone, delete evidence, or contact anyone outside. |
| **2. Contain** | Within the first hour | Stop it getting worse. For example: end a compromised account's access. **Today that means** resetting its password and marking its `UserSession` rows revoked in the database, or deleting the account (which ends its sessions), because MedApp has no "disable account" or "end sessions" action yet (roadmap steps 2 and 4); rotate a secret in Key Vault and restart the pods (`deploy/azure/README.md`, Passwords); take the service offline, **turning off Argo CD's automated sync first**, because its self-heal puts a scaled-down API back within seconds (section 3a); block an IP address with a Traefik rule added by hand (none is set up in advance); pull a bad release by reverting on `main` (the deploy is a revert, [architecture handbook, 6.5](../architecture.md#65-a-deploy-from-merge-to-running-in-the-cluster)). |
| **3. Keep evidence** | Alongside 2 | Save logs, audit entries, Azure activity logs and timelines before anything rotates them. Note who did what and when. |
| **4. Assess** | Within 12 hours | Which customers, which data, how many people and records, whether special category data is involved, whether the data was encrypted, the likely consequences for the people affected, and whether it's still happening. Use section 4 to grade it. |
| **5. Tell the customer** (processor breaches) | Aim for 24 hours; **within 48 hours of awareness** at the latest (24 once out-of-hours cover exists), even if the assessment isn't finished | By phone to the customer's breach contact in working hours (by email and a named deputy's phone outside them), then in writing with the template in section 5. Each affected customer is told separately, and only about its own data. |
| **6. Support the customer's notification** | Before the customer's **72-hour** deadline | Give the customer what it needs for its own report to the ICO, and for NHS bodies, the DSPT's incident reporting tool ([DSPT](https://www.dsptoolkit.nhs.uk/Help/incident-reporting)). Help draft its message to patients if it decides to send one. The supplier doesn't contact the ICO or patients about a customer's data unless the customer asks in writing. |
| **7. Notify the ICO** (controller breaches only) | Within **72 hours** of awareness | If the breach of the supplier's own data is likely to result in a risk to people. Report online or by phone to the ICO. If the full facts aren't known, report what is known, and follow up. If later than 72 hours, give the reasons. |
| **8. Tell the people affected** (controller breaches only) | Without undue delay | If the breach is likely to result in a high risk to them (Article 34). |
| **9. Recover** | As soon as safe | Restore from backup if needed, apply the destruction log again after a restore ([retention-schedule.md](retention-schedule.md)), and fix the cause. If automated sync was turned off in step 2, turn it back on (section 3a), which also scales the API back up. |
| **10. Update** | At least every 24 hours | To each affected customer, until contained, or as agreed with them. |
| **11. Record** | Every incident | In the breach log (section 6), whether or not it was reported. Article 33(5) requires a record of every breach. |
| **12. Review** | Within 2 weeks of closure | A post-incident review: what happened, why, what worked, what changes. Update the DPIA and this procedure. Share the review with affected customers. |

### 3a. Taking the service offline under Argo CD

Argo CD runs MedApp with automated sync and `selfHeal: true` (`deploy/argocd/medapp-azure.yaml`, `medapp-kind.yaml`), and the chart sets the API's replica count (`deploy/helm/medapp/templates/api.yaml`). So any live change to what Argo CD manages (scaling the API down, deleting the Ingress, editing the Service) is put back within seconds. **Turn off automated sync first, then scale down.** On AKS (on kind, the same with `--context kind-medapp`):

```bash
# Stop Argo CD putting things back, then scale down.
kubectl -n argocd patch application medapp --type merge -p '{"spec":{"syncPolicy":{"automated":null}}}'
kubectl -n medapp get deploy                     # check the API deployment's name
kubectl -n medapp scale deployment medapp-api --replicas=0

# During recovery: restore automated sync, which also scales the API back up.
kubectl -n argocd patch application medapp --type merge \
  -p '{"spec":{"syncPolicy":{"automated":{"prune":true,"selfHeal":true}}}}'
```

A PodDisruptionBudget doesn't stop scaling to zero; it only limits evictions. These commands go into the suspected-breach runbook (step 5), and are practised in the tabletop exercise (section 8).

## 4. Grading

Use it to decide how fast to escalate and what to recommend to the customer. The customer, as controller, makes the final decision on notification for its own data.

| Grade | Typical cases | Likely outcome |
|---|---|---|
| **Low** | One record seen by another member of the same customer's staff who reported it at once; a support ticket with a patient's name, removed at once | Customer told; log it; usually not reportable to the ICO |
| **Medium** | Several records disclosed inside the customer, or one outside; an account takeover with limited access | Customer told; the customer will probably report to the ICO |
| **High** | Health data of many patients disclosed or exposed outside the customer; data of several customers; ransomware; unauthorised changes to clinical notes; data lost for good | Customer told at once by phone; ICO report and, often, telling patients; the supplier's senior management, insurer and solicitor informed |

**Health data raises the grade.** Its disclosure is likely to cause significant harm (distress, discrimination, loss of trust in care), so a breach involving it is more likely to need reporting.

## 5. Notice to a customer: template

> **Subject: Personal data breach notice: MedApp, [Customer name], [reference]**
>
> **What happened:** [what, when, how it was found]
> **When we became aware:** [date and time]
> **Data affected:** [categories: patient details, procedures, notes, attached files, staff accounts]; **special category:** [yes or no]
> **People affected:** [approximate number of patients and staff]; **records:** [approximate number]
> **Likely consequences:** [for the people affected]
> **What we've done:** [containment and recovery so far]
> **What we propose:** [further measures; what we recommend you do, for example reset passwords]
> **Still unknown:** [and when we expect to know]
> **Contact:** [incident lead, phone, email]; our DPO: [DPO contact]
> **Next update:** [date and time]

## 6. The breach log

For every incident, whatever its grade, record:
- the reference, and dates and times (when it happened, when the supplier became aware, when it was contained)
- what happened, its cause, and the data and people affected
- the customers affected, and when each was told
- the assessment and grade, and the reasons for notifying or not
- whether the ICO was told (for controller breaches), and the ICO's reference
- the actions taken, and the review's findings

The log is kept by the DPO, for **6 years** after closure ([retention-schedule.md](retention-schedule.md), section 7).

## 7. How breaches will be detected

**Detection is weak today.** MedApp has never run in production, and there's no monitoring, alerting or exported logs in any deployed environment, and reads of patient data aren't recorded ([dpia.md](dpia.md), risk 13). Before go-live:
- **Step 5:** central logs in the UK; alerts on 5xx rates, sign-in failures and lockouts, database health, backup failures, and Argo CD sync failures; Defender for Cloud; out-of-hours cover.
- **Step 7:** an audit of every read and change, access reports, and an alert when an account opens an unusual number of records.
- **Always:** customers and staff reporting what they see. The supplier's support contact and the customer's own staff are often the first to notice.

## 8. Exercises

- **A tabletop exercise** before the first customer goes live, then every 12 months: walk through a scenario (for example "a staff account at a customer has been used from an unknown IP address to view many records overnight"), time each step, and write it up. Roadmap step 3's "Done when" requires one.
- **A restore drill** every quarter (step 5) also tests the availability side.
