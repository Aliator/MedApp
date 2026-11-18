# Review of PR #176: letters

[PR #176](https://github.com/Aliator/MedApp/pull/176), branch `feature/letters`, a quick review on 18 November 2026. **No critical or high findings.**

## Summary

Letters (PR 16 of `docs/centre-phases-3-5-plan.md`): written from a template by staff, approved by a named clinician, made into a PDF, and sent by secure email from MedApp or recorded as posted or handed over; withdrawn and corrected; the letter gate on stages; "Ask for more information" on referrals; unused templates deleted.

## How it was checked

- **Off by default:** secure email needs `Letters:SecureEmail` with a host and a from address **and** the organisation's DCB1596 declaration (`RelayMeetsDcb1596`); otherwise `ILetterTransport.IsAvailable` is false and nothing is emailed.
- **Exact-domain check:** an address must be in `Letters:SecureEmailDomains` (`nhs.net` by default) or a subdomain of one (`PracticeRules.IsInAllowedDomain`), and never the patient's.
- **Approval before send:** only an approved letter's stored PDF is sent or recorded; drafts have only a preview marked "Draft, not approved".
- **No addresses or text in logs:** a refused email logs its category and exception type only; the audit holds IDs, kinds and methods, never the text, comment, reason or anyone's name or address.

## Findings

- **Low: self-approval** (confidence high). A clinician holding `ApproveLetters` approves their own draft. By design (decision 102); the audit records `selfApproved`. Left as it is.
- **Low: an unencrypted relay outside Development** (confidence high). `Letters:SecureEmail:Security = None` would have sent letters in the clear on a deployment configured that way. **Fixed:** `SecureEmailLetterTransport.IsAvailable` is false for `None` outside Development, as for a missing declaration (`Transport_AnUnencryptedRelay_IsOnlyForDevelopment`).

## Not reviewed

`LetterNotifications`' wording, the timeline's mapping of letter entries, and the audit details' exact contents (the staff canary test covers their free text).
