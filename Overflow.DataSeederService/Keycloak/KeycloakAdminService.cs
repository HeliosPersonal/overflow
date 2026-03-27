using System.Net;
using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using Overflow.Common.Options;
using Overflow.DataSeederService.Clients;
using Refit;

namespace Overflow.DataSeederService.Keycloak;

public class KeycloakAdminService(
    IKeycloakTokenClient tokenClient,
    IKeycloakAdminClient adminClient,
    IOptions<KeycloakOptions> keycloakOptions,
    ILogger<KeycloakAdminService> logger)
{
    private readonly KeycloakOptions _kc = keycloakOptions.Value;
    private string? _adminToken;
    private DateTime _adminTokenExpiry = DateTime.MinValue;

    public async Task<Result<string>> GetUserTokenAsync(
        string username, string password, CancellationToken ct = default)
    {
        return await ExecuteKeycloakRequest("GetUserToken", async () =>
        {
            var response = await tokenClient.GetPasswordGrantTokenAsync(
                new PasswordGrantRequest
                {
                    ClientId = _kc.NextJsClientId ?? throw new InvalidOperationException("NextJsClientId is required"),
                    ClientSecret = string.IsNullOrEmpty(_kc.NextJsClientSecret) ? null : _kc.NextJsClientSecret,
                    Username = username,
                    Password = password
                }, ct);

            return response.AccessToken != null
                ? Result.Success(response.AccessToken)
                : Result.Failure<string>("Token response missing access_token");
        });
    }

    public async Task<Result<string>> CreateUserAsync(
        string email, string firstName, string lastName, string password,
        CancellationToken ct = default)
    {
        var tokenResult = await GetAdminTokenAsync(ct);
        if (tokenResult.IsFailure)
        {
            return tokenResult;
        }

        AdminTokenAccessor.Current = tokenResult.Value;

        return await ExecuteKeycloakRequest("CreateUser", async () =>
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

            var response = await adminClient.CreateUserAsync(body, ct);

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                logger.LogInformation("User {Email} already exists — looking up ID", email);
                return await LookupUserIdAsync(email, ct);
            }

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                return Result.Failure<string>($"HTTP {(int)response.StatusCode}: {error}");
            }

            var location = response.Headers.Location?.ToString();
            if (location != null)
            {
                var userId = location.Split('/').Last();
                logger.LogInformation("Created Keycloak user {Email} (ID: {Id})", email, userId);
                return Result.Success(userId);
            }

            return await LookupUserIdAsync(email, ct);
        });
    }

    private async Task<Result<string>> GetAdminTokenAsync(CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_adminToken) && DateTime.UtcNow < _adminTokenExpiry.AddMinutes(-1))
        {
            return _adminToken;
        }

        return await ExecuteKeycloakRequest("GetAdminToken", async () =>
        {
            var response = await tokenClient.GetClientCredentialsTokenAsync(
                new ClientCredentialsRequest
                {
                    ClientId = _kc.AdminClientId ?? throw new InvalidOperationException("AdminClientId is required"),
                    ClientSecret = _kc.AdminClientSecret ??
                                   throw new InvalidOperationException("AdminClientSecret is required")
                }, ct);

            if (response.AccessToken == null)
            {
                return Result.Failure<string>("Admin token response missing access_token");
            }

            _adminToken = response.AccessToken;
            _adminTokenExpiry = DateTime.UtcNow.AddSeconds(response.ExpiresIn);
            return Result.Success(_adminToken);
        });
    }

    private async Task<Result<string>> LookupUserIdAsync(string username, CancellationToken ct)
    {
        var tokenResult = await GetAdminTokenAsync(ct);
        if (tokenResult.IsFailure)
        {
            return tokenResult;
        }

        AdminTokenAccessor.Current = tokenResult.Value;

        var users = await adminClient.GetUserByUsernameExactAsync(username, true, ct);
        var userId = users.FirstOrDefault()?.Id;

        return userId != null
            ? Result.Success(userId)
            : Result.Failure<string>($"User '{username}' not found after creation");
    }

    private async Task<Result<string>> ExecuteKeycloakRequest(
        string operation, Func<Task<Result<string>>> action)
    {
        try
        {
            return await action();
        }
        catch (ApiException ex)
        {
            logger.LogError("{Op} failed: {Status} — {Content}", operation, ex.StatusCode, ex.Content);
            return Result.Failure<string>($"{operation}: HTTP {(int)ex.StatusCode}");
        }
        catch (OperationCanceledException)
        {
            logger.LogError("{Op} timed out (Keycloak: {Url})", operation, KeycloakUrl);
            return Result.Failure<string>($"{operation}: request timed out");
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "{Op} HTTP error (Keycloak: {Url})", operation, KeycloakUrl);
            return Result.Failure<string>($"{operation}: {ex.Message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Op} unexpected error", operation);
            return Result.Failure<string>($"{operation}: {ex.Message}");
        }
    }

    private string KeycloakUrl => $"{_kc.Url}/realms/{_kc.Realm}";
}