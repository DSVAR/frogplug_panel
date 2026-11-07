using FrogPlug.Core;
using Microsoft.EntityFrameworkCore;

namespace FrogPlug.Server.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Admin> Admins => Set<Admin>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<ClientUser> ClientUsers => Set<ClientUser>();
    public DbSet<Node> Nodes => Set<Node>();
    public DbSet<RoutingRule> RoutingRules => Set<RoutingRule>();
    public DbSet<NodeTrafficDaily> NodeTrafficDaily => Set<NodeTrafficDaily>();
    public DbSet<UserTrafficDaily> UserTrafficDaily => Set<UserTrafficDaily>();
    public DbSet<TrafficHourly> TrafficHourly => Set<TrafficHourly>();
    public DbSet<OpenFluxChannel> OpenFluxChannels => Set<OpenFluxChannel>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.Entity<Admin>().HasIndex(item => item.Login).IsUnique();
        builder.Entity<Setting>().HasKey(item => item.Key);
        builder.Entity<ClientUser>().HasIndex(item => item.Name).IsUnique();
        builder.Entity<ClientUser>().HasIndex(item => item.VlessUuid).IsUnique();
        builder.Entity<ClientUser>().HasIndex(item => item.SubscriptionToken).IsUnique();
        builder.Entity<ClientUser>().Property(item => item.AllowReality).HasDefaultValue(true).HasSentinel(true);
        builder.Entity<ClientUser>().Property(item => item.AllowXhttp).HasDefaultValue(true).HasSentinel(true);
        builder.Entity<ClientUser>().Property(item => item.AllowTgWeb).HasDefaultValue(false).HasSentinel(false);
        builder.Entity<ClientUser>().Property(item => item.AllowHysteria).HasDefaultValue(false).HasSentinel(false);
        builder.Entity<Node>().HasIndex(item => item.Name).IsUnique();
        builder.Entity<Node>().Property(item => item.RealityEnabled).HasDefaultValue(true).HasSentinel(true);
        builder.Entity<Node>().Property(item => item.XhttpEnabled).HasDefaultValue(true).HasSentinel(true);
        builder.Entity<Node>().Property(item => item.RealityFingerprint).HasDefaultValue("chrome").HasSentinel(string.Empty);
        builder.Entity<Node>().Property(item => item.XhttpMode).HasDefaultValue("auto").HasSentinel(string.Empty);
        builder.Entity<Node>().Property(item => item.HysteriaObfsEnabled).HasDefaultValue(true).HasSentinel(true);
        builder.Entity<Node>().Property(item => item.MtprotoPort).HasDefaultValue(NodePorts.Mtproto);
        builder.Entity<Node>().Property(item => item.MtprotoFrontingPort).HasDefaultValue(NodePorts.CaddyTls);
        builder.Entity<Node>().Property(item => item.TgWebEnabled).HasDefaultValue(false).HasSentinel(false);
        builder.Entity<Node>().Property(item => item.HysteriaEnabled).HasDefaultValue(false).HasSentinel(false);
        builder.Entity<Node>().Property(item => item.HysteriaPort).HasDefaultValue(NodePorts.Hysteria);
        builder.Entity<Node>().Property(item => item.MaskMode).HasConversion<string>()
            .HasDefaultValue(MaskMode.ForeignSite).HasSentinel((MaskMode)(-1));
        builder.Entity<Node>().Property(item => item.StubTemplate).HasDefaultValue(StubSiteTemplates.PersonalBlog);
        builder.Entity<RoutingRule>().Property(item => item.MatchType).HasConversion<string>();
        builder.Entity<NodeTrafficDaily>().HasIndex(item => new { item.NodeId, item.Date }).IsUnique();
        builder.Entity<UserTrafficDaily>().HasIndex(item => new { item.UserId, item.Date }).IsUnique();
        builder.Entity<TrafficHourly>().HasIndex(item => item.Date).IsUnique();
        builder.Entity<OpenFluxChannel>().HasIndex(item => new { item.NodeId, item.Name }).IsUnique();
        builder.Entity<OpenFluxChannel>().HasOne(item => item.Node).WithMany().HasForeignKey(item => item.NodeId).OnDelete(DeleteBehavior.Cascade);
    }
}
