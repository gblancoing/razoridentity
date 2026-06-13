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
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<SubscriptionAttempt> SubscriptionAttempts => Set<SubscriptionAttempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("payments");

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
            entity.Property(x => x.ReviewStatus).HasColumnName("review_status");
            entity.Property(x => x.ReviewNote).HasColumnName("review_note");
            entity.Property(x => x.CoreNotifiedAt).HasColumnName("core_notified_at");
            entity.Property(x => x.CoreNotifyAttempts).HasColumnName("core_notify_attempts").HasDefaultValue(0);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.ExternalReference);
        });

        modelBuilder.Entity<ProviderEvent>(entity =>
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
            entity.HasOne(x => x.Intent).WithMany(x => x.ProviderEvents).HasForeignKey(x => x.IntentId);
        });

        modelBuilder.Entity<CustomerToken>(entity =>
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
            entity.HasIndex(x => x.CustomerId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => new { x.CustomerId, x.ProviderRef }).IsUnique();
        });

        modelBuilder.Entity<Charge>(entity =>
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
            entity.HasOne(x => x.CustomerToken).WithMany(x => x.Charges).HasForeignKey(x => x.CustomerTokenId);
            entity.HasOne(x => x.Intent).WithMany().HasForeignKey(x => x.IntentId);
        });

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.ToTable("subscriptions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.CustomerId).HasColumnName("customer_id").IsRequired();
            entity.Property(x => x.CustomerTokenId).HasColumnName("customer_token_id");
            entity.Property(x => x.ExternalReference).HasColumnName("external_reference").IsRequired();
            entity.Property(x => x.PlanName).HasColumnName("plan_name");
            entity.Property(x => x.Provider).HasColumnName("provider").HasDefaultValue("transbank");
            entity.Property(x => x.Amount).HasColumnName("amount").HasColumnType("numeric(14,2)");
            entity.Property(x => x.Currency).HasColumnName("currency").HasDefaultValue("CLP");
            entity.Property(x => x.BillingInterval).HasColumnName("billing_interval").HasDefaultValue("monthly");
            entity.Property(x => x.Status).HasColumnName("status").IsRequired();
            entity.Property(x => x.NextChargeAt).HasColumnName("next_charge_at");
            entity.Property(x => x.CancelledAt).HasColumnName("cancelled_at");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.CustomerId);
            entity.HasIndex(x => x.ExternalReference);
            entity.HasOne(x => x.CustomerToken).WithMany().HasForeignKey(x => x.CustomerTokenId);
        });

        modelBuilder.Entity<SubscriptionAttempt>(entity =>
        {
            entity.ToTable("subscription_attempts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(x => x.SubscriptionId).HasColumnName("subscription_id");
            entity.Property(x => x.IntentId).HasColumnName("intent_id");
            entity.Property(x => x.ChargeId).HasColumnName("charge_id");
            entity.Property(x => x.Status).HasColumnName("status").IsRequired();
            entity.Property(x => x.ErrorMessage).HasColumnName("error_message");
            entity.Property(x => x.AttemptedAt).HasColumnName("attempted_at").HasDefaultValueSql("now()");
            entity.HasIndex(x => x.SubscriptionId);
            entity.HasOne(x => x.Subscription).WithMany(x => x.Attempts).HasForeignKey(x => x.SubscriptionId);
        });
    }
}
