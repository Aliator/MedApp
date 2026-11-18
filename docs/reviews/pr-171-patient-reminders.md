# Review of #171: patient reminders and the Did not attend list

- **PR:** https://github.com/Aliator/MedApp/pull/171 (`feature/patient-reminders`), reviewed at `322c4ded`, 18 November 2026. The review covered privacy and safety.
- **What it does:** texts and emails to patients (reminders, moves, cancellations, missed appointments) are stored rows (`PatientMessages`), made and sent by the `patient-reminders` job through an Off, Offline or Live (GOV.UK Notify) sender. It also adds the Did not attend list and its follow-up (`dna-follow-up`), and the patient's reminder choice. Exported, anonymised, governance updated.
- **No critical or high findings.** All five findings below are fixed in this PR.

## Findings

1. **Medium: duplicate sends.**
   - **What was wrong:** the Notify typed client inherited the service defaults' `AddStandardResilienceHandler` (`MedApp.ServiceDefaults/Extensions.cs`), which retries a POST after a timeout. Notify has no idempotency key, so a slow but accepted send could go twice. The job's own 5 tries added more.
   - **Fixed:**
     - Notify is now a named client (`NotifyPatientMessageTransport.ClientName`), and the API removes the resilience handler from it with `RemoveAllResilienceHandlers()`, as for `ssr`.
     - An `HttpClient` timeout is now `PatientMessageSendResult.Unconfirmed`, so the job marks the message `Failed` / `Unconfirmed` and never sends it again. The web app explains the reason.
     - `NotifyClientTests` runs the client the API registers against a real local server: a failing POST is made once, and a timeout is unconfirmed after one request. There are also job and transport unit tests.
2. **Medium: stale site.**
   - **What was wrong:** the text, which names the site, is fixed when made, and `PatientMessageRules.WhyNotToSend` checked only status and start. A room moved to another site at the same time still sent the old address, and the outbox's existence check ignored the room.
   - **Fixed:**
     - `PatientMessage.AppointmentRoomId` is in the unique index (unfiltered).
     - A different room makes the message `NoLongerApplies`.
     - The outbox and the job's reminder query match on the room, so a room move gets a new Moved message and a new reminder.
     - Covered by a rules test and an integration test (`MovedToAnotherSitesRoomAtTheSameTime_TellsThePatientTheNewPlace`).
3. **Low: a send could overwrite an anonymisation or restriction made mid-run.**
   - **Fixed:**
     - The appointment and patient are read again just before each send.
     - `PatientMessage.RowVersion` was added.
     - `PatientMessageRepository.SaveAsync` reloads a conflicting row, so the other change wins.
     - Job test: a restriction made during a run stops the rest.
4. **Low: Offline mode could email patient details through a real SMTP server.**
   - **Fixed:** `PatientMessages:OfflineRecipient` must end `.invalid`, `.test` or `localhost`. This is checked at startup (`ValidateOnStart`) and again on each send. Tests added.
5. **Low: a NotSent row blocked a later opt-in.**
   - **Fixed:**
     - The reminder query treats a reminder slot holding only `NotSent` rows as free once the patient can be reached (a channel chosen with its contact).
     - `AddAsync` deletes those rows, then inserts the new ones.
     - Integration test: `Reminders_PatientOptsInLater_ReplacesTheNotSentOne`.

## Remaining risk

- **An unconfirmed message isn't looked up afterwards** by its reference (Notify's `GET /v2/notifications?reference=`), so staff see "not confirmed" even when it was delivered.
- **Moving room A to B to A to B at the same time** sends no third Moved message, because the (start, B) message already exists.
- **Notify's terms, data location and retention** are still to confirm (`docs/governance/sub-processors.md`).
