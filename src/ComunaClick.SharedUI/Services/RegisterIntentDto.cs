using System.Text.Json.Serialization;

namespace ComunaClick.SharedUI.Services;

public sealed class RegisterIntentDto
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = RegistrationFlow.RoleNatural;

    [JsonPropertyName("returnUrl")]
    public string ReturnUrl { get; set; } = string.Empty;
}
