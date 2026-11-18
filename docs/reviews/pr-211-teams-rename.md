# PR #211: MC2, specialties renamed to teams

Rename across code, database (`RenamedSpecialtiesToTeams`: renames only, keys re-added; tested up and down on LocalDB), API (`api/teams`), web and light demo. Quick review: no findings; stored enums keep numbers (`ActivityAction` 42, `ReferralEventAction` 2); stored JSON keys keep `specialty`; `TeamsController` keeps its seven permissions.
