# International transfers and data location

**Draft prepared for review. It is not legal advice and must be reviewed and approved by a qualified data protection professional and the organisation's Data Protection Officer before use.**

| | |
|---|---|
| **Version** | 0.1 (draft), 16 November 2026; 0.2 (draft), 18 November 2026: texts and emails to patients through GOV.UK Notify, the customer's own processor, used only in the Live mode (`docs/centre-phases-3-5-plan.md`, PR 26; branch `feature/patient-reminders`) |
| **Owner** | [DPO name], Data Protection Officer, [Company name] |
| **Next review** | When a sub-processor or region changes; when UK adequacy regulations change; at least every 12 months |

## The decision: UK only

**All customer personal data is stored and processed in the UK.** The one exception is Microsoft's own support access, below, which may in rare cases come from outside the UK.
- **Primary region:** Microsoft Azure **UK South**. `deploy/azure/deploy.sh` creates the resource group there by default, and `main.bicep` puts every resource in the resource group's region.
- **Backups:** Azure SQL's backups stay in UK South today (locally redundant, `deploy/azure/main.bicep`). Step 5 makes them geo-redundant, which copies them to Azure's paired region, **UK West**, so they stay in the UK.
- **Attached files:** in a storage account in UK South, zone-redundant (three copies across UK South's availability zones), with no replication to another region (`deploy/azure/main.bicep`). Backups of the files (step 5) must stay in the UK too.
- **Disaster recovery:** a rebuild goes to UK West.
- **Logs and monitoring** (step 5): a Log Analytics workspace in UK South.
- **Texts and emails to patients** (PR 26), only when a customer turns on the Live mode: each message's mobile number or email address and its text go to **GOV.UK Notify**, the customer's own processor ([sub-processors.md](sub-processors.md)). Live is off by default and configured nowhere in the repository or the chart; the offline mode used in development sends nothing outside the deployment. Where Notify, and the providers that deliver its texts and emails, process data is [to confirm with GOV.UK Notify's documentation] (below).
- **Support:** the supplier's staff access customer data only from the UK ([support-access-policy.md](support-access-policy.md)), with one exception: an employee who is temporarily abroad may act in an emergency, on a company-managed device, logged and reported to the customer within 24 hours ([dpa-template.md](dpa-template.md), clause 8.2).
- **Which access is a transfer:** under the ICO's test, a restricted transfer needs a receiver that is a separate legal entity ([ICO](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/international-transfers/a-brief-guide-to-international-transfers/)). So the supplier's own employee working abroad isn't one; a contractor or another company (including Microsoft) is. The UK-only rule for employees is a contractual choice, stricter than the law; access by anyone else from abroad needs the customer's prior written approval and a transfer safeguard.

**Why UK only:**
- NHS guidance allows the UK, the European Economic Area, or a country with UK adequacy regulations ([NHS England](https://digital.nhs.uk/data-and-information/looking-after-information/data-security-and-information-governance/nhs-and-social-care-data-off-shoring-and-the-use-of-public-cloud-services/guidance)).
- "UK only" is the simplest answer to DTAC's data location question, to customers' IG teams, and to patients.
- It avoids transfer risk assessments for the main processing.
- Azure has two UK regions, so UK-only hosting still allows recovery to a second site.

## What isn't yet enforced

- **The region is a default, not a rule.** `deploy.sh` accepts another region as its second argument. Step 5 adds an **Azure Policy** at the subscription that allows only `uksouth` and `ukwest`, so nothing can be created elsewhere. Until then, the deploy runbook must use `uksouth`.
- **Geo-redundant backups** are planned (step 5); today's backups are locally redundant in UK South.

## Restricted transfers, one by one

A restricted transfer is personal data sent to, or made accessible from, a country outside the UK (UK GDPR Chapter V).

| Possible transfer | Customer personal data? | Assessment | Safeguard |
|---|---|---|---|
| **The supplier's own processing** of customer data | Yes | None: UK hosting, UK support | Not needed |
| **Microsoft's service operations and support** for Azure | Possibly, rarely | Microsoft's terms allow its engineers, some outside the UK, to access customer data for service operations and support in limited cases, under its controls. | Microsoft's Data Protection Addendum, with the UK's International Data Transfer Addendum to the EU standard contractual clauses or the UK-US data bridge (for Microsoft's US entities, which are certified to the EU-US Data Privacy Framework's UK extension) [to confirm against Microsoft's current terms when signing] ([ICO](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/international-transfers/adequacy-regulations/how-does-the-uk-extension-to-the-eu-us-data-privacy-framework-work/)). **Planned (step 5):** turn on **Customer Lockbox for Microsoft Azure**, so Microsoft support can reach customer data only with the supplier's approval for each request. Don't open Azure support cases that attach customer data. |
| **GitHub** (United States) | **No.** Code, images and CI only | Not a transfer of customer data | Not needed. GitHub holds the supplier's developers' details, which is the supplier's own processing. |
| **Microsoft Defender for Storage's malware scanning** of attached files (planned, step 9, brought forward before go-live) | Yes: each uploaded file's contents | To be assessed before it's turned on: Microsoft says scanning happens in the storage account's region [to confirm against its current documentation and terms] | **Decision:** turn it on only if files are scanned in UK South and Microsoft keeps no copy of their contents |
| **GOV.UK Notify,** for texts and emails to patients (PR 26; only in the Live mode, off by default) | Yes: each message's destination (a mobile number or email address) and text (the organisation's name, the appointment's date, time and site; never clinical content or the NHS number) | **The customer's own transfer question, not the supplier's:** the customer contracts Notify, so Notify is its processor, and the customer assesses it in its own DPIA. Where Notify stores and processes data, and where the networks and providers that deliver its texts and emails do, is [to confirm with GOV.UK Notify's documentation] | **Decision:** the supplier offers the Live mode only once the customer has confirmed, from Notify's terms, that the data stays in the UK, or goes only to a country with UK adequacy regulations or under an Article 46 safeguard, and has recorded it in its DPIA. A provider the supplier contracted instead would be the supplier's sub-processor, assessed here first, with the same rule as an email provider (UK or EU hosting) |
| **An email provider** (step 4) | Staff email addresses; no patient data | To be assessed when chosen | **Decision:** only a provider that hosts in the UK or the EU (the EU has UK adequacy regulations) |
| **A monitoring service** (step 5), if contracted | Alerts and possibly logs | To be assessed when chosen | **Decision:** UK or EU hosting, and no log content beyond what an alert needs |
| **The supplier's own business tools** (email, accounts, helpdesk) | No customer personal data by policy | The supplier's own processing ([ropa.md](ropa.md), part B) | Assessed for each, as the supplier's own transfers |

## Transfer risk assessment

**Not needed for the supplier's processing of customer data**, because there are no restricted transfers by the supplier.

**For Microsoft's limited access**, the supplier relies on Microsoft's own assessment and safeguards in its contract, and on Customer Lockbox once it's on. [Record here, when signing, which Microsoft terms apply and the date checked.]

If a restricted transfer is ever proposed (a new sub-processor, support from abroad), the supplier will:
1. Check whether the country has UK adequacy regulations.
2. If not, use the ICO's International Data Transfer Agreement (IDTA) or the Addendum, and complete a transfer risk assessment ([ICO](https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/international-transfers/a-brief-guide-to-international-transfers/)).
3. Update the DPIA, this document and the sub-processor list.
4. Give customers 30 days' notice ([sub-processors.md](sub-processors.md)).

## What customers can say

For a privacy notice or a procurement questionnaire:

> Your information is stored on servers in the United Kingdom, run by [Company name] on Microsoft Azure. [Company name] and Microsoft can reach it only to run and support the system, under contracts that bind them to our instructions and to confidentiality. In rare cases Microsoft's support engineers may do so from outside the UK, under the safeguards UK law requires.
