using System.ComponentModel.DataAnnotations;
using FrogPlug.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace FrogPlug.Server.Services;

public sealed class RoutingService(AppDbContext db, AuditService audit, SyncQueue? syncQueue = null)
{
    private static readonly (RoutingMatchType Type, string Value, string Target, string Comment)[] Defaults =
    [
        (RoutingMatchType.Geosite, "category-ads-all", "block", "Реклама"),
        (RoutingMatchType.Geosite, "category-ru", "role:RuExit", string.Empty),
        (RoutingMatchType.Domain, "ru", "role:RuExit", string.Empty),
        (RoutingMatchType.Domain, "su", "role:RuExit", string.Empty),
        (RoutingMatchType.Domain, "xn--p1ai", "role:RuExit", string.Empty),
        (RoutingMatchType.Geoip, "ru", "role:RuExit", string.Empty),
        (RoutingMatchType.Geosite, "google", "role:Exit", string.Empty)
    ];

    public Task<List<RoutingRule>> List()
    {
        return db.RoutingRules.OrderBy(rule => rule.Order).ToListAsync();
    }

    public async Task<RoutingRule> Add(RoutingRuleInput input)
    {
        await ValidateTarget(input.Target);
        ValidateValue(input);
        var rule = new RoutingRule
        {
            Order = await db.RoutingRules.CountAsync() + 1,
            MatchType = input.MatchType,
            Value = input.Value.Trim(),
            Target = input.Target,
            IsEnabled = input.IsEnabled,
            Comment = input.Comment
        };
        db.RoutingRules.Add(rule);
        await audit.Write(AuditActions.RoutingCreate, rule.Value);
        await db.SaveChangesAsync();
        syncQueue?.RequestAll();
        return rule;
    }

    public async Task<RoutingRule> Update(int id, RoutingRuleInput input)
    {
        await ValidateTarget(input.Target);
        ValidateValue(input);
        var rule = await Require(id);
        rule.MatchType = input.MatchType;
        rule.Value = input.Value.Trim();
        rule.Target = input.Target;
        rule.IsEnabled = input.IsEnabled;
        rule.Comment = input.Comment;
        await audit.Write(AuditActions.RoutingUpdate, rule.Value);
        await db.SaveChangesAsync();
        syncQueue?.RequestAll();
        return rule;
    }

    public async Task Delete(int id)
    {
        var rule = await Require(id);
        db.RoutingRules.Remove(rule);
        await NormalizeOrder();
        await audit.Write(AuditActions.RoutingDelete, rule.Value);
        await db.SaveChangesAsync();
        syncQueue?.RequestAll();
    }

    public async Task MoveUp(int id)
    {
        await Move(id, -1);
    }

    public async Task MoveDown(int id)
    {
        await Move(id, 1);
    }

    public async Task SetEnabled(int id, bool enabled)
    {
        var rule = await Require(id);
        rule.IsEnabled = enabled;
        await audit.Write(AuditActions.RoutingToggle, rule.Value);
        await db.SaveChangesAsync();
        syncQueue?.RequestAll();
    }

    public async Task SeedDefaultsAsync()
    {
        if (await db.RoutingRules.AnyAsync())
        {
            return;
        }

        for (var index = 0; index < Defaults.Length; index++)
        {
            var item = Defaults[index];
            db.RoutingRules.Add(new RoutingRule
            {
                Order = index + 1,
                MatchType = item.Type,
                Value = item.Value,
                Target = item.Target,
                IsEnabled = true,
                Comment = item.Comment
            });
        }

        await db.SaveChangesAsync();
        syncQueue?.RequestAll();
    }

    public Task<List<RoutingRule>> ListAsync()
    {
        return List();
    }

    public async Task SaveAsync(RoutingRule rule)
    {
        await Update(rule.Id, new RoutingRuleInput(rule.MatchType, rule.Value, rule.Target, rule.IsEnabled, rule.Comment));
    }

    public async Task DeleteAsync(RoutingRule rule)
    {
        await Delete(rule.Id);
    }

    private async Task Move(int id, int direction)
    {
        var rules = await List();
        var index = rules.FindIndex(rule => rule.Id == id);
        var otherIndex = index + direction;
        if (index < 0 || otherIndex < 0 || otherIndex >= rules.Count)
        {
            return;
        }

        (rules[index].Order, rules[otherIndex].Order) = (rules[otherIndex].Order, rules[index].Order);
        await NormalizeOrder();
        await audit.Write(AuditActions.RoutingMove, rules[index].Value);
        await db.SaveChangesAsync();
    }

    private async Task NormalizeOrder()
    {
        var rules = await db.RoutingRules.OrderBy(rule => rule.Order).ThenBy(rule => rule.Id).ToListAsync();
        for (var index = 0; index < rules.Count; index++)
        {
            rules[index].Order = index + 1;
        }
    }

    private async Task ValidateTarget(string target)
    {
        if (target is "direct" or "block" or "role:Exit" or "role:RuExit" or "role:GoogleExit")
        {
            return;
        }

        if (!target.StartsWith("node:") || !int.TryParse(target[5..], out var nodeId) || nodeId <= 0)
        {
            throw new ValidationException("Цель должна быть direct, block, ролью или node:<id>");
        }

        if (!await db.Nodes.AnyAsync(node => node.Id == nodeId))
        {
            throw new ValidationException("Указанная нода не найдена");
        }
    }

    private static void ValidateValue(RoutingRuleInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Value))
        {
            throw new ValidationException("Значение правила обязательно");
        }
    }

    private async Task<RoutingRule> Require(int id)
    {
        return await db.RoutingRules.FirstOrDefaultAsync(rule => rule.Id == id)
            ?? throw new KeyNotFoundException("Правило маршрутизации не найдено");
    }
}

public sealed record RoutingRuleInput(
    RoutingMatchType MatchType,
    string Value,
    string Target,
    bool IsEnabled = true,
    string? Comment = null);
