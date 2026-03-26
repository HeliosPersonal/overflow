using System.Text.Json.Serialization;
using Refit;

namespace Overflow.DataSeederService.Clients;

/// <summary>
///     Keycloak Admin REST API. Base URL: /admin/realms/{realm}. Token injected by
///     <see cref="AdminBearerTokenHandler" />.
/// </summary>
[Headers("Content-Type: application/json")]
public interface IKeycloakAdminClient
{
    [Post("/users")]
    Task<HttpResponseMessage> CreateUserAsync(
        [Body] KeycloakCreateUserRequest body,
        CancellationToken cancellationToken = default);

    [Get("/users")]
    Task<List<KeycloakUserDto>> GetUserByUsernameExactAsync(
        [Query] string username,
        [Query] bool exact = true,
        CancellationToken cancellationToken = default);
}

public class KeycloakCreateUserRequest
{
    [JsonPropertyName("username")] public required string Username { get; set; }

    [JsonPropertyName("email")] public required string Email { get; set; }

    [JsonPropertyName("firstName")] public required string FirstName { get; set; }

    [JsonPropertyName("lastName")] public required string LastName { get; set; }

    [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;

    [JsonPropertyName("emailVerified")] public bool EmailVerified { get; set; } = true;

    [JsonPropertyName("credentials")] public List<KeycloakCredentialDto> Credentials { get; set; } = new();
}

public class KeycloakCredentialDto
{
    [JsonPropertyName("type")] public string Type { get; set; } = "password";

    [JsonPropertyName("value")] public required string Value { get; set; }

    [JsonPropertyName("temporary")] public bool Temporary { get; set; } = false;
}

public class KeycloakUserDto
{
    [JsonPropertyName("id")] public string? Id { get; set; }

    [JsonPropertyName("username")] public string? Username { get; set; }
}