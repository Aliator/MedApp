using MedApp.BrowserDb;

// Builds the full browser demo's database (docs/full-browser-demo-plan.md, Phase 2) into a folder: medapp.db, medapp.db.gz and db-version.json.
if (args is not ["--out", { Length: > 0 } output])
{
    Console.Error.WriteLine("Usage: dotnet run --project tools/MedApp.BrowserDb -c Release -- --out <folder>");
    return 2;
}

await DemoDatabaseBuild.RunAsync(Path.GetFullPath(output), Console.Out, CancellationToken.None);
return 0;
