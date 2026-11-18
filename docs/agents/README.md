# Area guides for AI coding agents

The detail behind the root `AGENTS.md`, one file per area, read on demand. `AGENTS.md` is loaded into every session and subagent, so it keeps only what every change needs, plus a table (Read before you change) saying when each file below must be read.

- **Read only:** these files are read by agents, never by a build or test, so CI's `changes` job can treat a push of only `docs/` as needing no tests. Keep it that way: put nothing here that a build or test reads.
- **Briefing a subagent:** name the files it needs, rather than pasting their content.
- **Size:** no hard limit, but new detail should be as short as the rules need. The core's limit and how to move detail are in `AGENTS.md` (Keeping this file small).

| File | Covers |
|---|---|
| [pathways.md](pathways.md) | Pathways, templates and stages, pathway notes and note deletion |
| [specialties.md](specialties.md) | Specialties, each template's and pathway's specialty, and staff memberships |
| [referrals.md](referrals.md) | Referrals, their history and letters, the inbox, and their privacy rules |
| [specimens.md](specimens.md) | Specimens, the lab's worklist, the label, and the specimen setup lists |
| [letters.md](letters.md) | Letter templates, merge fields and the PDF engine |
| [timeline.md](timeline.md) | The patient timeline, and what staff and admins each see |
| [notifications.md](notifications.md) | In-app notifications, their kinds and live delivery |
| [calendar.md](calendar.md) | Calendars, templates, meetings, appointments and stage visits |
| [notepad.md](notepad.md) | Each member of staff's notepad |
| [monitoring.md](monitoring.md) | Admins' monitoring rules |
| [attachments.md](attachments.md) | File attachments |
| [jobs.md](jobs.md) | Scheduled jobs: the runner, the lease, and the operational purge |
| [privacy.md](privacy.md) | The activity log, restriction, export, anonymisation and retention |
| [insights.md](insights.md) | Admins' Insights page |
| [nhs-fhir-snomed.md](nhs-fhir-snomed.md) | The NHS number, FHIR, SNOMED CT and the demographics lookup |
| [auth.md](auth.md) | Sign-in, sessions, throttling, passwords, security headers and roles |
| [email-2fa.md](email-2fa.md) | Email, password reset and two-factor authentication |
| [dev-mcp.md](dev-mcp.md) | The `medapp-dev` tool and the demo data |
| [local-dev.md](local-dev.md) | Local containers, memory, server-side rendering and the production image |
| [testing.md](testing.md) | Integration and end-to-end tests |
| [ci.md](ci.md) | CI jobs, coverage, CodeQL, Dependabot and the supply chain rules |
| [deploy.md](deploy.md) | kind, Argo CD, Azure and the Publish workflow |
