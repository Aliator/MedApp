# Cookies and browser storage

**Draft prepared for review. It is not legal advice and must be reviewed and approved by a qualified data protection professional and the organisation's Data Protection Officer before use.**

| | |
|---|---|
| **Version** | 0.1 (draft), 16 November 2026 |
| **Checked against** | `MedApp.API/Common/Authentication/SessionCookieService.cs`, `SessionAuthenticationDefaults.cs`, and the storage keys in `MedApp.Web/src` on `main` as of 16 November 2026 |
| **Next review** | With any change that adds a cookie or a storage key |

The Privacy and Electronic Communications Regulations (PECR), regulation 6, require consent before storing or reading information on a user's device, unless an exception applies. This statement lists everything MedApp stores in the browser, and the exception relied on. Customers can publish the statement in section 3 to their staff, for example with the staff privacy notice.

## 1. What MedApp stores

**One cookie, no tracking.** MedApp sets no analytics, advertising or third-party cookies, loads no third-party scripts, and doesn't fingerprint devices.

| Name | Type | What it holds | Lasts | Set by |
|---|---|---|---|---|
| `medapp.session` | Cookie: first-party, HttpOnly (script can't read it), `SameSite=Lax`, `Secure` outside development | The ID of the user's sign-in session | Until the session ends: sign-out, or 8 hours after sign-in, or later if the user chooses "Stay signed in" (24 hours after sign-in at most) | The API, at sign-in (`SessionCookieService.cs`) |
| `medapp.colorMode`, `medapp.colours`, `medapp.textSize`, `medapp.accessibility` | `localStorage` | Light or dark mode, colours, text size, and the accessibility switches | Until cleared | The React app, when the user changes them (`MedApp.Web/src/app`) |
| `medapp.pageSize`, `medapp.proceduresStartOnMine`, `medapp.startPage`, `medapp.navClosesHalfView`, `medapp.navFlyoutDelay` | `localStorage` | List and navigation preferences | Until cleared | As above |
| `medapp.favourites` | `localStorage` | Saved searches and quick filters. **The terms can be patient names.** | Until cleared | As above (`favourites.ts`) |
| `medapp.pins` | `localStorage` | The IDs of the patients and procedures a user has pinned in the side navigation. (Older versions kept them under `medapp.pinnedPatients.<username>` and `medapp.pinnedProcedures.<username>`; they move to `medapp.pins` and the old keys are removed at sign-out.) | Until sign-out | `MedApp.Web/src/components/navigation/pins.ts` |
| `medapp.halfView` | `sessionStorage` | The page open in the half view, which can include a patient or procedure ID | Until the tab closes or the user signs out | `MedApp.Web/src/components/navigation/AppLayout.tsx` |

The settings and pins are also stored with the account on the server (`UserSettings`), so they follow the user between computers; the half view is kept only in the browser.

**Signing out clears every `medapp.` key** in `localStorage` and `sessionStorage` except the four visual settings (`medapp.colorMode`, `medapp.colours`, `medapp.textSize`, `medapp.accessibility`), so the sign-in page keeps looking the way the user set it. Signing in again brings the rest back from the account (`clearStorageAtSignOut`, `MedApp.Web/src/app/settingsStore.ts`). A session that only expires, without signing out, leaves them in the browser until the next sign-out.

## 2. The exceptions relied on

- **The session cookie** is **strictly necessary** to provide the service the user asked for: without it, nobody can sign in. No consent is needed ([ICO](https://ico.org.uk/for-organisations/direct-marketing-and-privacy-and-electronic-communications/guidance-on-the-use-of-storage-and-access-technologies/what-are-the-exceptions/)).
- **The preferences, saved searches, pins and half view** are stored only because the user chose them, to make the service work the way they asked. The supplier relies on the **strictly necessary** exception for functions the user explicitly requests, and on the exception the Data (Use and Access) Act 2025 added to PECR for storage that **adapts a service's appearance or functions to the user's preferences**. [Confirm against the ICO's current guidance on the DUAA's PECR exceptions.]

**So MedApp shows no cookie banner,** and needs none while this stays true. Adding analytics, a third-party script, or a non-essential cookie would need consent first, a DPIA update, and this statement updated.

## 3. Statement for staff

> **Cookies and storage in MedApp**
>
> MedApp uses one cookie, `medapp.session`, to keep you signed in. It's deleted when you sign out, or after 8 hours (up to 24 hours if you choose to stay signed in). MedApp also keeps your display and accessibility settings, your saved searches and your pinned patients and procedures in your browser, so the app looks and works the way you set it. Your settings are saved with your account too.
>
> MedApp uses no tracking, analytics or advertising cookies.
>
> **On a shared computer,** sign out when you finish: signing out removes your saved searches and pins from the browser (your display settings stay). Patient searches are part of the page address, so they stay in the browser's history; use your own browser profile on a shared computer, or clear the history when you finish.

## 4. Gaps

- **Browser history** isn't storage MedApp sets, but list pages put their filters in the URL, so patient search terms (names, email, date of birth) land in the history and address bar suggestions. The product owner decided to keep filters in the URL, because shareable list links are a feature; the staff rules and the customer's shared-computer measures cover it ([dpia.md](dpia.md), risk 10).
- **A session that expires** without a sign-out leaves the storage in the browser until someone signs out there.
- **The cookie name** will gain the `__Host-` prefix outside development (roadmap step 2), which keeps it to MedApp's own host. That doesn't change this statement.
