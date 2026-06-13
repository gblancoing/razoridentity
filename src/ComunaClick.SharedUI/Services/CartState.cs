using Microsoft.JSInterop;

namespace ComunaClick.SharedUI.Services;

/// <summary>
/// Estado compartido del carrito de invitado dentro del circuito Blazor:
/// expone el total de unidades para el badge del header y notifica a los
/// suscriptores cuando una página modifica el carrito (agregar, quitar,
/// crear pedido). La fuente de verdad sigue siendo localStorage (app.js).
/// </summary>
public sealed class CartState
{
    public int Count { get; private set; }

    public event Action? OnChange;

    /// <summary>Relee el contador desde localStorage y avisa a los suscriptores.</summary>
    public async Task RefreshAsync(IJSRuntime jsRuntime)
    {
        try
        {
            Count = await jsRuntime.InvokeAsync<int>("comunaclic.getGuestCartCount");
        }
        catch
        {
            // Prerender o storage inaccesible: se mantiene el último valor.
            return;
        }

        OnChange?.Invoke();
    }
}
