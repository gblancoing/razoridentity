using System.Text.Json;
using ComunaClick.Api.Modules.Marketplace.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ComunaClick.Api.Modules.Marketplace;

public sealed class MarketplacePaymentService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly CoreDbContext _db;
    private readonly FeeCalculator _feeCalculator;
    private readonly MercadoPagoOAuthService _oauthService;
    private readonly MercadoPagoMarketplaceClient _mpClient;
    private readonly SellerMarketplaceService _sellerService;
    private readonly MarketplaceAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly MarketplaceMetricsService _metrics;
    private readonly MercadoPagoMarketplaceOptions _options;

    public MarketplacePaymentService(
        CoreDbContext db,
        FeeCalculator feeCalculator,
        MercadoPagoOAuthService oauthService,
        MercadoPagoMarketplaceClient mpClient,
        SellerMarketplaceService sellerService,
        MarketplaceAuditService auditService,
        IHttpContextAccessor httpContextAccessor,
        MarketplaceMetricsService metrics,
        IOptions<MercadoPagoMarketplaceOptions> options)
    {
        _db = db;
        _feeCalculator = feeCalculator;
        _oauthService = oauthService;
        _mpClient = mpClient;
        _sellerService = sellerService;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
        _metrics = metrics;
        _options = options.Value;
    }

    public async Task<MarketplacePaymentResponse> CreateAsync(CreateMarketplacePaymentRequest request, CancellationToken cancellationToken)
    {
        ValidateCreateRequest(request);

        var seller = await _sellerService.EnsureSellerAsync(request.SellerId, cancellationToken);
        var accessToken = await _oauthService.GetSellerAccessTokenAsync(request.SellerId, cancellationToken);

        var order = await ResolveOrCreateOrderAsync(request, seller, cancellationToken);

        if (string.Equals(order.Status, "approved", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Approved orders cannot be charged again.");
        }

        var existingApproved = await _db.Payments.AnyAsync(x =>
            x.OrderId == order.Id &&
            x.Status == "approved",
            cancellationToken);
        if (existingApproved)
        {
            throw new InvalidOperationException("Order already has an approved payment.");
        }

        var feeConfig = await ResolveFeeConfigurationAsync(request.SellerId, request.FeeOverride, cancellationToken);
        var fee = _feeCalculator.Calculate(order.GrossAmount, feeConfig.FixedFeeAmount, feeConfig.PercentageFee);
        order.PlatformFeeAmount = fee.TotalPlatformFeeAmount;
        order.NetAmount = fee.NetToSellerAmount;
        order.TotalAmount = order.GrossAmount;
        order.Currency = _options.Currency;
        order.UpdatedAt = DateTimeOffset.UtcNow;

        var correlationId = _httpContextAccessor.HttpContext?.GetCorrelationId() ?? Guid.NewGuid().ToString("N");
        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? $"cc:{order.ExternalReference}:{Guid.NewGuid():N}"
            : request.IdempotencyKey.Trim();

        var payment = new Payment
        {
            TenantId = seller.TenantId,
            SellerId = seller.Id,
            OrderId = order.Id,
            Provider = "mercadopago",
            ExternalReference = order.ExternalReference ?? order.Id.ToString("N"),
            Amount = order.GrossAmount,
            TransactionAmount = order.GrossAmount,
            Currency = order.Currency,
            Status = "pending",
            IdempotencyKey = idempotencyKey,
            CorrelationId = correlationId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(cancellationToken);

        MarketplacePaymentResponse response;
        if (string.Equals(request.Flow, "checkout_pro", StringComparison.OrdinalIgnoreCase))
        {
            var preference = await _mpClient.CreateCheckoutProPreferenceAsync(
                accessToken,
                new MercadoPagoPreferenceRequest(
                    payment.ExternalReference,
                    fee.TotalPlatformFeeAmount,
                    new MercadoPagoBackUrls(
                        $"{_options.AppBaseUrl.TrimEnd('/')}/buyer/orders?orderId={order.Id}",
                        $"{_options.AppBaseUrl.TrimEnd('/')}/buyer/orders?orderId={order.Id}",
                        $"{_options.AppBaseUrl.TrimEnd('/')}/buyer/orders?orderId={order.Id}"),
                    order.Items.Select(x => new MercadoPagoPreferenceItem(
                        x.ProductId.ToString("N"),
                        !string.IsNullOrWhiteSpace(request.Items.FirstOrDefault(i => string.Equals(i.Sku, x.ProductId.ToString("N"), StringComparison.OrdinalIgnoreCase))?.Title)
                            ? request.Items.First(i => string.Equals(i.Sku, x.ProductId.ToString("N"), StringComparison.OrdinalIgnoreCase)).Title
                            : "Producto ComunaClic",
                        x.Quantity,
                        order.Currency,
                        x.UnitPrice)).ToArray(),
                    new MercadoPagoPreferencePayer(order.BuyerEmail ?? request.Buyer.Email, order.BuyerName ?? request.Buyer.Name),
                    $"{_options.AppBaseUrl.TrimEnd('/')}/api/webhooks/mercadopago"),
                cancellationToken);

            payment.ProviderToken = preference.Id;
            payment.RawResponseJson = JsonSerializer.Serialize(preference, JsonOptions);
            payment.StatusDetail = "checkout_pro_preference_created";
            response = new MarketplacePaymentResponse(
                payment.Id,
                order.Id,
                seller.Id,
                "checkout_pro",
                payment.Provider,
                payment.Status,
                payment.StatusDetail,
                payment.Currency,
                order.GrossAmount,
                fee.TotalPlatformFeeAmount,
                fee.NetToSellerAmount,
                payment.ExternalReference,
                null,
                preference.InitPoint ?? preference.SandboxInitPoint,
                correlationId);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.PaymentToken) || string.IsNullOrWhiteSpace(request.PaymentMethodId))
            {
                throw new InvalidOperationException("PaymentToken and PaymentMethodId are required for checkout_api flow.");
            }

            var mpPayment = await _mpClient.CreatePaymentAsync(
                accessToken,
                new MercadoPagoPaymentCreateRequest(
                    order.GrossAmount,
                    request.PaymentToken,
                    request.Description ?? BuildDescription(order),
                    request.Installments.GetValueOrDefault(1) <= 0 ? 1 : request.Installments.GetValueOrDefault(1),
                    request.PaymentMethodId,
                    request.IssuerId,
                    new MercadoPagoPayerRequest(
                        request.Buyer.Email,
                        request.Buyer.Name,
                        string.IsNullOrWhiteSpace(request.Buyer.IdentificationType) || string.IsNullOrWhiteSpace(request.Buyer.IdentificationNumber)
                            ? null
                            : new MercadoPagoIdentificationRequest(request.Buyer.IdentificationType, request.Buyer.IdentificationNumber)),
                    fee.TotalPlatformFeeAmount,
                    payment.ExternalReference,
                    "COMUNACLIC",
                    new
                    {
                        orderId = order.Id,
                        sellerId = seller.Id,
                        platformFeeAmount = fee.TotalPlatformFeeAmount
                    }),
                idempotencyKey,
                cancellationToken);

            payment.MercadoPagoPaymentId = mpPayment.Id?.ToString();
            payment.PaymentMethod = mpPayment.PaymentMethodId;
            payment.Status = NormalizeMercadoPagoStatus(mpPayment.Status);
            payment.StatusDetail = mpPayment.StatusDetail;
            payment.PaidAmount = mpPayment.TransactionAmount;
            payment.DateApproved = mpPayment.DateApproved;
            payment.RawResponseJson = JsonSerializer.Serialize(mpPayment, JsonOptions);

            response = new MarketplacePaymentResponse(
                payment.Id,
                order.Id,
                seller.Id,
                "checkout_api",
                payment.Provider,
                payment.Status,
                payment.StatusDetail,
                payment.Currency,
                order.GrossAmount,
                fee.TotalPlatformFeeAmount,
                fee.NetToSellerAmount,
                payment.ExternalReference,
                payment.MercadoPagoPaymentId,
                null,
                correlationId);
        }

        _db.PaymentFees.Add(new PaymentFee
        {
            PaymentId = payment.Id,
            PlatformFeeAmount = fee.TotalPlatformFeeAmount,
            NetToSellerAmount = fee.NetToSellerAmount,
            CreatedAt = DateTimeOffset.UtcNow
        });

        _db.PaymentStatusHistory.Add(new PaymentStatusHistory
        {
            PaymentId = payment.Id,
            PreviousStatus = null,
            NewStatus = payment.Status,
            Detail = payment.StatusDetail,
            RawPayloadJson = payment.RawResponseJson,
            CreatedAt = DateTimeOffset.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        _metrics.Increment("payments_created");
        if (string.Equals(payment.Status, "approved", StringComparison.OrdinalIgnoreCase))
        {
            _metrics.Increment("payments_approved");
        }

        await _auditService.WriteAsync(
            _sellerService.GetActor(),
            "marketplace.payment.created",
            nameof(Payment),
            payment.Id.ToString(),
            new
            {
                PaymentId = payment.Id,
                OrderId = order.Id,
                SellerId = seller.Id,
                flow = response.Flow,
                payment.ExternalReference,
                payment.Status,
                fee = fee.TotalPlatformFeeAmount
            },
            cancellationToken);

        return response;
    }

    public async Task<MarketplacePaymentDetailResponse?> GetAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var payment = await _db.Payments.AsNoTracking().FirstOrDefaultAsync(x => x.Id == paymentId, cancellationToken);
        if (payment is null)
        {
            return null;
        }

        return await BuildPaymentDetailAsync(payment, cancellationToken);
    }

    public async Task<Guid?> GetSellerIdByPaymentAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        return await _db.Payments.AsNoTracking()
            .Where(x => x.Id == paymentId)
            .Select(x => x.SellerId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<MarketplacePaymentDetailResponse?> GetByOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var payment = await _db.Payments.AsNoTracking()
            .Where(x => x.OrderId == orderId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (payment is null)
        {
            return null;
        }

        return await BuildPaymentDetailAsync(payment, cancellationToken);
    }

    public async Task<Guid?> GetSellerIdByOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        return await _db.Payments.AsNoTracking()
            .Where(x => x.OrderId == orderId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => x.SellerId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MarketplacePaymentDetailResponse>> ListBySellerAsync(Guid sellerId, CancellationToken cancellationToken)
    {
        var payments = await _db.Payments.AsNoTracking()
            .Where(x => x.SellerId == sellerId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        var items = new List<MarketplacePaymentDetailResponse>(payments.Count);
        foreach (var payment in payments)
        {
            items.Add(await BuildPaymentDetailAsync(payment, cancellationToken));
        }

        return items;
    }

    private async Task<MarketplacePaymentDetailResponse> BuildPaymentDetailAsync(Payment payment, CancellationToken cancellationToken)
    {
        var order = payment.OrderId.HasValue
            ? await _db.Orders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == payment.OrderId.Value, cancellationToken)
            : null;
        var seller = payment.SellerId.HasValue
            ? await _db.Sellers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == payment.SellerId.Value, cancellationToken)
            : null;
        var fee = await _db.PaymentFees.AsNoTracking().FirstOrDefaultAsync(x => x.PaymentId == payment.Id, cancellationToken);
        var history = await _db.PaymentStatusHistory.AsNoTracking()
            .Where(x => x.PaymentId == payment.Id)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new MarketplacePaymentStatusItemResponse(x.PreviousStatus, x.NewStatus, x.Detail, x.CreatedAt))
            .ToListAsync(cancellationToken);

        return new MarketplacePaymentDetailResponse(
            payment.Id,
            payment.OrderId ?? Guid.Empty,
            payment.SellerId ?? Guid.Empty,
            seller?.Name ?? "Seller",
            order?.BuyerEmail ?? string.Empty,
            order?.BuyerName,
            payment.Provider,
            payment.Status,
            payment.StatusDetail,
            payment.Currency,
            order?.GrossAmount ?? payment.Amount,
            fee?.PlatformFeeAmount ?? order?.PlatformFeeAmount ?? 0m,
            fee?.NetToSellerAmount ?? order?.NetAmount ?? 0m,
            fee?.MercadoPagoFeeAmount,
            payment.PaidAmount,
            payment.ExternalReference,
            payment.MercadoPagoPaymentId,
            payment.PaymentMethod,
            payment.DateApproved,
            payment.CreatedAt,
            history);
    }

    private async Task<Order> ResolveOrCreateOrderAsync(CreateMarketplacePaymentRequest request, Seller seller, CancellationToken cancellationToken)
    {
        if (request.OrderId.HasValue)
        {
            var existing = await _db.Orders.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == request.OrderId.Value, cancellationToken)
                ?? throw new InvalidOperationException("Order was not found.");

            if (existing.PartnerId != seller.Id)
            {
                throw new InvalidOperationException("Seller does not match the order.");
            }

            if (string.IsNullOrWhiteSpace(existing.BuyerEmail))
            {
                existing.BuyerEmail = request.Buyer.Email.Trim();
            }

            if (string.IsNullOrWhiteSpace(existing.BuyerName) && !string.IsNullOrWhiteSpace(request.Buyer.Name))
            {
                existing.BuyerName = request.Buyer.Name.Trim();
            }

            if (string.IsNullOrWhiteSpace(existing.ExternalReference))
            {
                existing.ExternalReference = $"cc-order-{existing.Id:N}";
            }

            if (existing.GrossAmount <= 0)
            {
                existing.GrossAmount = existing.TotalAmount > 0 ? existing.TotalAmount : existing.Subtotal + existing.DeliveryFee;
            }

            return existing;
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            throw new InvalidOperationException("At least one order item is required.");
        }

        var grossAmount = request.Items.Sum(x => x.UnitPrice * x.Quantity);
        var order = new Order
        {
            TenantId = seller.TenantId,
            PartnerId = seller.Id,
            CustomerId = Guid.Empty,
            ExternalReference = $"cc-order-{Guid.NewGuid():N}",
            Status = "payment_pending",
            Subtotal = grossAmount,
            DeliveryFee = 0m,
            TotalAmount = grossAmount,
            GrossAmount = grossAmount,
            PlatformFeeAmount = 0m,
            NetAmount = grossAmount,
            Currency = _options.Currency,
            BuyerEmail = request.Buyer.Email.Trim(),
            BuyerName = request.Buyer.Name?.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Items = request.Items.Select(x => new OrderItem
            {
                ProductId = Guid.Empty,
                Quantity = x.Quantity,
                UnitPrice = x.UnitPrice,
                TotalPrice = x.UnitPrice * x.Quantity
            }).ToList()
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);
        return order;
    }

    private async Task<SellerFeeConfiguration> ResolveFeeConfigurationAsync(
        Guid sellerId,
        MarketplaceFeeOverrideRequest? feeOverride,
        CancellationToken cancellationToken)
    {
        var config = await _db.SellerFeeConfigurations.FirstOrDefaultAsync(x => x.SellerId == sellerId, cancellationToken);
        if (feeOverride is null)
        {
            return config ?? new SellerFeeConfiguration
            {
                SellerId = sellerId,
                FixedFeeAmount = 0m,
                PercentageFee = 0m,
                IsActive = true
            };
        }

        return new SellerFeeConfiguration
        {
            SellerId = sellerId,
            FixedFeeAmount = feeOverride.FixedFeeAmount ?? config?.FixedFeeAmount ?? 0m,
            PercentageFee = feeOverride.PercentageFee ?? config?.PercentageFee ?? 0m,
            IsActive = true
        };
    }

    private static void ValidateCreateRequest(CreateMarketplacePaymentRequest request)
    {
        if (request.SellerId == Guid.Empty)
        {
            throw new InvalidOperationException("SellerId is required.");
        }

        if (request.Buyer is null || string.IsNullOrWhiteSpace(request.Buyer.Email))
        {
            throw new InvalidOperationException("Buyer email is required.");
        }
    }

    private static string BuildDescription(Order order)
        => !string.IsNullOrWhiteSpace(order.BuyerName)
            ? $"Compra ComunaClic {order.BuyerName}"
            : $"Compra ComunaClic {order.Id:N}"[..Math.Min(24, $"Compra ComunaClic {order.Id:N}".Length)];

    private static string NormalizeMercadoPagoStatus(string? status)
        => status?.Trim().ToLowerInvariant() switch
        {
            "approved" => "approved",
            "authorized" => "authorized",
            "rejected" => "rejected",
            "cancelled" or "canceled" => "cancelled",
            "refunded" => "refunded",
            "charged_back" => "charged_back",
            "in_process" => "in_process",
            _ => "pending"
        };
}
