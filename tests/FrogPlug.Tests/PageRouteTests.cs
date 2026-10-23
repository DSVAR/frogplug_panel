using Microsoft.AspNetCore.Components;

namespace FrogPlug.Tests;

public sealed class PageRouteTests
{
    [Fact]
    public void Every_page_route_is_declared_once()
    {
        var assembly = typeof(FrogPlug.Server.Components.App).Assembly;

        var duplicates = assembly.GetTypes()
            .SelectMany(type => type.GetCustomAttributes(typeof(RouteAttribute), false)
                .Cast<RouteAttribute>()
                .Select(route => (Route: route.Template.ToLowerInvariant(), Page: type.Name)))
            .GroupBy(item => item.Route)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key + ": " + string.Join(", ", group.Select(item => item.Page)))
            .ToList();

        Assert.Empty(duplicates);
    }
}
