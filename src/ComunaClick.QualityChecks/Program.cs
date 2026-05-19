using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

var suite = new QualityCheckSuite();
await suite.RunAsync(args);

internal sealed class QualityCheckSuite
{
    private readonly HttpClient _httpClient = new(new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false
    })
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly string _appBaseUrl = ReadSetting("QUALITY_APP_BASE_URL", "https://app.comunaclic.cl");
    private readonly string _aclBaseUrl = ReadSetting("QUALITY_ACL_BASE_URL", "https://acl.comunaclic.cl");
    private readonly string _apiBaseUrl = ReadSetting("QUALITY_API_BASE_URL", "https://api.comunaclic.cl");

    private readonly string? _qaAccessToken = ReadOptional("QUALITY_ACCESS_TOKEN");
    private readonly string? _qaEmail = ReadOptional("QUALITY_QA_EMAIL");
    private readonly string? _qaPassword = ReadOptional("QUALITY_QA_PASSWORD");
    private readonly string? _qaRecaptchaToken = ReadOptional("QUALITY_QA_RECAPTCHA_TOKEN");
    private readonly bool _strictAuthChecks = ReadBool("QUALITY_STRICT_AUTH_CHECKS");
    private readonly bool _allowRegister = ReadBool("QUALITY_ALLOW_REGISTER");

    private readonly string? _expectedDiscoveryPartnerName = ReadOptional("QUALITY_EXPECTED_DISCOVERY_PARTNER_NAME");
    private readonly string? _ownPartnerId = ReadOptional("QUALITY_PARTNER_ID");
    private readonly string? _foreignPartnerId = ReadOptional("QUALITY_FOREIGN_PARTNER_ID");
    private readonly string? _favoriteServiceId = ReadOptional("QUALITY_FAVORITE_SERVICE_ID");
    private readonly string? _bookingFlowServiceId = ReadOptional("QUALITY_BOOKING_FLOW_SERVICE_ID");
    private readonly string? _leadWorkflowLeadId = ReadOptional("QUALITY_LEAD_ID");
    private readonly string? _leadWorkflowOwner = ReadOptional("QUALITY_LEAD_OWNER");
    private readonly string? _leadWorkflowOutcomeReason = ReadOptional("QUALITY_LEAD_OUTCOME_REASON");

    private int _passCount;
    private int _skipCount;

    public async Task RunAsync(string[] args)
    {
        if (args.Any(arg => arg is "--help" or "-h"))
        {
            PrintHelp();
            return;
        }

        await CheckAppPublicAsync();
        await CheckAclContractsAsync();
        await CheckApiPublicAsync();
        await CheckTrackingContractsAsync();
        await CheckAuthenticatedOwnershipAsync();
        await CheckOptionalRegisterAsync();

        Console.WriteLine();
        Console.WriteLine($"[OK] Quality checks terminados. OK: {_passCount} | Skipped: {_skipCount}");
    }

    private async Task CheckAppPublicAsync()
    {
        Info($"Validando app pública en {_appBaseUrl}");

        var home = await SendAsync(HttpMethod.Get, $"{_appBaseUrl}/");
        Expect(home, "app.home", HttpStatusCode.OK);
        ExpectContains(home.Body, "ComunaClic", "app.home debería contener branding");
        ExpectHeader(home, "Content-Security-Policy", "app.home debería devolver CSP");
        Pass("App home pública responde con branding y CSP");

        var login = await SendAsync(HttpMethod.Get, $"{_appBaseUrl}/login");
        Expect(login, "app.login", HttpStatusCode.OK);
        ExpectContains(login.Body, "Cuenta personal", "app.login debería exponer CTA buyer");
        ExpectContains(login.Body, "Registrar negocio", "app.login debería exponer CTA partner");
        Pass("App login pública responde con CTAs esperados");

        var registerBusiness = await SendAsync(HttpMethod.Get, $"{_appBaseUrl}/register/business");
        Expect(registerBusiness, "app.registerBusiness", HttpStatusCode.OK);
        ExpectContains(registerBusiness.Body, "Registro de Negocio", "register/business debería renderizar onboarding");
        Pass("App register/business responde correctamente");
    }

    private async Task CheckAclContractsAsync()
    {
        Info($"Validando ACL en {_aclBaseUrl}");

        var invalidLoginPayload = JsonSerializer.Serialize(new
        {
            email = string.Empty,
            password = string.Empty,
            tenantId = (Guid?)null,
            partnerId = (Guid?)null,
            recaptchaToken = string.Empty
        }, _json);

        var invalidLogin = await SendAsync(HttpMethod.Post, $"{_aclBaseUrl}/v1/auth/login", invalidLoginPayload);
        ExpectOneOf(invalidLogin, "acl.invalidLogin", HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized);
        Pass("ACL login valida payload o credenciales");

        var invalidRegisterPayload = JsonSerializer.Serialize(new
        {
            name = string.Empty,
            email = string.Empty,
            password = "123",
            recaptchaToken = string.Empty
        }, _json);

        var invalidRegister = await SendAsync(HttpMethod.Post, $"{_aclBaseUrl}/v1/auth/register", invalidRegisterPayload);
        ExpectOneOf(invalidRegister, "acl.invalidRegister", HttpStatusCode.BadRequest, HttpStatusCode.Conflict);
        Pass("ACL register valida payload");
    }

    private async Task CheckApiPublicAsync()
    {
        Info($"Validando API pública en {_apiBaseUrl}");

        var countries = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/v1/public/geo/countries");
        Expect(countries, "api.countries", HttpStatusCode.OK);
        var countryId = ReadFirstGuidFromArray(countries.Body, "id");
        Pass("Geo countries responde con datos");

        var regions = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/v1/public/geo/regions?countryId={countryId}");
        Expect(regions, "api.regions", HttpStatusCode.OK);
        var regionId = ReadFirstGuidFromArray(regions.Body, "id");
        Pass("Geo regions responde con datos");

        var comunas = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/v1/public/geo/comunas?regionId={regionId}");
        Expect(comunas, "api.comunas", HttpStatusCode.OK);
        var comunaId = ReadFirstGuidFromArray(comunas.Body, "id");
        Pass("Geo comunas responde con datos");

        var tenantByComunaGet = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/v1/public/geo/tenant-by-comuna/{comunaId}");
        Expect(tenantByComunaGet, "api.tenantByComunaGet", HttpStatusCode.OK);
        ExpectJsonProperty(tenantByComunaGet.Body, "tenantId", "tenant-by-comuna GET debería devolver tenantId");
        Pass("tenant-by-comuna GET responde");

        var tenantByComunaPost = await SendAsync(HttpMethod.Post, $"{_apiBaseUrl}/v1/public/geo/tenant-by-comuna/{comunaId}");
        Expect(tenantByComunaPost, "api.tenantByComunaPost", HttpStatusCode.OK);
        ExpectJsonProperty(tenantByComunaPost.Body, "tenantId", "tenant-by-comuna POST debería devolver tenantId");
        Pass("tenant-by-comuna POST responde");

        var categories = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/v1/public/catalog/categories");
        Expect(categories, "api.categories", HttpStatusCode.OK);
        var categoryCode = ReadFirstStringFromArray(categories.Body, "code");
        Pass("Categorías públicas responden");

        var discovery = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/v1/public/catalog/discovery/{categoryCode}");
        Expect(discovery, "api.discovery", HttpStatusCode.OK);
        ExpectJsonProperty(discovery.Body, "category", "discovery debería incluir category");
        if (!string.IsNullOrWhiteSpace(_expectedDiscoveryPartnerName))
        {
            ExpectContains(discovery.Body, _expectedDiscoveryPartnerName, "discovery no contiene partner esperado");
        }
        Pass("Discovery público responde");
    }

    private async Task CheckTrackingContractsAsync()
    {
        Info("Validando tracking público y protección por customerId");

        var fakeOrderId = Guid.NewGuid();
        var fakeBookingId = Guid.NewGuid();

        var orderWithoutCustomer = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/v1/public/orders/{fakeOrderId}");
        Expect(orderWithoutCustomer, "api.publicOrderWithoutCustomer", HttpStatusCode.NotFound);
        Pass("Tracking público de order exige customerId");

        var bookingWithoutCustomer = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/v1/public/bookings/{fakeBookingId}");
        Expect(bookingWithoutCustomer, "api.publicBookingWithoutCustomer", HttpStatusCode.NotFound);
        Pass("Tracking público de booking exige customerId");
    }

    private async Task CheckAuthenticatedOwnershipAsync()
    {
        string token;
        if (!string.IsNullOrWhiteSpace(_qaAccessToken))
        {
            Info("Validando ownership y módulo partner con token QA inyectado");
            token = _qaAccessToken;
            Pass("Token QA inyectado disponible para checks autenticados");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(_qaEmail) || string.IsNullOrWhiteSpace(_qaPassword))
            {
                if (_strictAuthChecks)
                {
                    throw new InvalidOperationException("QUALITY_STRICT_AUTH_CHECKS=1 exige QUALITY_ACCESS_TOKEN o QUALITY_QA_EMAIL/QUALITY_QA_PASSWORD.");
                }

                Skip("Checks autenticados omitidos: faltan QUALITY_ACCESS_TOKEN y QUALITY_QA_EMAIL / QUALITY_QA_PASSWORD");
                return;
            }

            Info("Validando login real, ownership y publicación partner");

            var loginPayload = JsonSerializer.Serialize(new
            {
                email = _qaEmail,
                password = _qaPassword,
                tenantId = (Guid?)null,
                partnerId = (Guid?)null,
                recaptchaToken = _qaRecaptchaToken
            }, _json);

            var login = await SendAsync(HttpMethod.Post, $"{_aclBaseUrl}/v1/auth/login", loginPayload);
            Expect(login, "acl.realLogin", HttpStatusCode.OK);
            token = ReadNestedString(login.Body, "accessToken");
            Pass("Login real devuelve accessToken");
        }

        var myBusinesses = await SendAsync(HttpMethod.Get, $"{_appBaseUrl}/my-businesses", bearerToken: token);
        ExpectOneOf(myBusinesses, "app.myBusinesses", HttpStatusCode.OK, HttpStatusCode.Found);
        Pass("Ruta /my-businesses responde con sesión autenticada");

        var partnerMine = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/v1/partners/mine", bearerToken: token);
        Expect(partnerMine, "api.partnersMine", HttpStatusCode.OK);
        Pass("Partners/mine responde con token válido");

        Guid? resolvedOwnPartnerId = null;
        if (TryParseGuid(_ownPartnerId, out var ownPartnerIdFromEnv))
        {
            resolvedOwnPartnerId = ownPartnerIdFromEnv;
        }
        else if (TryReadFirstGuidFromArray(partnerMine.Body, "id", out var ownPartnerIdFromMine))
        {
            resolvedOwnPartnerId = ownPartnerIdFromMine;
        }

        if (resolvedOwnPartnerId.HasValue)
        {
            var ownOrders = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/v1/partners/{resolvedOwnPartnerId.Value}/orders", bearerToken: token);
            ExpectOneOf(ownOrders, "api.ownPartnerOrders", HttpStatusCode.OK, HttpStatusCode.NoContent);
            Pass("Orders del partner propio responden");

            var ownBookings = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/v1/partners/{resolvedOwnPartnerId.Value}/bookings", bearerToken: token);
            ExpectOneOf(ownBookings, "api.ownPartnerBookings", HttpStatusCode.OK, HttpStatusCode.NoContent);
            Pass("Bookings del partner propio responden");
        }
        else
        {
            Skip("Checks positivos de ownership omitidos: falta QUALITY_PARTNER_ID válido");
        }

        if (TryParseGuid(_foreignPartnerId, out var foreignPartnerId))
        {
            var foreignOrders = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/v1/partners/{foreignPartnerId}/orders", bearerToken: token);
            ExpectOneOf(foreignOrders, "api.foreignPartnerOrders", HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
            Pass("Ownership bloquea acceso a orders de partner ajeno");

            var foreignBookings = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/v1/partners/{foreignPartnerId}/bookings", bearerToken: token);
            ExpectOneOf(foreignBookings, "api.foreignPartnerBookings", HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
            Pass("Ownership bloquea acceso a bookings de partner ajeno");
        }
        else
        {
            Skip("Checks negativos de ownership omitidos: falta QUALITY_FOREIGN_PARTNER_ID válido");
        }

        await CheckPartnerModuleAsync(token, resolvedOwnPartnerId, TryParseGuid(_foreignPartnerId, out var foreignPartnerIdForModule) ? foreignPartnerIdForModule : null);
        await CheckFavoritesAuthenticatedAsync(token);
        await CheckBuyerBookingFlowAsync(token);
        await CheckLeadWorkflowAsync(token);
    }

    private async Task CheckPartnerModuleAsync(string token, Guid? ownPartnerId, Guid? foreignPartnerId)
    {
        if (!ownPartnerId.HasValue || ownPartnerId == Guid.Empty)
        {
            Skip("Checks partner autenticados omitidos: no se pudo resolver partner propio.");
            return;
        }

        Info("Validando módulo partner autenticado");

        var partner = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/v1/partners/{ownPartnerId.Value}", bearerToken: token);
        Expect(partner, "api.partner.get", HttpStatusCode.OK);
        ExpectJsonProperty(partner.Body, "id", "Partner GET debería devolver id");
        Pass("Partner profile responde para el partner autenticado");

        var activation = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/v1/partners/{ownPartnerId.Value}/activation", bearerToken: token);
        Expect(activation, "api.partner.activation", HttpStatusCode.OK);
        ExpectJsonProperty(activation.Body, "partnerId", "Partner activation debería devolver partnerId");
        Pass("Partner activation responde para el partner autenticado");

        var products = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/v1/partners/{ownPartnerId.Value}/products", bearerToken: token);
        Expect(products, "api.partner.products", HttpStatusCode.OK);
        Pass("Partner products responde para el partner autenticado");

        var services = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/v1/partners/{ownPartnerId.Value}/services", bearerToken: token);
        Expect(services, "api.partner.services", HttpStatusCode.OK);
        Pass("Partner services responde para el partner autenticado");

        var professionals = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/v1/partners/{ownPartnerId.Value}/professionals", bearerToken: token);
        Expect(professionals, "api.partner.professionals", HttpStatusCode.OK);
        Pass("Partner professionals responde para el partner autenticado");

        var leads = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/v1/partners/{ownPartnerId.Value}/leads", bearerToken: token);
        Expect(leads, "api.partner.leads", HttpStatusCode.OK);
        Pass("Partner leads responde para el partner autenticado");

        var notifications = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/v1/partners/{ownPartnerId.Value}/notifications", bearerToken: token);
        Expect(notifications, "api.partner.notifications", HttpStatusCode.OK);
        Pass("Partner notifications responde para el partner autenticado");

        var payouts = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/v1/partners/{ownPartnerId.Value}/payouts", bearerToken: token);
        Expect(payouts, "api.partner.payouts", HttpStatusCode.OK);
        Pass("Partner payouts responde para el partner autenticado");

        var mpStatus = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/api/sellers/{ownPartnerId.Value}/mercadopago/status", bearerToken: token);
        Expect(mpStatus, "api.partner.mercadopago.status", HttpStatusCode.OK);
        Pass("Marketplace status responde para el seller autenticado");

        var mpPayments = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/api/sellers/{ownPartnerId.Value}/payments", bearerToken: token);
        Expect(mpPayments, "api.partner.mercadopago.payments", HttpStatusCode.OK);
        Pass("Marketplace payments responde para el seller autenticado");

        if (!foreignPartnerId.HasValue || foreignPartnerId == Guid.Empty)
        {
            Skip("Checks partner negativos omitidos: falta QUALITY_FOREIGN_PARTNER_ID válido");
            return;
        }

        var foreignPartner = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/v1/partners/{foreignPartnerId.Value}", bearerToken: token);
        ExpectOneOf(foreignPartner, "api.partner.foreign.get", HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        Pass("Partner profile bloquea acceso a partner ajeno");

        var foreignNotifications = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/v1/partners/{foreignPartnerId.Value}/notifications", bearerToken: token);
        ExpectOneOf(foreignNotifications, "api.partner.foreign.notifications", HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        Pass("Partner notifications bloquea acceso a partner ajeno");

        var foreignPayouts = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/v1/partners/{foreignPartnerId.Value}/payouts", bearerToken: token);
        ExpectOneOf(foreignPayouts, "api.partner.foreign.payouts", HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        Pass("Partner payouts bloquea acceso a partner ajeno");

        var foreignMarketplaceStatus = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/api/sellers/{foreignPartnerId.Value}/mercadopago/status", bearerToken: token);
        ExpectOneOf(foreignMarketplaceStatus, "api.partner.foreign.mercadopago.status", HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        Pass("Marketplace status bloquea acceso a seller ajeno");

        var foreignMarketplacePayments = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/api/sellers/{foreignPartnerId.Value}/payments", bearerToken: token);
        ExpectOneOf(foreignMarketplacePayments, "api.partner.foreign.mercadopago.payments", HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        Pass("Marketplace payments bloquea acceso a seller ajeno");
    }

    private async Task CheckBuyerBookingFlowAsync(string token)
    {
        var serviceIdRaw = !string.IsNullOrWhiteSpace(_bookingFlowServiceId)
            ? _bookingFlowServiceId
            : _favoriteServiceId;

        if (!TryParseGuid(serviceIdRaw, out var serviceId))
        {
            Skip("E2E buyer booking omitido: faltan QUALITY_BOOKING_FLOW_SERVICE_ID o QUALITY_FAVORITE_SERVICE_ID válidos");
            return;
        }

        var publicService = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/v1/public/services/{serviceId}");
        Expect(publicService, "api.publicServiceForBookingFlow", HttpStatusCode.OK);

        var partnerId = ReadNestedGuid(publicService.Body, "partnerId");
        var tenantId = ReadNestedGuid(publicService.Body, "tenantId");
        var durationMinutes = ReadNestedInt(publicService.Body, "durationMinutes", 30);

        var ensurePayload = JsonSerializer.Serialize(new
        {
            tenantId
        }, _json);

        var ensureCustomer = await SendAsync(HttpMethod.Post, $"{_apiBaseUrl}/v1/buyer/customer/ensure", ensurePayload, token);
        Expect(ensureCustomer, "api.ensureBuyerCustomerForBookingFlow", HttpStatusCode.OK);
        var customerId = ReadNestedGuid(ensureCustomer.Body, "id");
        Pass("Buyer customer profile se asegura para el tenant del servicio");

        var startAt = DateTimeOffset.UtcNow.AddDays(2).Date.AddHours(10);
        var endAt = startAt.AddMinutes(Math.Max(durationMinutes, 30));
        var createBookingPayload = JsonSerializer.Serialize(new
        {
            partnerId,
            serviceId,
            slotId = (Guid?)null,
            customerId,
            startAt,
            endAt,
            amount = 0d,
            currency = "CLP",
            cancellationPolicy = (string?)null
        }, _json);

        var created = await SendAsync(HttpMethod.Post, $"{_apiBaseUrl}/v1/bookings", createBookingPayload, token);
        Expect(created, "api.createBookingFlow", HttpStatusCode.Created);
        var bookingId = ReadNestedGuid(created.Body, "id");
        Pass("Creación de reserva buyer responde 201");

        var recent = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/v1/buyer/bookings/recent?limit=8", bearerToken: token);
        Expect(recent, "api.buyerRecentBookings", HttpStatusCode.OK);
        ExpectContains(recent.Body, bookingId.ToString(), "Reservas recientes debería contener la reserva recién creada");
        Pass("Reservas recientes incluyen la nueva reserva");

        var publicTracking = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/v1/public/bookings/{bookingId}?customerId={customerId}");
        Expect(publicTracking, "api.publicBookingTrackingFromFlow", HttpStatusCode.OK);
        ExpectJsonProperty(publicTracking.Body, "service", "tracking de booking debería incluir service");
        Pass("Tracking público de reserva responde para la reserva creada");

        var partnerProfile = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/v1/public/partners/{partnerId}/profile");
        Expect(partnerProfile, "api.publicPartnerProfileForOrderFlow", HttpStatusCode.OK);
        Guid productId;
        double productPrice;
        try
        {
            productId = ReadFirstGuidFromArrayProperty(partnerProfile.Body, "products", "id");
            productPrice = ReadFirstDoubleFromArrayProperty(partnerProfile.Body, "products", "price", 0d);
        }
        catch (InvalidOperationException)
        {
            Skip("E2E tracking positivo de order omitido: partner del servicio no expone productos públicos");
            return;
        }

        var createOrderPayload = JsonSerializer.Serialize(new
        {
            partnerId,
            customerId,
            deliveryFee = 0d,
            currency = "CLP",
            items = new[]
            {
                new
                {
                    productId,
                    quantity = 1,
                    unitPrice = productPrice
                }
            },
            deliveryProviderId = (Guid?)null,
            deliveryAddress = (string?)null
        }, _json);

        var createdOrder = await SendAsync(HttpMethod.Post, $"{_apiBaseUrl}/v1/orders", createOrderPayload, token);
        Expect(createdOrder, "api.createOrderFlow", HttpStatusCode.Created);
        var orderId = ReadNestedGuid(createdOrder.Body, "id");
        Pass("Creación de order buyer responde 201");

        var publicOrderTracking = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/v1/public/orders/{orderId}?customerId={customerId}");
        Expect(publicOrderTracking, "api.publicOrderTrackingFromFlow", HttpStatusCode.OK);
        ExpectJsonProperty(publicOrderTracking.Body, "partner", "tracking de order debería incluir partner");
        Pass("Tracking público de order responde para la order creada");
    }

    private async Task CheckFavoritesAuthenticatedAsync(string token)
    {
        if (!TryParseGuid(_favoriteServiceId, out var serviceId))
        {
            Skip("Checks de favoritos omitidos: falta QUALITY_FAVORITE_SERVICE_ID válido");
            return;
        }

        var createPayload = JsonSerializer.Serialize(new
        {
            type = "service",
            targetId = serviceId,
            tenantId = (Guid?)null
        }, _json);

        var created = await SendAsync(HttpMethod.Post, $"{_apiBaseUrl}/v1/buyer/favorites", createPayload, token);
        ExpectOneOf(created, "api.favoriteCreate", HttpStatusCode.Created, HttpStatusCode.OK);
        var favoriteId = ReadNestedGuid(created.Body, "id");
        Pass("Favorito service se crea o reutiliza correctamente");

        var list = await SendAsync(HttpMethod.Get, $"{_apiBaseUrl}/v1/buyer/favorites?type=service", bearerToken: token);
        Expect(list, "api.favoriteList", HttpStatusCode.OK);
        ExpectContains(list.Body, serviceId.ToString(), "Listado de favoritos debería contener el serviceId guardado");
        Pass("Listado de favoritos devuelve el item esperado");

        var deleted = await SendAsync(HttpMethod.Delete, $"{_apiBaseUrl}/v1/buyer/favorites/{favoriteId}", bearerToken: token);
        Expect(deleted, "api.favoriteDelete", HttpStatusCode.NoContent);
        Pass("Eliminación de favorito responde correctamente");
    }

    private async Task CheckOptionalRegisterAsync()
    {
        if (!_allowRegister)
        {
            Skip("Registro real omitido: QUALITY_ALLOW_REGISTER no está activo");
            return;
        }

        if (string.IsNullOrWhiteSpace(_qaRecaptchaToken))
        {
            Skip("Registro real omitido: falta QUALITY_QA_RECAPTCHA_TOKEN");
            return;
        }

        var uniqueEmail = $"qa+{DateTimeOffset.UtcNow:yyyyMMddHHmmss}@comunaclic.test";
        var registerPayload = JsonSerializer.Serialize(new
        {
            name = "QA Auto Register",
            email = uniqueEmail,
            password = "test12345",
            recaptchaToken = _qaRecaptchaToken
        }, _json);

        var register = await SendAsync(HttpMethod.Post, $"{_aclBaseUrl}/v1/auth/register", registerPayload);
        Expect(register, "acl.realRegister", HttpStatusCode.OK);
        ExpectJsonProperty(register.Body, "accessToken", "register real debería devolver accessToken");
        Pass("Registro real crea una cuenta nueva");
    }

    private async Task CheckLeadWorkflowAsync(string token)
    {
        if (!TryParseGuid(_leadWorkflowLeadId, out var leadId))
        {
            Skip("Checks de lead workflow omitidos: falta QUALITY_LEAD_ID válido");
            return;
        }

        var owner = string.IsNullOrWhiteSpace(_leadWorkflowOwner) ? "QA Operaciones" : _leadWorkflowOwner;
        var outcomeReason = string.IsNullOrWhiteSpace(_leadWorkflowOutcomeReason)
            ? "sin respuesta despues de seguimiento"
            : _leadWorkflowOutcomeReason;

        var followUpPayload = JsonSerializer.Serialize(new
        {
            status = "in_follow_up",
            priority = "normal",
            owner,
            nextFollowUpAt = DateTimeOffset.UtcNow.AddHours(2),
            internalNote = "QA workflow check",
            outcomeReason = (string?)null
        }, _json);

        var followUp = await SendAsync(HttpMethod.Patch, $"{_apiBaseUrl}/v1/leads/{leadId}/workflow", followUpPayload, token);
        Expect(followUp, "api.leadWorkflow.followUp", HttpStatusCode.OK);
        ExpectContains(followUp.Body, "\"status\":\"in_follow_up\"", "workflow debería quedar en in_follow_up");
        Pass("Lead workflow permite actualizar seguimiento operativo");

        var lostWithoutReasonPayload = JsonSerializer.Serialize(new
        {
            status = "lost",
            outcomeReason = ""
        }, _json);

        var lostWithoutReason = await SendAsync(HttpMethod.Patch, $"{_apiBaseUrl}/v1/leads/{leadId}/workflow", lostWithoutReasonPayload, token);
        Expect(lostWithoutReason, "api.leadWorkflow.lostWithoutReason", HttpStatusCode.BadRequest);
        Pass("Lead workflow exige motivo al marcar como perdido");

        var lostWithReasonPayload = JsonSerializer.Serialize(new
        {
            status = "lost",
            outcomeReason
        }, _json);

        var lostWithReason = await SendAsync(HttpMethod.Patch, $"{_apiBaseUrl}/v1/leads/{leadId}/workflow", lostWithReasonPayload, token);
        Expect(lostWithReason, "api.leadWorkflow.lostWithReason", HttpStatusCode.OK);
        ExpectContains(lostWithReason.Body, "\"status\":\"lost\"", "workflow debería quedar en lost");
        Pass("Lead workflow permite cerrar como perdido con motivo");
    }

    private async Task<ResponseData> SendAsync(HttpMethod method, string url, string? jsonBody = null, string? bearerToken = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (!string.IsNullOrWhiteSpace(jsonBody))
        {
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        }

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        using var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        return new ResponseData(response.StatusCode, body, response.Headers, response.Content.Headers);
    }

    private static string ReadSetting(string key, string fallback)
        => Environment.GetEnvironmentVariable(key)?.Trim() is { Length: > 0 } value ? value : fallback;

    private static string? ReadOptional(string key)
        => Environment.GetEnvironmentVariable(key)?.Trim() is { Length: > 0 } value ? value : null;

    private static bool ReadBool(string key)
        => bool.TryParse(Environment.GetEnvironmentVariable(key), out var parsed) && parsed;

    private static bool TryParseGuid(string? raw, out Guid value)
        => Guid.TryParse(raw, out value) && value != Guid.Empty;

    private static bool TryReadFirstGuidFromArray(string json, string propertyName, out Guid parsed)
    {
        parsed = Guid.Empty;
        try
        {
            parsed = ReadFirstGuidFromArray(json, propertyName);
            return parsed != Guid.Empty;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static Guid ReadFirstGuidFromArray(string json, string propertyName)
    {
        using var document = JsonDocument.Parse(json);
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.TryGetProperty(propertyName, out var property) &&
                property.ValueKind == JsonValueKind.String &&
                Guid.TryParse(property.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        throw new InvalidOperationException($"No se encontró GUID para '{propertyName}'.");
    }

    private static string ReadFirstStringFromArray(string json, string propertyName)
    {
        using var document = JsonDocument.Parse(json);
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.TryGetProperty(propertyName, out var property) &&
                property.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(property.GetString()))
            {
                return property.GetString()!;
            }
        }

        throw new InvalidOperationException($"No se encontró string para '{propertyName}'.");
    }

    private static string ReadNestedString(string json, string propertyName)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty(propertyName, out var direct) && direct.ValueKind == JsonValueKind.String)
        {
            return direct.GetString()!;
        }

        throw new InvalidOperationException($"No se encontró propiedad string '{propertyName}'.");
    }

    private static Guid ReadNestedGuid(string json, string propertyName)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty(propertyName, out var direct) &&
            direct.ValueKind == JsonValueKind.String &&
            Guid.TryParse(direct.GetString(), out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"No se encontró GUID válido en '{propertyName}'.");
    }

    private static int ReadNestedInt(string json, string propertyName, int fallback)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty(propertyName, out var direct))
        {
            if (direct.ValueKind == JsonValueKind.Number && direct.TryGetInt32(out var number))
            {
                return number;
            }

            if (direct.ValueKind == JsonValueKind.String && int.TryParse(direct.GetString(), out number))
            {
                return number;
            }
        }

        return fallback;
    }

    private static Guid ReadFirstGuidFromArrayProperty(string json, string arrayPropertyName, string propertyName)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty(arrayPropertyName, out var arrayElement) || arrayElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"No se encontró arreglo '{arrayPropertyName}'.");
        }

        foreach (var item in arrayElement.EnumerateArray())
        {
            if (item.TryGetProperty(propertyName, out var property) &&
                property.ValueKind == JsonValueKind.String &&
                Guid.TryParse(property.GetString(), out var parsed) &&
                parsed != Guid.Empty)
            {
                return parsed;
            }
        }

        throw new InvalidOperationException($"No se encontró GUID en '{arrayPropertyName}.{propertyName}'.");
    }

    private static double ReadFirstDoubleFromArrayProperty(string json, string arrayPropertyName, string propertyName, double fallback)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty(arrayPropertyName, out var arrayElement) || arrayElement.ValueKind != JsonValueKind.Array)
        {
            return fallback;
        }

        foreach (var item in arrayElement.EnumerateArray())
        {
            if (item.TryGetProperty(propertyName, out var property))
            {
                if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var number))
                {
                    return number;
                }

                if (property.ValueKind == JsonValueKind.String && double.TryParse(property.GetString(), out number))
                {
                    return number;
                }
            }
        }

        return fallback;
    }

    private static void ExpectJsonProperty(string json, string propertyName, string message)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty(propertyName, out _))
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void ExpectContains(string actual, string expected, string message)
    {
        if (!actual.Contains(expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void ExpectHeader(ResponseData response, string headerName, string message)
    {
        var hasHeader = response.Headers.TryGetValues(headerName, out _) || response.ContentHeaders.TryGetValues(headerName, out _);
        if (!hasHeader)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void Expect(ResponseData response, string name, HttpStatusCode expected)
    {
        if (response.StatusCode != expected)
        {
            throw new InvalidOperationException($"{name} devolvió {(int)response.StatusCode} y se esperaba {(int)expected}. Body: {response.Body}");
        }
    }

    private static void ExpectOneOf(ResponseData response, string name, params HttpStatusCode[] expectedStatuses)
    {
        if (!expectedStatuses.Contains(response.StatusCode))
        {
            var expected = string.Join(", ", expectedStatuses.Select(status => ((int)status).ToString()));
            throw new InvalidOperationException($"{name} devolvió {(int)response.StatusCode} y se esperaba uno de: {expected}. Body: {response.Body}");
        }
    }

    private void Info(string message) => Console.WriteLine($"[INFO] {message}");

    private void Pass(string message)
    {
        _passCount++;
        Console.WriteLine($"[PASS] {message}");
    }

    private void Skip(string message)
    {
        _skipCount++;
        Console.WriteLine($"[SKIP] {message}");
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Quality checks mínimos para ComunaClic.");
        Console.WriteLine();
        Console.WriteLine("Uso:");
        Console.WriteLine("  dotnet run --project src/ComunaClick.QualityChecks/ComunaClick.QualityChecks.csproj");
        Console.WriteLine();
        Console.WriteLine("Variables principales:");
        Console.WriteLine("  QUALITY_APP_BASE_URL");
        Console.WriteLine("  QUALITY_ACL_BASE_URL");
        Console.WriteLine("  QUALITY_API_BASE_URL");
        Console.WriteLine("  QUALITY_ACCESS_TOKEN");
        Console.WriteLine("  QUALITY_QA_EMAIL");
        Console.WriteLine("  QUALITY_QA_PASSWORD");
        Console.WriteLine("  QUALITY_QA_RECAPTCHA_TOKEN");
        Console.WriteLine("  QUALITY_PARTNER_ID");
        Console.WriteLine("  QUALITY_FOREIGN_PARTNER_ID");
        Console.WriteLine("  QUALITY_FAVORITE_SERVICE_ID");
        Console.WriteLine("  QUALITY_BOOKING_FLOW_SERVICE_ID");
        Console.WriteLine("  QUALITY_LEAD_ID");
        Console.WriteLine("  QUALITY_LEAD_OWNER");
        Console.WriteLine("  QUALITY_LEAD_OUTCOME_REASON");
        Console.WriteLine("  QUALITY_ALLOW_REGISTER=true");
        Console.WriteLine("  QUALITY_STRICT_AUTH_CHECKS=true");
    }

    private sealed record ResponseData(
        HttpStatusCode StatusCode,
        string Body,
        HttpResponseHeaders Headers,
        HttpContentHeaders ContentHeaders);
}
