using System.Security.Claims;
using ComunaClick.Api.Modules.Delivery;
using ComunaClick.Api.Modules.Delivery.Contracts;
using ComunaClick.Api.Persistence;
using ComunaClick.Api.Persistence.Entities;
using ComunaClick.Tests.Unit.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ComunaClick.Tests.Unit.Delivery;

public sealed class CourierPortalTests
{
    private static CourierPortalController CreateController(CoreDbContext db, Guid userId, string email)
        => new(db, new CourierPayeeService(db), mpOAuth: null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim("sub", userId.ToString()),
                        new Claim("email", email)
                    ], "test"))
                }
            }
        };

    private static Courier SeedCourier(CoreDbContext db, Guid tenantId, Guid partnerId, string? email, Guid? userId = null, string name = "Repartidor")
    {
        var courier = new Courier
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartnerId = partnerId,
            Name = name,
            Phone = "+56912345678",
            Email = email,
            UserId = userId,
            IsAvailable = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Couriers.Add(courier);
        db.SaveChanges();
        return courier;
    }

    private static void SeedSettlement(CoreDbContext db, Guid tenantId, Guid partnerId, Guid courierId, decimal net, string status)
    {
        db.DeliverySettlements.Add(new DeliverySettlement
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = Guid.NewGuid(),
            PartnerId = partnerId,
            CourierId = courierId,
            GrossAmount = net + 500m,
            PlatformFeeAmount = 300m,
            MercadoPagoFeeAmount = 200m,
            NetToCourierAmount = net,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        db.SaveChanges();
    }

    // El primer ingreso con el correo registrado por el negocio reclama el
    // perfil: queda persistido el user_id.
    [Fact]
    public async Task Me_ClaimByEmail_PersistsUserId()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, _, _) = TestDb.SeedCatalog(db, stock: 1);
        var courier = SeedCourier(db, tenantId, partner.Id, email: "repa@test.cl");

        var userId = Guid.NewGuid();
        var controller = CreateController(db, userId, "Repa@Test.cl");

        var result = await controller.Get(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<CourierMeResponse>(ok.Value);
        var item = Assert.Single(response.Items);
        Assert.Equal(courier.Id, item.CourierId);
        Assert.Equal(userId, db.Couriers.Single(x => x.Id == courier.Id).UserId);
    }

    // El vínculo por user_id sobrevive aunque el token traiga otro correo.
    [Fact]
    public async Task Me_UserIdMatch_SurvivesDifferentEmail()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, _, _) = TestDb.SeedCatalog(db, stock: 1);
        var userId = Guid.NewGuid();
        SeedCourier(db, tenantId, partner.Id, email: "viejo@test.cl", userId: userId);

        var controller = CreateController(db, userId, "nuevo@test.cl");

        var result = await controller.Get(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<CourierMeResponse>(ok.Value);
        Assert.Single(response.Items);
    }

    // Aislamiento: las ganancias de un repartidor jamás incluyen las de otro.
    [Fact]
    public async Task Earnings_NeverLeakAcrossCouriers()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, _, _) = TestDb.SeedCatalog(db, stock: 1);
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var courierA = SeedCourier(db, tenantId, partner.Id, "a@test.cl", userA, "A");
        var courierB = SeedCourier(db, tenantId, partner.Id, "b@test.cl", userB, "B");

        SeedSettlement(db, tenantId, partner.Id, courierA.Id, net: 2000m, status: "settled");
        SeedSettlement(db, tenantId, partner.Id, courierB.Id, net: 9999m, status: "settled");

        var controller = CreateController(db, userA, "a@test.cl");
        var result = await controller.GetEarnings(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var earnings = Assert.IsType<CourierEarningsResponse>(ok.Value);
        Assert.Equal(2000m, earnings.SettledTotal);
        Assert.Single(earnings.Recent);
    }

    // Agregados: settled aparte; pending + processing juntos; manual aparte.
    [Fact]
    public async Task Earnings_AggregatesByStatus()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, _, _) = TestDb.SeedCatalog(db, stock: 1);
        var userId = Guid.NewGuid();
        var courier = SeedCourier(db, tenantId, partner.Id, "repa@test.cl", userId);

        SeedSettlement(db, tenantId, partner.Id, courier.Id, 1000m, "settled");
        SeedSettlement(db, tenantId, partner.Id, courier.Id, 2000m, "pending");
        SeedSettlement(db, tenantId, partner.Id, courier.Id, 3000m, "processing");
        SeedSettlement(db, tenantId, partner.Id, courier.Id, 4000m, "manual");

        var controller = CreateController(db, userId, "repa@test.cl");
        var result = await controller.GetEarnings(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var earnings = Assert.IsType<CourierEarningsResponse>(ok.Value);
        Assert.Equal(1000m, earnings.SettledTotal);
        Assert.Equal(5000m, earnings.PendingTotal);
        Assert.Equal(4000m, earnings.ManualTotal);
        Assert.Equal(1, earnings.SettledCount);
        Assert.Equal(3, earnings.PendingCount);
    }

    // Iniciar la vinculación MP de un courier ajeno no revela su existencia.
    [Fact]
    public async Task MercadoPagoStart_ForeignCourier_ReturnsNotFound()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, _, _) = TestDb.SeedCatalog(db, stock: 1);
        var foreign = SeedCourier(db, tenantId, partner.Id, "otro@test.cl", Guid.NewGuid(), "Otro");

        var controller = CreateController(db, Guid.NewGuid(), "yo@test.cl");
        var result = await controller.StartMercadoPago(foreign.Id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    // El partner cambia el correo del repartidor → el vínculo anterior se
    // invalida (el correo nuevo debe reclamar de nuevo).
    [Fact]
    public async Task PartnerUpdateEmail_ResetsClaimedUserId()
    {
        await using var db = TestDb.Create();
        var (tenantId, partner, _, _) = TestDb.SeedCatalog(db, stock: 1);
        var courier = SeedCourier(db, tenantId, partner.Id, "viejo@test.cl", Guid.NewGuid());

        var tenantContext = new TenantContext();
        tenantContext.Set(tenantId, partner.Id);
        var controller = new CouriersController(
            db,
            tenantContext,
            deliveryService: null!,
            payeeService: null!,
            mpOAuth: null!,
            trackingTokens: TestDb.CreateTrackingTokenService())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.Update(
            partner.Id,
            courier.Id,
            new CourierUpdateRequest(null, null, null, null, Email: "nuevo@test.cl"));

        Assert.IsType<OkObjectResult>(result.Result);
        var updated = db.Couriers.Single(x => x.Id == courier.Id);
        Assert.Equal("nuevo@test.cl", updated.Email);
        Assert.Null(updated.UserId);
    }
}
