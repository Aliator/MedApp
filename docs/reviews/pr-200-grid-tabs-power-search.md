# PR #200: Configurable tabs on every grid, and server-side search on four more

https://github.com/Aliator/MedApp/pull/200 (`feature/grid-power-search`), head `cbf05d93`, reviewed 18 November 2026 (quick review).

Every grid using `ListFilterBar` opens on a tab row of the person's favourites (defaults: the page's old tabs or quick filters, else "All"), with a menu button: Search (the power search bar; "Tabs" returns) and Configuration (add with a name and exact search, edit, remove, drag or keyboard reorder, reset). The `medapp.favourites` setting holds only the person's changes; old `{saved, startId}` entries become their own tabs. Lab, Results, Letters and Data Requests gain server-side search and sorting; the Lab's search goes in the body of a new `POST api/specimens/search` (`ViewPatients`, the same handler, so `PatientListed` is still recorded) and isn't kept in the address; "Find by specimen number" is removed. The Lab and Specialty pickers sit in the title row.

Findings: one low, left. Results, Letters and Data Requests search a patient's name through the GET query string, so a name can appear in a URL, as the Patients list's name filter already does; never an NHS or specimen number.

Checked: attributes unchanged elsewhere (Data Requests keeps `ManagePrivacy`); all LINQ, with search capped at 200 characters by the validators, sort columns from a fixed list (`Ordering.ColumnOf`, unknown values fall back), every order ending `ThenBy(Id)`, paging unchanged; the favourites migration touches only `medapp.favourites` and loses nothing; demo handlers only under `src/demo/handlers/`; no stored enum changed (`LetterView.All` is a request enum).

Remaining risk: the letters' patient-name search and sort use a correlated subquery, proven to translate by `GridSearchSqlTests` but not yet run on SQL Server; the new POST endpoint's integration test comes with the next full run.
