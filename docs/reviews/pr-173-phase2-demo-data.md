# Review: PR #173, Phase 2 demo data

**PR:** [#173](https://github.com/Aliator/MedApp/pull/173), branch `feature/phase2-demo-data`, reviewed at `2b25456` on 18 November 2026. A quick skim of `ReferralTexts.cs` and the wipe change; the 1,400 lines of story code weren't read. CI (full tier) passed: https://github.com/Aliator/MedApp/actions/runs/44359532171. No finding is high or critical.

The demo seed now replays 18 months of referrals, triage and waiting-time clocks through the app's shared code: 701 referrals (most accepted into the clinic's pathways, the rest rejected, redirected, withdrawn or entered in error), an inbox of 20 awaiting triage (3 breached, one specialty nobody can triage), paused, resumed, stopped and breached clocks, triage notifications and new saved searches.

**Checked:** no realistic personal data in the added text (invented practices without ODS codes, GPs as initial and surname, no phone numbers, postcodes or NHS numbers, no real hospitals); the wipe deletes `ReferralClockPauses` before `Referrals` (`DemoWipe.cs:41`), inside the existing block, demo tables only.

**Finding, low, fixed in the PR:** "Fallowfield Green Health Centre" used a real Manchester district; renamed to the invented "Thornwick Vale Health Centre".

**Remaining risk:** that every row goes through `ReferralChange`, `WaitingClock` and `PathwayCreation` rests on the PR's new seed integration tests, not on this read.
