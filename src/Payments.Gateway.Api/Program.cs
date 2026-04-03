using Microsoft.EntityFrameworkCore;
using Payments.Common.Interfaces;
using Payments.Gateway.Api.Persistence;
using Payments.Gateway.Api.Services;
using Payments.Gateway.Api.Services.Providers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<PaymentsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("PaymentsDb")));
builder.Services.AddHttpClient<IComunaClicNotifier, ComunaClicNotifier>();
builder.Services.Configure<TransbankProviderOptions>(builder.Configuration.GetSection("PaymentProviders:Transbank"));
builder.Services.Configure<KhipuProviderOptions>(builder.Configuration.GetSection("PaymentProviders:Khipu"));
builder.Services.Configure<MercadoPagoProviderOptions>(builder.Configuration.GetSection("PaymentProviders:MercadoPago"));
builder.Services.AddHttpClient<TransbankPaymentProvider>();
builder.Services.AddHttpClient<KhipuPaymentProvider>();
builder.Services.AddHttpClient<MercadoPagoPaymentProvider>();
builder.Services.AddScoped<IPaymentProvider>(sp => sp.GetRequiredService<TransbankPaymentProvider>());
builder.Services.AddScoped<IPaymentProvider>(sp => sp.GetRequiredService<KhipuPaymentProvider>());
builder.Services.AddScoped<IPaymentProvider>(sp => sp.GetRequiredService<MercadoPagoPaymentProvider>());
builder.Services.AddScoped<IPaymentProviderResolver, PaymentProviderResolver>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapControllers();

app.Run();
