# PR #190: Phase 3 to 5 demo data

https://github.com/Aliator/MedApp/pull/190 (`feature/demo-data-phases-3-5`), reviewed 18 November 2026 (quick review), `main` merged at `800c6dee`.

Seeds Phases 3 to 5 (plan PRs 17, 28 and 39, less what was trimmed from Phase 5) in `tools/MedApp.DevMcp/DemoData` only:
- **Setup through the app's commands:** staff full names and job titles, 8 GP practices, sites, rooms and their hours, part-time staff hours, 2 consent forms, 20 letter templates.
- **Patients:** contact details, practices and reminder choices.
- **In the replay, from ten weeks ago** (`DemoTimeline.LabAndLettersFromUtc`), through the shared code (`SpecimenChange`, `ResultChange`, `ConsentChange`, `MdtAudit`, `StageFinishing`, `StageGates`):
  - specimens, results and their sign-off, and consent;
  - skin lesions on the skin cancer MDT;
  - the skin lesion template's Benign/Treatment branches, and optional stages skipped and reinstated;
  - decisions after a Did not attend.
- **After the save, through the commands:** discharge summaries and letters in every state, clinic patterns and sessions with bookings and one cancellation, appointment reminders from `PatientMessageFactory`, and one open data request with its identity check.
- **Tests:** `DemoDataSeedTests` gains `PhasesThreeToFive_FollowTheAppsRules`. The clash check ignores an appointment's own clinic session, as the app does.

Findings: none.
- The data is synthetic: phones from the Ofcom drama ranges (01632 960xxx, 07700 900xxx), postcodes in the non-existent `ZZ` area, invented practices with no ODS codes, NHS numbers only from the 999 range, `.example` email domains.
- No production code changed. The `DiscardedFiles` store is registered only in the seed's own service container (`SeedServices`), and only when no attachment store answers. The Development `IHostEnvironment` is `AddDevMcpCore`'s (from #185); the seed's copy was removed.
- The specimen sequence restart builds its SQL from a constant name and an integer only.

Remaining risk: the counts and the records `tour.centre` names vary with the day the seed runs. A special case can be missing when its story hasn't reached that point yet; the summary shows what exists. Without Azurite, report and letter files keep their records without their bytes.
