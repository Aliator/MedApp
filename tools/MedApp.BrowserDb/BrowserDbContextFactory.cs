using MedApp.Browser.Hosting;
using MedApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MedApp.BrowserDb;

/// <summary>The browser's context for <c>dotnet ef dbcontext optimize</c> (<c>optimize-model.sh</c>): the model fitted to SQLite.</summary>
public sealed class BrowserDbContextFactory : IDesignTimeDbContextFactory<MedAppDbContext>
{
    public MedAppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MedAppDbContext>();
        options.UseBrowserDatabase("Data Source=:memory:", compiledModel: false);
        return new MedAppDbContext(options.Options);
    }
}
