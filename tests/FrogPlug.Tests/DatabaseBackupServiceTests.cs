using System.Text;
using FrogPlug.Server.Data;
using FrogPlug.Server.Endpoints;
using FrogPlug.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace FrogPlug.Tests;

public sealed class DatabaseBackupServiceTests
{
    [Fact]
    public async Task CreateReturnsValidSqliteHeader()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        fixture.Db.Settings.Add(new Setting { Key = "test", Value = "value" });
        await fixture.Db.SaveChangesAsync();

        var backup = await fixture.Backups.Create();

        Assert.StartsWith("SQLite format 3", Encoding.ASCII.GetString(backup, 0, 15));
    }

    [Fact]
    public void EndpointRequiresAuthentication()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddScoped<DatabaseBackupService>();
        builder.Services.AddScoped<AuditService>();
        var app = builder.Build();
        app.MapBackupEndpoint();
        var routes = (IEndpointRouteBuilder)app;
        var endpoint = routes.DataSources.Single().Endpoints.Single();

        Assert.Contains(endpoint.Metadata, item => item is IAuthorizeData);
    }
}
