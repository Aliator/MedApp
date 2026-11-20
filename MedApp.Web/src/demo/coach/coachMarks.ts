import {PATHWAYS} from '../ids';
import type {CoachTarget} from './findTarget';

export interface CoachMark {
  text: string;
  target?: CoachTarget;
  reveal?: CoachTarget;
  canDoIt?: boolean;
  opensStepPage?: boolean;
  path?: string;
  showsSampleText?: boolean;
}

const nav = (name: string): CoachTarget => ({name, kind: 'link', inNav: true});
const navMenu = (name: string): CoachTarget => ({name: `${name} options`, kind: 'button', inNav: true});
const button = (name: string, after?: string): CoachTarget => ({name, kind: 'button', after});
const heading = (name: string): CoachTarget => ({name, kind: 'heading'});
const section = (name: string): CoachTarget => ({name, kind: 'heading', area: true});
const menuItem = (name: string): CoachTarget => ({name, kind: 'menuitem'});
const BELL: CoachTarget = {css: '[aria-label^="Notifications"]'};
const RELAY_TITLE = 'Scaly patch, left forearm';

export const COACH_MARKS: Record<string, CoachMark[]> = {
  'half-view': [
    {text: 'Patients, in the side nav. Every row has a menu at its end, shown when the pointer rests on the row.', target: nav('Patients')},
    {
      text: 'Its menu, opened for you. Open in half view on the right puts Patients beside this page.',
      target: menuItem('Open in half view on the right'),
      reveal: navMenu('Patients'),
      canDoIt: true,
    },
    {text: 'Close the half view with its close button (×).', target: {css: '[aria-label="Close half view"]'}, canDoIt: true},
    {text: 'Shift-click Patients: it opens on the right again. Then close it again.', target: nav('Patients')},
    {text: 'Ctrl-click (⌘-click on a Mac) Patients: it opens on the left, and this page moves to the right.', target: nav('Patients')},
    {text: 'With two pages open, the side nav folds to its icons. Expand sidebar opens it from the side.', target: {css: '[aria-label="Expand sidebar"]'}},
    {
      text: 'The folding is a setting, on by default: Shrink the side nav when two pages are open.',
      target: {name: 'Shrink the side nav when two pages are open', kind: 'field'},
      opensStepPage: true,
    },
  ],
  'pin-patient': [
    {text: 'Samuel Okafor’s record. While it’s open, he shows in the side nav, under Patients.', target: nav('Samuel Okafor')},
    {text: 'His row’s menu, opened for you. Choose Pin.', target: menuItem('Pin'), reveal: navMenu('Samuel Okafor'), canDoIt: true},
    {text: 'Go to another page: he stays under Patients. Click his name there to open him again.', target: nav('Samuel Okafor'), path: '/patients'},
  ],
  'team-walls': [
    {text: 'Tobias Lund, opened by the lab. Patients are shared across the centre.', target: heading('View Patient')},
    {text: 'Each team sees only its own pathways in full: his Dermatology pathway shows here as a one-line summary.', target: {name: 'Pathways', kind: 'link'}},
    {text: 'The admin can read every team’s records, but changes nothing clinical.'},
  ],
  'relay-triage': [
    {text: 'Leah Corrin’s pathway, at Triage.', target: heading(RELAY_TITLE)},
    {
      text: 'Pin it from its row’s menu: it stays under Pathways in the side nav, for the notepad later.',
      target: menuItem('Pin'),
      reveal: navMenu(`${RELAY_TITLE} for Leah Corrin`),
      canDoIt: true,
    },
    {text: 'Triage can’t be finished yet: her consent, given at yesterday’s visit, was never recorded.', target: {css: '[aria-label="Consent needed, show details"]'}},
    {text: 'Record it here: the form, who signed, and the date it was given.', target: button('Record Consent', 'Consent'), canDoIt: true},
    {text: 'Then Finish Stage. Testing is the lab’s, so the pathway goes to the lab.', target: button('Finish Stage')},
  ],
  'relay-lab': [
    {text: 'Leah Corrin’s lab case: her sample is in, and its steps are done.', target: section("What's next")},
    {text: 'Type the results, then Authorise and Open Pathway. They go on the lab’s Testing stage.', target: button('Enter Results and Authorise'), canDoIt: true},
    {text: 'On her pathway, Finish Stage on Testing sends it back to Dermatology.', target: button('Finish Stage'), path: `/pathways/${PATHWAYS.relay}`},
  ],
  'relay-back': [
    {text: 'Leah Corrin’s pathway is back from the lab, with the results on Testing.', target: heading(RELAY_TITLE)},
    {text: 'The bell has it too.', target: BELL},
    {text: 'Results appointment needs a visit: Book Appointment, pick who and a free time, and book it.', target: button('Book Appointment'), canDoIt: true},
  ],
  'team-calendar': [
    {text: 'Dermatology’s team calendar: everyone in the team side by side. Pick any team here.', target: button('Dermatology')},
    {text: 'New, then New Meeting.', target: menuItem('New Meeting'), reveal: button('New'), canDoIt: true},
    {text: 'Give it a title, and in Who add one person: n.thornbury (Nadia Thornbury).', target: {name: 'Who', kind: 'field'}},
    {text: 'Pick the first suggested time.', target: {css: '[role=group][aria-label="Suggested times"]'}},
    {text: 'Now add b.sandiford and t.hallworth to Who. Bea Sandiford is at an outreach clinic then.', target: {name: 'Who', kind: 'field'}},
    {text: 'The suggested times now fit everyone (Tom Hallworth works Tuesday to Thursday). Pick another, and save.', target: button('Add Meeting')},
  ],
  'meeting-seen': [
    {text: 'The meeting is on Nadia Thornbury’s calendar.', target: heading('Calendar')},
    {text: 'The bell has the invitation, to accept or decline.', target: BELL},
  ],
  'on-hold-overdue': [
    {text: 'As the admin, Pathways lists every team’s open pathways.', target: heading('Pathways')},
    {text: 'Narrow it to Dermatology or the lab here.', target: button('All teams')},
    {text: 'Samuel Okafor’s is on hold.', target: {name: 'On hold', kind: 'tab'}, canDoIt: true},
    {text: 'Harriet Quill’s is past its target date: its last stage waits for her referral letter.', target: {name: 'Overdue', kind: 'tab'}, canDoIt: true},
    {text: 'Sort and filter from the bar above the list, and its menu.', target: {css: '[aria-label^="List options"]'}},
  ],
  notepad: [
    {text: 'The notepad: it opens beside the page you’re on and stays open as you move around.', target: {css: '[data-notepad-toggle]'}, canDoIt: true},
    {
      text: 'Paste the sample notes here. It saves as you type, and each account has its own.',
      target: {css: '[role=textbox][aria-label="Notepad"]'},
      showsSampleText: true,
    },
    {text: 'Or Shift-click Notepad, to open it in the half view on the right instead.', target: {css: '[data-notepad-toggle]'}},
    {text: 'Leah Corrin’s pathway, opened for you: pinned under Pathways if you pinned it in the relay.', target: heading(RELAY_TITLE), path: `/pathways/${PATHWAYS.relay}`},
    {text: 'Add Note to Pathway, check it’s her pathway, and add it. It shows under Notes, by you.', target: button('Add Note to Pathway'), canDoIt: true},
  ],
  accessibility: [
    {text: 'Text size, High contrast, Reduce motion, Easy-to-read font and more.', target: section('Accessibility')},
    {text: 'They apply at once; the full version keeps them with your account.', target: {css: '[role=radiogroup]:has(input[value="larger"])'}},
  ],
  colours: [
    {text: 'The app’s colours in light mode: a colour theme, or your own.', target: section('Light Mode')},
    {text: 'And in dark mode.', target: section('Dark Mode')},
  ],
  insights: [
    {text: 'Open, in progress and overdue pathways, for every team.', target: heading('Insights')},
    {text: 'Or the one picked here. Staff see only their own teams’.', target: button('All teams')},
    {text: 'This week’s counts come from the demo’s pathways; the history behind the charts is made up.', target: section('Time at each stage')},
  ],
};
