using ComunaClick.Shared.Partner.Interfaces;
using ComunaClick.Shared.Partner.Leads;

namespace ComunaClick.SharedUI.Services.Mocks;

public sealed class MockPartnerLeadService : IPartnerLeadService
{
    public Task<IReadOnlyList<LeadDto>> GetLeadsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.Now;
        IReadOnlyList<LeadDto> items = new List<LeadDto>
        {
            new(Guid.NewGuid(), "María L.", "Busca servicio de manicure", "Nuevo", now.AddHours(-2)),
            new(Guid.NewGuid(), "José P.", "Cotización para remodelación", "En seguimiento", now.AddDays(-1)),
            new(Guid.NewGuid(), "Carla G.", "Consulta por clases", "Nuevo", now.AddHours(-6))
        };

        return Task.FromResult(items);
    }
}
