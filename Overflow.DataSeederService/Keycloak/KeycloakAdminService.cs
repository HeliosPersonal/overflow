using System.Net;
using Microsoft.Extensions.Options;
using Overflow.Common.Options;
using Overflow.DataSeederService.Clients;
using Refit;

namespace Overflow.DataSeederService.Keycloak;

/// <summary>Wraps Keycloak Admin REST API — token management, user creation, user lookup.</summary>
public class KeycloakAdminService(
    IKeycloakTokenClient tokenClient,
    IKeycloakAdminClient adminClient,
    IOptions<KeycloakOptions> keycloakOptions,
    ILogger<KeycloakAdminService> logger)
{
    private readonly KeycloakOptions _kc = keycloakOptions.Value;
    private string? _adminToken;
    private DateTime _adminTokenExpiry = DateTime.MinValue;

    private async Task<string?> GetAdminTokenAsync(CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(_adminToken) && DateTime.UtcNow < _adminTokenExpiry.AddMinutes(-1))
        {
            return _adminToken;
        }

        try
        {
            var response = await tokenClient.GetClientCredentialsTokenAsync(
                new ClientCredentialsRequest
                {
                    ClientId = _kc.AdminClientId
                               ?? throw new InvalidOperationException("AdminClientId is required"),
                    ClientSecret = _kc.AdminClientSecret
                                   ?? throw new InvalidOperationException("AdminClientSecret is required")
                },
                cancellationToken);

            if (response.AccessToken == null)
            {
                logger.LogError("Admin token response did not contain an access token");
                return null;
            }

            _adminToken = response.AccessToken;
            _adminTokenExpiry = DateTime.UtcNow.AddSeconds(response.ExpiresIn);
            logger.LogDebug("Obtained admin token (expires in {Seconds}s)", response.ExpiresIn);
            return _adminToken;
        }
        catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException)
        {
            logger.LogError(
                "Admin token request timed out or was canceled (Keycloak URL: {Url}). " +
                "This usually means the Keycloak endpoint is unreachable or the resilience pipeline timeout was exceeded.",
                $"{_kc.Url}/realms/{_kc.Realm}");
            return null;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex,
                "HTTP error obtaining admin token (Keycloak URL: {Url}). Check network connectivity and Keycloak health.",
                $"{_kc.Url}/realms/{_kc.Realm}");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error obtaining admin token");
            return null;
        }
    }

    public async Task<string?> GetUserTokenAsync(
        string username, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await tokenClient.GetPasswordGrantTokenAsync(
                new PasswordGrantRequest
                {
                    ClientId = _kc.NextJsClientId
                               ?? throw new InvalidOperationException("NextJsClientId is required"),
                    ClientSecret = string.IsNullOrEmpty(_kc.NextJsClientSecret)
                        ? null
                        : _kc.NextJsClientSecret,
                    Username = username,
                    Password = password
                },
                cancellationToken);

            if (response.AccessToken == null)
            {
                logger.LogError("Token response for {Username} did not contain an access token", username);
                return null;
            }

            logger.LogDebug("Obtained token for user {Username}", username);
            return response.AccessToken;
        }
        catch (ApiException ex)
        {
            logger.LogError("Failed to obtain token for {Username}: {Status} — {Content}",
                username, ex.StatusCode, ex.Content);
            return null;
        }
        catch (Exception ex) when (ex is TaskCanceledException or OperationCanceledException)
        {
            logger.LogError(
                "Token request for {Username} timed out or was canceled (Keycloak URL: {Url}). " +
                "This usually means the Keycloak endpoint is unreachable or the resilience pipeline timeout was exceeded.",
                username, $"{_kc.Url}/realms/{_kc.Realm}");
            return null;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex,
                "HTTP error obtaining token for {Username} (Keycloak URL: {Url}). " +
                "Check network connectivity and Keycloak health.",
                username, $"{_kc.Url}/realms/{_kc.Realm}");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error obtaining token for {Username}", username);
            return null;
        }
    }

    public async Task<string?> CreateUserAsync(
        string email, string firstName, string lastName, string password,
        CancellationToken cancellationToken = default)
    {
        var adminToken = await GetAdminTokenAsync(cancellationToken);
        if (adminToken == null)
        {
            return null;
        }

        AdminTokenAccessor.Current = adminToken;

        try
        {
            var body = new KeycloakCreateUserRequest
            {
                Username = email,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                Enabled = true,
                EmailVerified = true,
                Credentials = [new KeycloakCredentialDto { Value = password }]
            };

            var response = await adminClient.CreateUserAsync(body, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                logger.LogWarning("User {Email} already exists", email);
                return await GetUserIdByUsernameAsync(email, cancellationToken);
            }

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError("Failed to create user {Email}: {Status} — {Error}", email, response.StatusCode, error);
                return null;
            }

            var location = response.Headers.Location?.ToString();
            if (location != null)
            {
                var userId = location.Split('/').Last();
                logger.LogInformation("Created user {Email} (ID: {Id})", email, userId);
                return userId;
            }

            return await GetUserIdByUsernameAsync(email, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating user {Email}", email);
            return null;
        }
    }

    private async Task<string?> GetUserIdByUsernameAsync(
        string username, CancellationToken cancellationToken = default)
    {
        var adminToken = await GetAdminTokenAsync(cancellationToken);
        if (adminToken == null)
        {
            return null;
        }

        AdminTokenAccessor.Current = adminToken;

        try
        {
            var users = await adminClient.GetUserByUsernameExactAsync(username, true, cancellationToken);
            return users.FirstOrDefault()?.Id;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error looking up user {Username}", username);
            return null;
        }
    }
}