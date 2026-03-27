using System.Text.Json.Serialization;
using Refit;

namespace Overflow.DataSeederService.Clients;

public interface IKeycloakTokenClient
{
    [Post("/protocol/openid-connect/token")]
    Task<KeycloakTokenResponse> GetClientCredentialsTokenAsync(
        [Body(BodySerializationMethod.UrlEncoded)]
        ClientCredentialsRequest request,
        CancellationToken ct = default);

    [Post("/protocol/openid-connect/token")]
    Task<KeycloakTokenResponse> GetPasswordGrantTokenAsync(
        [Body(BodySerializationMethod.UrlEncoded)]
        PasswordGrantRequest request,
        CancellationToken ct = default);
}

public class KeycloakTokenResponse
{
    [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    [JsonPropertyName("token_type")] public string? TokenType { get; set; }
}

public class ClientCredentialsRequest
{
    [AliasAs("grant_type")] public string GrantType { get; set; } = "client_credentials";
    [AliasAs("client_id")] public required string ClientId { get; set; }
    [AliasAs("client_secret")] public required string ClientSecret { get; set; }
}

public class PasswordGrantRequest
{
    [AliasAs("grant_type")] public string GrantType { get; set; } = "password";
    [AliasAs("client_id")] public required string ClientId { get; set; }
    [AliasAs("client_secret")] public string? ClientSecret { get; set; }
    [AliasAs("username")] public required string Username { get; set; }

    [AliasAs("password")] public required string Password { get; set; }
}