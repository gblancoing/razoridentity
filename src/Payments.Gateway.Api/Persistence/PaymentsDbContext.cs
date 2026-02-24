using Microsoft.EntityFrameworkCore;
using Payments.Gateway.Api.Persistence.Entities;

namespace Payments.Gateway.Api.Persistence;

public sealed class PaymentsDbContext : DbContext
{
    public PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : base(options) { }

    public DbSet<PaymentIntent> PaymentIntents => Set<PaymentIntent>();
    public DbSet<ProviderEvent> ProviderEvents => Set<ProviderEvent>();
    public DbSet<CustomerToken> CustomerTokens => Set<CustomerToken>();
    public DbSet<Charge> Charges => Set<Charge>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("payments");

        modelBuilder.Entity<PaymentIntent>(entity =>
        {
            entity.ToTable("payment_intents");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.ExternalReference).IsRequired();
            entity.Property(x => x.Amount).HasColumnType("numeric(14,2)");
            entity.Property(x => x.Currency).HasDefaultValue("CLP");
            entity.Property(x => x.Status).IsRequired();
            entity.Property(x => x.Provider).HasDefaultValue("transbank");
            entity.Property(x => x.RawResponse).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.ExternalReference);
        });

        modelBuilder.Entity<ProviderEvent>(entity =>
        {
            entity.ToTable("provider_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.ProviderEventId).IsRequired();
            entity.Property(x => x.EventType).IsRequired();
            entity.Property(x => x.Payload).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
            entity.Property(x => x.ReceivedAt).HasDefaultValueSql("now()");
            entity.HasIndex(x => x.ProviderEventId).IsUnique();
            entity.HasIndex(x => x.IntentId);
            entity.HasOne(x => x.Intent).WithMany(x => x.ProviderEvents).HasForeignKey(x => x.IntentId);
        });

        modelBuilder.Entity<CustomerToken>(entity =>
        {
            entity.ToTable("customer_tokens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.CustomerId).IsRequired();
            entity.Property(x => x.ProviderRef).IsRequired();
            entity.Property(x => x.Status).IsRequired();
            entity.Property(x => x.RawResponse).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            entity.HasIndex(x => x.CustomerId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => new { x.CustomerId, x.ProviderRef }).IsUnique();
        });

        modelBuilder.Entity<Charge>(entity =>
        {
            entity.ToTable("charges");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.Amount).HasColumnType("numeric(14,2)");
            entity.Property(x => x.Currency).HasDefaultValue("CLP");
            entity.Property(x => x.Status).IsRequired();
            entity.Property(x => x.RawResponse).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            entity.HasIndex(x => x.IntentId);
            entity.HasIndex(x => x.Status);
            entity.HasOne(x => x.CustomerToken).WithMany(x => x.Charges).HasForeignKey(x => x.CustomerTokenId);
            entity.HasOne(x => x.Intent).WithMany().HasForeignKey(x => x.IntentId);
        });
    }
}
