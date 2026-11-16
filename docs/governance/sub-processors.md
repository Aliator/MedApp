# Sub-processors

**Draft prepared for review. It is not legal advice and must be reviewed and approved by a qualified data protection professional and the organisation's Data Protection Officer before use.**

| | |
|---|---|
| **Version** | 0.1 (draft), 16 November 2026 |
| **Owner** | [DPO name], Data Protection Officer, [Company name] |
| **Published at** | [URL of the published list] |
| **Next review** | Whenever a provider is added, removed or changes what it does; at least every 6 months |

The organisations [Company name] uses to provide MedApp, and what each does with customers' personal data. Customers authorise these in the data processing agreement ([dpa-template.md](dpa-template.md), clause 7). The list is taken from the real stack: `deploy/azure`, `deploy/helm/medapp`, `.github/workflows` and `deploy/argocd`.

## How changes are made

- **Notice:** the supplier gives customers **30 days' written notice** before adding or replacing a sub-processor that will process their personal data, by email to each customer's data protection contact and by updating this list.
- **Objection:** a customer may object on reasonable data protection grounds within those 30 days. The supplier will then try to find another way. If it can't, the customer may end the affected service without penalty.
- **Emergency replacement:** if a sub-processor must be replaced at once to protect the service or the data, the supplier tells customers as soon as it can, with the same right to object.
- **Flow-down:** every sub-processor is bound by a written contract with data protection terms at least as protective as the supplier's agreement with the customer (Article 28(4)). The supplier stays fully liable to the customer for each sub-processor.
- **Due diligence:** before using one, the supplier checks its security certifications, data location, breach terms and transfer safeguards, and records the result in [the supplier register; see [security-policies.md](security-policies.md), supplier management].

## Sub-processors that process customer personal data

| Sub-processor | Service | What it does with personal data | Data location | Safeguards | Since |
|---|---|---|---|---|---|
| **Microsoft** ([Microsoft contracting entity, as named in the supplier's Azure agreement]) | **Microsoft Azure:** Azure Kubernetes Service (where MedApp runs), Azure SQL Database (where the data is stored, with its backups), Azure Blob Storage (where attached files are stored), Key Vault (secrets), Azure Container Registry (the application image, no personal data), and, once step 5 is done, Azure Monitor (Application Insights and Log Analytics) | Hosts and stores all customer data: patient records, attached files (scans, photos, results, letters), the audit trail, staff accounts, sessions, notifications, logs and backups. Microsoft doesn't access it except for service operations and support under its terms. | **UK South** (London), with geo-redundant backups in **UK West** (Cardiff) once step 5 is done. Attached files are zone-redundant within UK South | Microsoft's Products and Services Data Protection Addendum, incorporated in the Azure agreement [to confirm the current version]; ISO/IEC 27001, 27017, 27018 and other certifications [to confirm current list]; see [international-transfers.md](international-transfers.md) for access from outside the UK | [Date of first customer deployment] |

**Email** is sent through **Azure Communication Services Email**, part of the Microsoft Azure service above, under the same agreement (built on `feature/email-2fa`, not yet deployed). Its data location is set to the **UK** (`emailDataLocation` in `deploy/azure/main.bicep`). It receives a staff member's work email address and username, and the email: a password reset or email confirmation link, or a notice of a change to their own sign-in. It never receives patient data or clinical text (`EmailTemplates`). Delivery onward to the staff member's mailbox goes through the customer's own mail provider.

**Planned additions** (each needs the 30 days' notice above):
- **A managed monitoring service** for out-of-hours alerts (roadmap step 5), if one is contracted. It would see alerts and logs. [Provider to choose.]

**Planned changes to what a sub-processor does:**
- **Microsoft Defender for Storage's malware scanning** of attached files (roadmap step 9, brought forward to before go-live, [dpia.md](dpia.md) risk 22). Microsoft is already a sub-processor, but scanning is a new operation on file contents, so customers are told before it's turned on. [Confirm that scanning happens in the storage account's region, UK South, and what Microsoft keeps about a scanned file.]

## Providers that don't process customer personal data

These are part of how MedApp is built and delivered, but they hold no customer personal data. They're listed so customers can see the whole picture.

| Provider | What it does | Why it holds no customer personal data |
|---|---|---|
| **GitHub** (owned by Microsoft) | Holds the source code, runs CI and the Publish workflow, stores the container image (GitHub Container Registry), and holds the `env/*` branches that Argo CD deploys from | Only code, configuration and images. Tests and demo data are synthetic (`AGENTS.md`). The GitHub Actions identity in Azure is limited to the registry (`AcrPush`, which can push and pull images), so it can't read the database or Key Vault (`deploy/azure/main.bicep`). Argo CD in the cluster reads from GitHub; nothing in the cluster writes to it. GitHub does hold personal data of the supplier's own developers (commit authors), which is the supplier's own processing ([ropa.md](ropa.md), B2.8). **But GitHub is in the supply chain:** code pushed there is deployed, so its security matters (DPIA risk 15). |
| **Let's Encrypt** | Issues the TLS certificate | It sees only the domain name. |
| **Open-source projects** (Traefik, cert-manager, External Secrets Operator, Argo CD, Valkey, SQL Server's image for local use, Azurite (Microsoft's storage emulator, for the kind cluster and development), SkiaSharp (which makes thumbnails inside the API), .NET, Node.js, React and the npm and NuGet packages) | Software MedApp runs | Software, not services: they run inside the supplier's Azure environment and receive no data. Their images and packages are downloaded from public registries at build or deploy time. |

## The supplier's own processors

The supplier also uses providers for its own business (email, accounts, a helpdesk), as the controller of its own data ([ropa.md](ropa.md), part B). They aren't sub-processors of customer data, because support tickets and email must not contain patient data ([support-access-policy.md](support-access-policy.md)). If customer personal data reaches one by mistake, it's handled as an incident ([breach-response.md](breach-response.md)).

## Change log

| Date | Change |
|---|---|
| 16 November 2026 | First draft |
| 16 November 2026 | Azure Communication Services Email added, within Microsoft Azure (`feature/email-2fa`) |
| 16 November 2026 | File attachments: Azure Blob Storage added to Microsoft's services (no new sub-processor); Defender for Storage's malware scanning planned; Azurite and SkiaSharp added to the open-source software |
