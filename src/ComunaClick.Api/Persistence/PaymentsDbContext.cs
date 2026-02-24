using ComunaClick.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ComunaClick.Api.Persistence;

public sealed class PaymentsDbContext : DbContext
{
    public PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : base(options) { }

    public DbSet<PaymentCustomerToken> CustomerTokens => Set<PaymentCustomerToken>();
    public DbSet<PaymentIntent> PaymentIntents => Set<PaymentIntent>();
    public DbSet<PaymentCharge> Charges => Set<PaymentCharge>();
    public DbSet<PaymentProviderEvent> ProviderEvents => Set<PaymentProviderEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("payments");

        modelBuilder.Entity<PaymentCustomerToken>(entity =>
        {
            entity.ToTable("customer_tokens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
            entity.Property(x => x.ProviderRef).HasColumnName("provider_ref").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").IsRequired();
            entity.Property(x => x.RawResponse).HasColumnName("raw_response").HasColumnType("jsonb")
                .HasDefaultValueSql("'{}'::jsonb");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.RevokedAt).HasColumnName("revoked_at");
            entity.HasIndex(x => new { x.CustomerId, x.ProviderRef }).IsUnique();
            entity.HasIndex(x => x.CustomerId);
            entity.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<PaymentIntent>(entity =>
        {
            entity.ToTable("payment_intents");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.ExternalReference).HasColumnName("external_reference").IsRequired();
            entity.Property(x => x.Amount).HasColumnName("amount").HasColumnType("numeric(14,2)");
            entity.Property(x => x.Currency).HasColumnName("currency").HasDefaultValue("CLP");
            entity.Property(x => x.Status).HasColumnName("status").IsRequired();
            entity.Property(x => x.Provider).HasColumnName("provider").HasDefaultValue("transbank");
            entity.Property(x => x.ProviderToken).HasColumnName("provider_token");
            entity.Property(x => x.AuthorizationCode).HasColumnName("authorization_code");
            entity.Property(x => x.RawResponse).HasColumnName("raw_response").HasColumnType("jsonb")
                .HasDefaultValueSql("'{}'::jsonb");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => x.ExternalReference);
            entity.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<PaymentCharge>(entity =>
        {
            entity.ToTable("charges");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.CustomerTokenId).HasColumnName("customer_token_id");
            entity.Property(x => x.IntentId).HasColumnName("intent_id");
            entity.Property(x => x.Amount).HasColumnName("amount").HasColumnType("numeric(14,2)");
            entity.Property(x => x.Currency).HasColumnName("currency").HasDefaultValue("CLP");
            entity.Property(x => x.Status).HasColumnName("status").IsRequired();
            entity.Property(x => x.ProviderRef).HasColumnName("provider_ref");
            entity.Property(x => x.AuthorizationCode).HasColumnName("authorization_code");
            entity.Property(x => x.RawResponse).HasColumnName("raw_response").HasColumnType("jsonb")
                .HasDefaultValueSql("'{}'::jsonb");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => x.IntentId);
            entity.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<PaymentProviderEvent>(entity =>
        {
            entity.ToTable("provider_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.ProviderEventId).HasColumnName("provider_event_id").IsRequired();
            entity.Property(x => x.IntentId).HasColumnName("intent_id");
            entity.Property(x => x.EventType).HasColumnName("event_type").IsRequired();
            entity.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb")
                .HasDefaultValueSql("'{}'::jsonb");
            entity.Property(x => x.ReceivedAt).HasColumnName("received_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => x.ProviderEventId).IsUnique();
            entity.HasIndex(x => x.IntentId);
        });
    }
}
