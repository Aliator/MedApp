# Review: PR #117, accepting and declining meeting invitations

**PR:** [#117](https://github.com/Aliator/MedApp/pull/117), branch `feature/meeting-replies`, reviewed at `0c82417` against `main` at `a5690b5` (the merge of #113, the last `main` merged into the branch). `main` has since moved to `1e1a33a` (the merge of #114); the branch merges with it without conflicts.

**Reviewed:** 18 November 2026, from the diff, the code around it and narrow test runs, independently of the PR description's conclusions.

**One finding is high:** the web app still counts a declined meeting as busy, so the person who declined can't select or book the time their decline freed, though the dialog tells them it's free (finding 1). Two are medium and five are low. **All eight are fixed in the PR**, each as its finding's "What happened to it" says.

## Summary

- **What it does:** staff can accept or decline a meeting they're invited to. A decline frees that time on the decliner's own calendar and tells the organiser. Moving a meeting wipes everyone's reply and asks them to reply again. The meeting dialog shows who replied what, and the calendar shows a declined meeting faded.
- **Verdict:** the server side is careful. Only the attendee can reply, replies are serialised with bookings by the same per-person lock, and accepting again re-checks the freed time. The notifications, migration, demo data and governance updates follow the house rules. But the web app keeps its own copy of the "what's busy" rule, and that copy wasn't changed, so the headline benefit (book into the time you declined) doesn't work in the UI.

## What changed, and why

67 files, 3,829 lines added and 105 removed, in 5 commits (one a merge of `main`). About 1,900 of the added lines are the migration's generated designer file.

### Replies stored on the attendee

- **What:** `MeetingAttendee` gains `Reply` (`MeetingReply`: `NoReply = 0`, `Accepted = 1`, `Declined = 2`) and `RepliedAtUtc`. The migration `AddedMeetingReplies` adds both, every existing attendee starting at `NoReply`.
  - **An analogy:** a sign-up sheet on the meeting room door, with a "going / not going" box beside each name.
- **Why a database default matters:** the new `Reply` column has `DEFAULT 0`, so an older copy of the API that doesn't know the column can still add attendees during a rolling update.

### The reply endpoint

- **What:** `PUT api/calendar/meetings/{id}/reply` with `{ reply: "Accepted" | "Declined" }`, open to staff, but only for meetings the caller is going to.
  - Someone not going is a **403**, admins and the organiser included: nobody answers for anyone else.
  - A cancelled meeting is deleted, so it's a **404**. One that has ended is a **409**. `NoReply` isn't a reply (**400**).
  - The same reply again changes nothing, and tells nobody again.
- **The lock:** the handler holds the replier's calendar lock (`ICalendarLock`, SQL Server's `sp_getapplock`).
  - **What that is:** one named lock per member of staff, held for the length of a database transaction. Bookings, blocks and meeting changes take the same lock.
  - **An analogy:** one pen per person's diary. Whoever holds it writes; everyone else waits their turn.
- **Not a change to the meeting:** a reply doesn't move `LastUpdated`, so an admin's open Change dialog isn't made stale by it.

### A decline frees the time

- **What:** `CalendarClashes.BusyTimes` leaves out a meeting the calendar's owner declined. Clash checks, free time, suggested meeting times and the team view's "Everyone free" all use it, so they all follow.
  - **An analogy:** crossing a meeting out in your own diary. It stays in everyone else's.
- **Accepting again** after declining re-checks that time on the replier's calendar, under the lock. If something was put there meanwhile, it's a 409 naming it, and the decline stands. That keeps the no-overlap rule true.

### Moving a meeting resets replies

- **What:** if a change moves the start or end, every reply goes back to `NoReply`, and "Meeting moved" ends "Please reply again." Everyone, decliners too, must then be free at the new time.
  - **An analogy:** a party moved to another day wipes the RSVPs, because "yes" was about the old day.
- **A change that doesn't move it** (title, notes, attendees) keeps replies, and skips decliners' calendars when checking clashes: the time isn't theirs to keep free any more.

### The organiser told of a decline

- **What:** `NotificationKind.MeetingDeclined = 7` (the first of the calendar kinds kept free), title "Meeting declined", message "k.ashford declined 'MDT', Tuesday 24 November, 10:00 to 11:00." Not sent when the organiser declines their own meeting, or when their account has gone. Accepting tells nobody.
- **Rules kept:** a fixed title (`Notification.HasFixedTitle` now covers 4 to 7), a message of usernames, the admin-written title and a time, and no free text: a reply has no reason field.

### The web app

- **`MeetingDialog`:** each attendee with a `MeetingReplyToken` (Accepted green, Declined red, No reply grey, with icons when "Icons on statuses" is on), under a summary such as "3 accepted, 1 declined, 2 haven't replied". An attendee gets **Accept** and **Decline**, a `ToggleButtonGroup` named "Your reply" (buttons with `aria-pressed`), pressed as they last replied. The reply is announced, and the dialog stays open showing what the API returned.
- **The calendar:** on the decliner's calendar the meeting is faded and says "Declined" instead of "N attending". Faded means plain (no tint), a dashed edge and drawn below busy items, with its words at full contrast.
  - **Why not opacity:** making a whole card see-through also fades its text below the 4.5:1 contrast WCAG asks for. **An analogy:** rather than greying out a sticky note, use a plain note with a dotted outline and keep the ink black.
- **"N attending"** leaves out those who declined. The bell has an icon for "Meeting declined".
- **`schema.d.ts`** was edited by hand rather than regenerated with `npm run gen:api`. The shapes match the C# DTOs.

### Demo data, docs and governance

- **Demo data:** `reset_demo_data` replays replies through the same `MeetingReplies` rules (mostly accepted for past meetings, a mix for upcoming ones), with the organiser told of each decline. Replies draw their own random numbers, so the rest of the story is unchanged.
- **Docs:** `AGENTS.md`, `MedApp.Web/AGENTS.md`, calendar plan decision 71, `docs/demo-data.md` and `docs/todo.md`.
- **Governance:** the data inventory, the DPIA (risk 25), the staff privacy notice, the rights procedure and the RoPA now say replies are staff data that everyone who can see a meeting can see.

## Design choices

- **The reply as two columns on the attendee row:**
  - **Instead:** a separate replies table, with a history of each change.
  - **Chosen because:** one reply per person per meeting, loaded with the attendees the calendar already reads; no new joins.
  - **Worse at:** there's no history. Only the latest reply and its time are kept, so "declined, then accepted" leaves no trace.
- **No version on a reply:**
  - **Instead:** send the meeting's `lastUpdated`, or the time the person saw, as meeting changes do (`expectedLastUpdated`).
  - **Chosen because:** a reply isn't a change to the meeting, so a title edit shouldn't refuse it.
  - **Worse at:** a reply sent from a screen opened before a move counts for the new time (finding 2).
- **Resetting replies on a move:**
  - **Instead:** keep replies across moves, or keep only declines.
  - **Chosen because:** an answer is about a time, so an answer for the old time shouldn't stand for the new one.
  - **Worse at:** in MedApp nothing may overlap. So a decliner who used the freed time blocks every move of the meeting until they're taken off it (finding 5).
- **Re-checking the time when accepting again:**
  - **Instead:** let accepting create an overlap, as a later template change may.
  - **Chosen because:** it keeps "no overlaps" true, and names what's in the way.
  - **Worse at:** it also refuses overlaps that were there before the decline (finding 4).
- **The attendee alone replies, even admins can't for others:**
  - **Instead:** let admins record a reply given in person.
  - **Chosen because:** a reply speaks for the person; it's simpler to audit with no "on behalf of".
  - **Worse at:** an admin can't fix a reply for someone away from MedApp; they can only take them off the meeting.
- **The web's own busy-time copy left as it was:**
  - **Instead:** change `calendarSlots.busyTimes` and the grid's "taken" rows with the server's rule.
  - **Worse at:** the UI and the server now disagree about what's free (finding 1).

## How it was checked

- **Unit tests, Release:** `dotnet test MedApp.UnitTests -c Release` filtered to the reply handler and validator, `MeetingReplies`, `UpdateMeetingHandler`, `CalendarClashes`, `MeetingMapper`, `MeetingNotifications`, `Availability`, `CalendarMapper`, `MeetingsController` and `DemoMeetings`: **318 passed**.
- **Web:** `npx vitest run` on `MeetingDialog`, `calendarItems`, `CalendarTokens`, `CalendarGrid`, `NotificationBell` and `calendarSlots`: **102 passed** in 6 files.
- **Migration:** `dotnet ef migrations has-pending-model-changes` in Release: no changes, so the model and snapshot agree. The migration is the newest, adds two columns, and drops them on the way down.
- **`main`:** `git merge-tree` with today's `main` (`1e1a33a`): no conflicts, and no new migrations there.
- **CI on the reviewed commit:** `unit-tests`, `helm` and `dependency-review` passed; `web`, `image`, `integration-tests` and `e2e` were still running when this was written.
- **By reading:**
  - **Authorisation:** the controller is behind the Staff policy; the command takes the user from the session (`User.ToCalendarActor()`), never from the body; `MeetingReplies.Reply` refuses anyone not on the meeting with a `ForbiddenException`, which the middleware turns into a 403. No user ID can be sent, so there's nothing to swap.
  - **Locks:** a reply takes one lock (the replier's); meeting creates and changes take every attendee's in a fixed order; bookings and blocks take the owner's. So a reply can't run during a change it's part of, and a decline and a booking into the freed time are serialised. A cancel takes no lock, but a reply racing it fails its save and becomes the usual 409.
  - **Notifications:** fixed title, no free text, never to the person who acted, and not repeated for a repeated decline.
  - **Demo seed:** replies come after the invitation or last move, before the meeting ends and before "now", only from people working then, never from the organiser, and no "accept after decline" (which the seed couldn't check).
  - **SSR:** nothing new reads the clock or `window` while rendering; `calendarItems` gives the same result on the server and in the browser.
- **Not done:** the integration tests, the browser, the axe audit and the UI parity screens. CI runs the first; nobody has done the others yet (finding 7).

## Findings

### 1. High: the web app still counts a declined meeting as busy, so its time can't be booked

- **Confidence:** high, by reading. No web test covers free time with a declined meeting.
- **What goes wrong:** a nurse declines Tuesday's 09:00 MDT meeting to fit in a patient. The dialog says "You declined: this time is free on your calendar." On the Calendar page, 09:00 to 10:00 can't be selected: the slots are marked taken, and a click lands on the faded meeting and opens it. New → Book Appointment doesn't list 09:00 to 09:30 either. The server would accept the booking (the integration test proves it), but the UI never offers it. In the team view, an admin can't book into that person's freed time either.
  - **An analogy:** the diary says the slot is free, but the booking clerk reads an old copy that still shows it taken.
- **Evidence:**
  - `MedApp.Web/src/utils/calendarSlots.ts:97-104`: `busyTimes` counts every meeting, with no reply check. `BookAppointmentDialog.tsx:226-230` (its free times) and `BlockDialog.tsx:124` (its default times) use it.
  - `MedApp.Web/src/components/calendar/CalendarGrid.tsx:325` and `:332-335`: a row is "taken" if any item covers it, faded items included.
  - `MedApp.Web/src/components/calendar/MeetingDialog.tsx:271-275` promises the opposite, as does decision 71 ("since that calendar's time is free for booking").
  - The only working routes are dragging an existing appointment there, or choosing a block's times by hand.
- **The fix:**
  - In `busyTimes`, skip meetings the calendar's owner declined (take `staffUserId` in its `Pick`, and move `hasDeclined` to a shared util).
  - In `CalendarGrid`, don't count faded items as taken, and make the slots under one reachable by pointer: for example, draw the faded card in a narrow lane at the column's edge, or let a press on it start a selection while a click or Enter still opens it.
  - Add tests: a declined meeting's time listed in `freeStartsByDay`, and selectable in the grid.
- **What happened to it:** fixed in the PR (`d8c59e28`).
  - `busyTimes` (`calendarSlots.ts`) leaves out a meeting the calendar's owner declined, with `hasDeclined` now in `src/api/calendar.ts`, so the booking dialog's free times and the block dialog's default times offer the freed time.
  - `CalendarGrid` doesn't count a faded item's rows as taken, so they select by pointer and keyboard. **How the card and the slots coexist:** the faded card keeps its place beside anything it overlaps, but takes only the last 40% of its share of the column's width (`FADED_SHARE`), so the rest shows the free slots under it. It still opens with a click on it, or Enter when it's focused (every item is a tab stop).
  - Tests that fail without the fix: `calendarSlots` (busy times and free starts), `BookAppointmentDialog` (09:00 and 09:30 offered under a declined meeting, not under an accepted one) and `CalendarGrid` (the slots under a declined meeting are free buttons and select; the card's width and place; it still opens).

### 2. Medium: a reply from a screen opened before a move counts for the new time

- **Confidence:** high.
- **What goes wrong:** a doctor opens Tuesday 10:00's meeting. Meanwhile an admin drags it to Thursday 14:00, which clears every reply and sends "Meeting moved … Please reply again." The doctor then clicks Accept in the still-open dialog. The server records Accepted for Thursday 14:00, a time the doctor never saw. The organiser sees an acceptance that wasn't given. With Decline, the doctor is quietly out of a Thursday meeting they may have been free for.
  - **An analogy:** replying "yes" to a Tuesday invitation that, unknown to you, became Thursday, and your "yes" counts for Thursday.
- **Evidence:**
  - `MedApp.Contracts/Calendar/Requests/MeetingReplyRequest.cs:7` carries only the reply; `MeetingsController.cs:84` and `ReplyToMeetingHandler.cs:36-49` apply it to whatever the meeting is now.
  - The dialog holds a copy taken when it opened (`CalendarPage.tsx:245` and `:318`), so the window is as long as the dialog stays open, not a millisecond race.
  - Decision 71 says the lock means "a reply can't cross a move … one before it is what they reply to". The lock orders the two, but doesn't make the reply about the time the person saw.
- **The fix:** send the start and end the person saw (`expectedStartUtc`, `expectedEndUtc`) with the reply. Under the lock, compare them with the meeting's, and refuse a mismatch with a 409 ("This meeting has moved to Thursday 26 November, 14:00 to 15:00 since you opened it."). The web app already refreshes calendars on a 409. Comparing times, not `lastUpdated`, keeps a title edit from refusing replies.
- **What happened to it:** fixed in the PR (`cf01422a`, `d3714ac6`).
  - The request carries `expectedStartUtc` and `expectedEndUtc` (required: a 400 without them). `MeetingReplies.Reply` compares them with the meeting's, under the replier's lock, after the 403 check: a mismatch is a 409, "This meeting has moved to Thursday 26 November, 14:00 to 15:00: look at its new time, then reply." Times, not `lastUpdated`, so a title edit still doesn't refuse a reply.
  - `MeetingDialog` sends the start and end it shows, and after a 409 reads the meeting again (`meetingQuery`) and shows it as it is now, so the next reply is about the new time; if it has gone, the message stays.
  - Tests that fail without it: `MeetingRepliesTests` (three kinds of move, a time with no kind, someone not going still a 403), `ReplyToMeetingHandlerTests`, `ReplyToMeetingValidatorTests`, `MeetingDialog` (the body sent; the dialog showing the new time after a 409, then replying for it) and an integration test (a reply from a copy read before a move is a 409, and one from the copy after it goes through).

### 3. Medium: a rollback leaves overlaps the old code can't handle, and there's no runbook

- **Confidence:** medium to high, from the old code on `main`.
- **What goes wrong:** after this ships, decliners book patients into time they declined. Then the release is rolled back by reverting it on `main` (the documented route). The database keeps the new columns (the default keeps inserts working), but the old code knows nothing of replies:
  - **Meetings can't be changed:** the old `UpdateMeetingHandler` checks every attendee's calendar, even for a title change, so any such meeting is a 409 naming the decliner's appointment until an admin takes them off.
  - **Calendars look double-booked:** the decliner shows going to the meeting and seeing a patient at once, with no "Declined" on screen, and "N attending" counts them.
  - **The safety reasoning is untrue in the data:** the device decision says "booking is refused over a meeting".
  - **An analogy:** going back to an old paper diary with no "declined" column. It sees double bookings it believes can't happen.
- **Evidence:** the migration's `defaultValue: 0` (`20261118020738_AddedMeetingReplies.cs:25`) covers the forward rollout only. Old `main`: `UpdateMeetingHandler.cs:62` checks all attendees, and `CalendarClashes.cs:88` counts every meeting. New code skips decliners: `UpdateMeetingHandler.cs:66-68`, `CalendarClashes.cs:90`. Decision 71 covers the rollout, not a rollback.
- **The fix:** add a rollback note to decision 71 (and the PR): after rolling back, take decliners off meetings still to come, so the old rule holds again (the SQL is under Detail). Or accept the overlaps knowingly and tell admins. Reverting the migration itself (`Down`) would also lose every reply.
- **What happened to it:** documented in the PR (`e401f734`). Decision 71 has a **Rolling back** bullet: what the old code does with decliners' overlaps, the SQL above to take decliners off meetings still to come, telling admins, cancelling any meeting left empty, and not reverting the migrations (their `Down` loses every reply). `deploy/helm/medapp/README.md`'s Migrations list says the same and points there, as does `AGENTS.md`.

### 4. Low: accepting again is refused for an overlap that was there before declining

- **Confidence:** high.
- **What goes wrong:** an admin moves everyone's Lunch to 12:30, over a 12:00 to 13:00 meeting. That's allowed: "a later template change can overlap what's already there." A nurse declines, then changes her mind. She gets "That time has been used since you declined. That time overlaps Lunch, 12:30 to 13:00." Nothing was used since. She must change that day's lunch first. The same happens to a mis-clicked Decline.
- **Evidence:** `ReplyToMeetingHandler.cs:64-68` checks every clash, template blocks included; `MeetingReplies.cs:19` has the message.
- **The fix:** leave template blocks out of the re-accept check (they may overlap by design), or reword the message to "That time is no longer free on your calendar:" and name the clash.
- **What happened to it:** fixed in the PR (`cf01422a`). The refusal now says "You can't accept it while something else is on your calendar at that time: That time overlaps Lunch, 12:30 to 13:00." It names what overlaps and never claims it came after the decline: with finding 5's fix, a move can also keep the decline of someone busy at the new time, so "used since you declined" could be untrue for more than template blocks. The check itself stays (accepting can't create an overlap); a template block in the way is changed for that day first, as for any other clash. Tested in `ReplyToMeetingHandlerTests` and the integration tests.

### 5. Low: a decliner who used the freed time trips up the Change dialog and every move

- **Confidence:** medium to high.
- **What goes wrong:** a decliner books a patient at 09:30, inside a 09:00 to 10:00 meeting they declined.
  - **The Change dialog warns wrongly:** an admin opens Change to fix a typo in the title. The dialog says "Not everyone is free then" and names the decliner's appointment, at the meeting's own time. Saving would work, because a change that doesn't move a meeting skips decliners. The admin may give up or move it needlessly.
  - **Moves are refused:** moving the meeting, even by 15 minutes, is a 409 naming the decliner. They said they aren't coming, yet they block it until taken off.
- **Evidence:** `UpdateMeetingHandler.cs:66-68` (moves check everyone); `MeetingDialog.tsx:330-352` (busy times from availability, with the meeting itself left out but the decliner's appointment counted); `GetAvailabilityHandler.cs` (busy from `CalendarClashes.BusyTimes`).
- **The fix:** in the Change dialog, while the time is unchanged, leave out decliners' busy time (or say "k.ashford declined"). For moves, either explain in the 409 ("k.ashford, who declined, has …; take them off to move it") or keep a decliner's decline when they're busy at the new time.
- **What happened to it:** fixed in the PR (`cf01422a`, `d3714ac6`).
  - **Server:** those who declined are never checked when a meeting changes, moved or not (`UpdateMeetingHandler`). On a move, decliners busy at the new time keep their decline (`MeetingReplies.ResetAfterMove`): they still aren't going, and asking again would only clash. Their "Meeting moved" says "You have something else on then, so your decline stands." Decliners free at the new time are asked again like everyone else.
  - **Change dialog:** those who declined are left out of who must be free (its suggested times and "Not everyone is free then"), unless nobody else is going.
  - Tests that fail without it: `UpdateMeetingHandlerTests` (a move over a decliner's appointment is saved, keeping their decline and telling them so; a decliner free at the new time is asked again; someone going who's busy still refuses it), `MeetingRepliesTests`, `MeetingNotificationsTests`, `MeetingDialog` (the availability asked about leaves decliners out) and an integration test.

### 6. Low: "Meeting declined" opens a calendar that may not show the meeting

- **Confidence:** high.
- **What goes wrong:** an admin arranges a meeting for others and unticks "I'm going too". Someone declines. The admin clicks the notification and lands on their own calendar that day, where the meeting isn't, so they can't see who declined without hunting for it.
- **Evidence:** `NotificationBell.tsx:26-27` sends every meeting notification to `/calendar?date=…&view=day` (the viewer's own calendar); `MeetingNotifications.cs` sends `MeetingDeclined` to the organiser whether or not they're going.
- **The fix:** for `MeetingDeclined`, open the team view that day, or open the meeting itself (a `meeting` search parameter that opens its dialog).
- **What happened to it:** fixed in the PR (`cf01422a`, `d3714ac6`). Meeting notifications carry the meeting's ID (`Notification.MeetingId`, a new nullable column with no foreign key, in the migration `AddedMeetingIdToNotifications`; a cancellation has none, as there's nothing to open). The bell links to `/calendar?date=…&view=day&meeting=<id>`, and the Calendar page opens that meeting's dialog from the `meeting` parameter, even when it isn't on the calendar shown; one that's gone says "That meeting couldn't be found. It may have been cancelled." Invitations and moves open the meeting too, so an attendee can reply from the notification. Tested in `MeetingNotificationsTests`, `NotificationMapperTests`, `NotificationBell`, `CalendarPage`, `calendarSearch` and the integration tests. The data inventory lists the new column.

### 7. Low: not yet looked at in a browser, and no parity screen covers the new UI

- **Confidence:** high.
- **What goes wrong:** the reply buttons, the tokens and the faded meeting are visible changes. `AGENTS.md` asks for a look in a browser and the axe audit on the screens they touch. The PR says "Browser: before merging", and `tools/ui-parity/screens.ts` has only New Meeting (`:171`), so the audit can't reach an existing meeting's dialog or a declined meeting at all.
- **The fix:** add screens for an attendee's meeting dialog (with replies) and a calendar with a declined meeting (the demo data now has both), capture them, and run `npm run a11y -- -g "<screen>"`.
- **What happened to it:** fixed in the PR (`e401f734`). `tools/ui-parity/screens.ts` has `meeting-replies` (an attendee's meeting dialog, opened from `?meeting=`, with the replies, the summary, and Accept and Decline) and `calendar-declined-meeting` (the faded card on the calendar, its time free). The demo data has no meeting of the parity account's own, so `auth.setup.ts` finds or makes one ("Parity check meeting (synthetic)", at its first free half hour in the next four weeks, nobody else going, so nobody is told) and declines it; both screens skip without it. The files type-check. Capturing the screens and running `npm run a11y -- -g "meeting-replies|calendar-declined-meeting"` need the AppHost, which this fix didn't start: that's the browser check before merging.

### 8. Low: the device decision now says something untrue

- **Confidence:** high.
- **What goes wrong:** `docs/clinical-safety/intended-purpose-and-device-decision.md:145` justifies "no new hazard" with "a meeting is refused wherever an attendee has an appointment, and booking is refused over a meeting." With replies, a booking may go into a declined meeting's time, and a change that doesn't move a meeting skips decliners. Decision 71 says no hazard changes, but the safety file wasn't updated, so a clinical safety officer reading it gets the old rule.
- **The fix:** add a sentence (a booking may go into the time of a meeting the person declined, which they aren't attending, so no visit is hidden) and a version row.
- **What happened to it:** fixed in the PR (`e401f734`). `docs/clinical-safety/intended-purpose-and-device-decision.md` (version 0.8) now says the meeting rule holds for everyone going, and has a paragraph on replies: a booking may go into the time of a meeting the person declined, which they aren't going to, so it hides no visit; the decision stands and no hazard changes.

## Remaining risks

- **No history of replies:** only the latest reply and its time are kept. A dispute ("I never declined") has no record. The activity log doesn't record replies either (they're staff data, not patient data).
- **Repeated declines notify again:** decline, accept, decline tells the organiser twice. Harmless today; a reason to rate-limit if replies get noisier.
- **"Please reply again"** goes to everyone still going after a move, including those who never replied.
- **The seed's test is weaker:** `DemoDataSeedTests` now skips decliners' calendars, though the seed promises nothing was booked into declined time. It could still check them.
- **Old copies of the API during a rollout** count declined meetings as busy and don't reset replies on a move, for the minutes both versions run.
- **Enum parsing quirk, from before:** `ProcedureEnums.TryParse` refuses a leading digit but not a sign, so `"+2"` is accepted as `Declined`. Harmless, but not "by name only".

## Say it like this

> "Staff can now accept or decline a meeting. A decline frees that time on their own calendar, and moving the meeting resets everyone's answer, because an answer is about a time. Replies use the same per-person database lock as bookings, so a decline and a booking into the freed time can't race. The trade-off is that the browser kept its own copy of the busy-time rule, which the review caught, and a reply carries no version, so one sent from a stale screen counts for the meeting's new time."

## Follow-up review

**Reviewed:** 18 November 2026, at `d2c9c615` (the fixes `d8c59e28` to `b21aff90`, and this document), against `origin/main` at `1b00eae` (the merge of #115). Checked from the code, not from the "What happened to it" lines.

**Verdict:** every finding's fix holds up in the code, and its tests would fail without it. Nothing new is high or critical. **One new medium:** `main` has moved on (#115 merged), the branch now conflicts with it in ten files, and `main`'s new migration sorts between this PR's two.

### The original findings

- **1 (High), fixed.** `busyTimes` skips a meeting the calendar's owner declined (`MedApp.Web/src/utils/calendarSlots.ts:103`, with `hasDeclined` moved to `src/api/calendar.ts:38`). Every caller passes a calendar from the API, which carries `staffUserId` (`BlockDialog.tsx:124`, `BookAppointmentDialog.tsx:226-229`). The grid leaves faded items out of its taken rows (`CalendarGrid.tsx:326`), and the faded card takes only the end 40% of its share (`:1034`, `:1086-1093`), so the slots beside it can be clicked. The team view fades per member (`calendarItems.ts:165` passes each calendar's `staffUserId`). The new tests in `calendarSlots.test.ts`, `CalendarGrid.test.tsx` and `BookAppointmentDialog.test.tsx` fail on the old code.
- **2 (Medium), fixed.** The times seen are required (`ReplyToMeetingValidator`) and compared under the replier's lock (`ReplyToMeetingHandler.cs`, `MeetingReplies.cs:62-66`, after the 403 check). `UkTime.Utc` handles a time sent without a `Z` or with an offset. The dialog sends the times it shows (`src/api/calendar.ts:353`), and after a 409 it reads the meeting again (`MeetingDialog.tsx:121`, `:128-134`). `meetingQuery`'s key is under `calendar`, so the refresh after a reply covers it too. Unit, Vitest and integration tests (`MeetingReplyTests.cs:227`) all cover it.
- **3 (Medium), documented.** The **Rolling back** step is in decision 71 (`docs/calendar-plan.md:150`), the chart README (`deploy/helm/medapp/README.md:104-106`) and `AGENTS.md`. The SQL is right. Old copies show a `MeetingDeclined` (7) notification as kind "7" with no icon, which is harmless.
- **4 (Low), fixed.** The message names the clash without claiming when it came (`MeetingReplies.NotFreeToAccept`), and an integration test checks the exact text (`MeetingReplyTests.cs:159`).
- **5 (Low), fixed.** `UpdateMeetingHandler.cs:69-75` checks only those going. On a move, decliners busy at the new time keep their decline. Decliners free then are put back to no reply, which is safe, since they were just checked under their locks (`:106-118`). Their "Meeting moved" says which (`MeetingNotifications.cs:79`). The Change dialog leaves decliners out of availability unless nobody else is going (`MeetingDialog.tsx:348-351`). If everyone has declined, the dialog may still warn about them, but the warning is advisory and saving works. The demo seed resets every reply on a move without that check (`MeetingStory.cs:88`), but it gathers replies only after the last change, so it never has one to keep.
- **6 (Low), fixed.** `Notification.MeetingId` is set on every meeting notification except a cancellation (`MeetingNotifications.cs:127`). The bell adds `&meeting=` (`NotificationBell.tsx:28`), and the Calendar page opens that meeting even off the calendar shown, or says it's gone (`CalendarPage.tsx:276-277`, `:711-715`). The migration is additive and nullable. `Down` drops only the links. The snapshot is in step with the model (`has-pending-model-changes`: no changes). The ID is not personal data, and the data inventory lists the column.
- **7 (Low), fixed in part.** The screens and the setup that makes and declines a synthetic meeting are there. Nothing has been captured yet, and the axe audit hasn't run, so the browser check before merging is still to do, as its line says. The headline "all eight are fixed" says more than that.
- **8 (Low), fixed.** Version 0.8 of the device decision has a "Replies to meetings" paragraph (`intended-purpose-and-device-decision.md:146`) that matches the code: decliners aren't checked, and a move can keep a decline.

**Stored numbers:** `MeetingDeclined = 7` and `MeetingReply` 0 to 2 don't clash with `main`. `main`'s new kinds are 18 to 21 (`NoteDeletionRequested` to `AuthorsNoteDeleted`).

**Checked here:** 166 unit tests (replies, the reply command, changing a meeting, meeting notifications, the notification mapper, clashes), all passing in Release. Six Vitest files (126 tests), all passing.

### New findings

#### 9. Medium: the branch conflicts with `main`, and `main`'s migration lands between this PR's two

- **Confidence:** high for the conflicts and the order. Medium for how much the order matters.
- **What goes wrong:** #115 merged after the fixes. `git merge-tree HEAD origin/main` reports conflicts in ten files. GitHub can't build the merge ref for a conflicting PR, so CI won't run until the conflicts are resolved. Two parts are easy to get wrong without anything failing:
  - **Migration order.** `main`'s `20261118025735_AddedNoteDeletion` sorts between `20261118020738_AddedMeetingReplies` and `20261118033641_AddedMeetingIdToNotifications`.
    - **Out of order on existing databases:** kind's database and the local dev database already have `AddedNoteDeletion`. EF Core applies the older-dated `AddedMeetingReplies` after it without complaint.
    - **Not linear:** the history is no longer the single linear one `AGENTS.md` (Migrations) asks for.
    - **A partial rollback:** `database update AddedNoteDeletion` would undo only `AddedMeetingIdToNotifications` and leave the replies' columns in place.
    - **An incomplete model:** `AddedMeetingIdToNotifications`' designer has no model of `main`'s new tables.
    - **No test catches it.**
    - **An analogy:** two people each adding pages to a dated logbook. One page gets slipped in between the other person's two, after some readers have already read past that point.
  - **`Notification.HasFixedTitle`.** Both sides changed it. The merged version needs `<= MeetingDeclined` and `|| IsNoteDeletion(kind)`. `MeetingNotificationsTests.cs:26` and `NotificationPreferencesTests` would catch a mistake, so this part is safe if the tests run.
  - **Also conflicting:**
    - `NotificationResponse`/`NotificationMapper`: both add a nullable positional parameter. Keep both, and keep the mapper's arguments in the same order.
    - `NotificationBell.hrefOf`: keep `main`'s note-deletion branch and add `&meeting=`.
    - `schema.d.ts`: regenerate it.
    - The data inventory's `Notification` row: list both columns.
    - `AGENTS.md`, `MedApp.Web/AGENTS.md`, `docs/governance/README.md`, `docs/reviews/README.md`.
- **Evidence:** `git merge-tree --write-tree HEAD origin/main` lists the conflicts. The migration file names are in `MedApp.Infrastructure/Migrations/` on each side. `main`'s `Notification.cs` ends `HasFixedTitle` at `MeetingCancelled`.
- **The fix:**
  - **Back up the dev database:** take a `medapp-dev` `save_snapshot`.
  - **Undo this PR's migrations there:** `dotnet ef database update AddedNotepads`.
  - **Merge `main`:** resolve the files above.
  - **Remove and re-add the migrations:** delete this PR's two migrations, put back `main`'s snapshot, and re-add them on top of `AddedNoteDeletion`. The re-added migrations must keep the `Reply` default of 0, so read their `Up`.
  - **Check:** `has-pending-model-changes` reports nothing, and the history goes `AddedNoteDeletion`, then the replies.
  - **Rename instead, if you prefer:** rename both migrations to later timestamps and regenerate their designers.
- **What happened to it:** Not yet.

### Remaining risks after the follow-up

- **Browser check:** finding 7's capture and axe audit are still to do before merging.
- **Narrow columns:** on a narrow week or team column, the faded card's 40% may show only its start time. That's by the grid's own fitting rules, but nobody has looked at it yet.

## Detail

### Finding 1: the shape of the fix

```ts
// MedApp.Web/src/utils/calendarSlots.ts
export function busyTimes(
  calendar: Pick<Calendar, 'blocks' | 'appointments' | 'meetings' | 'staffUserId'>,
  ignoreAppointmentId?: string,
): Interval[] {
  const meetings = (calendar.meetings ?? [])
    .filter((m) => m.startUtc && m.endUtc && !hasDeclined(m, calendar.staffUserId))
    .map((m) => intervalOf(m.startUtc!, m.endUtc!));
  // ...
}

// MedApp.Web/src/components/calendar/CalendarGrid.tsx, in the column layout
const busy = column.items.filter((i) => !i.isFaded).map((i) => intervalOf(i.startUtc, i.endUtc));
```

The faded card still covers its slots for the pointer, so a click there opens the meeting. That part needs a layout decision, not just the filter.

### Finding 2: the shape of the fix

```cs
// MeetingReplyRequest
public DateTime? ExpectedStartUtc { get; set; }
public DateTime? ExpectedEndUtc { get; set; }

// ReplyToMeetingHandler, under the lock, before MeetingReplies.Reply
if (tracked.StartUtc != expectedStart || tracked.EndUtc != expectedEnd)
    throw new ConflictException(
        $"This meeting has moved to {UkTime.DescribeRange(tracked.StartUtc, tracked.EndUtc)} since you opened it.");
```

The web app sends `meeting.startUtc` and `meeting.endUtc` from what the dialog shows.

### Finding 3: a rollback step

After going back to an API from before #117 (the columns stay), take decliners off meetings still to come, so every attendee's time is busy again, as the old code assumes:

```sql
DELETE a
FROM MeetingAttendees a
JOIN Meetings m ON m.Id = a.MeetingId
WHERE a.Reply = 2 AND m.EndUtc > SYSUTCDATETIME();
```

A meeting left with nobody should then be cancelled by an admin (the app deletes one itself when its last attendee's account goes). Nobody is told by this SQL, so tell admins.

### Checks of the main concerns that found nothing

- **IDOR (acting on someone else's record by changing an ID):** the only ID sent is the meeting's; who replies always comes from the session.
- **Deadlocks:** one lock per reply, so it can't be part of a lock cycle with the multi-lock meeting changes.
- **Accept after decline, racing a booking:** both take the replier's lock, so one waits and then sees the other's result.
- **Notification rules:** `MeetingDeclined` has a fixed title, carries no free text, sits inside `HasFixedTitle`'s range and outside `IsMonitoring`'s, and the anonymiser leaves it alone.
- **Migration:** additive, defaulted, newest in the chain, and in step with the model.
- **Governance:** what's held, who sees it and how long it's kept are stated in the inventory, DPIA, notice, rights procedure and RoPA.
