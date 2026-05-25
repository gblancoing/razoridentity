using ComunaClick.Shared.Auth;

namespace ComunaClick.SharedUI.Services;

/// <summary>
/// Tokens del circuito Blazor actual (evita desfase entre AuthState, localStorage y HttpClient).
/// </summary>
public sealed class SessionTokenHolder
{
    public AuthTokens? Current { get; set; }
}
