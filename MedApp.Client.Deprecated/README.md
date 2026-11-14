# MedApp.Client.Deprecated

> **Deprecated: not in use.** This is MedApp's original Blazor WebAssembly UI. It was replaced by the React app in [`MedApp.Web`](../MedApp.Web/README.md) at the end of the React migration ([`docs/react-migration-plan.md`](../docs/react-migration-plan.md), phase 5).

It stays in `MedApp.sln` for reference only:
- **Not run.** The Aspire AppHost doesn't start it, and the API no longer allows its origin (`https://localhost:7292`) through CORS.
- **No new work.** The only changes it gets are ones that keep it compiling, such as SDK or package moves. New UI work goes in `MedApp.Web`.
- **Kept building.** It's still in the solution build. Its `RootNamespace` and `AssemblyName` are still `MedApp.Client`, so the code was not changed by the rename.

## Running it anyway

To look at it, you would need to put two things back:
- **A CORS policy for its origin.** It calls the API cross-origin.
- **A cross-site session cookie.** That means `SameSite=None` with `Secure`; the API now issues `SameSite=Lax`.

Then run the AppHost for the database and API, and this project separately:

```bash
dotnet run --project MedApp.Client.Deprecated --launch-profile https   # https://localhost:7292, calls the API on https://localhost:7038
```

The last screenshots of it, taken against the same data as the React app, are in `tools/ui-parity/screenshots/*/blazor.png`; they're git-ignored, so they're local only.
