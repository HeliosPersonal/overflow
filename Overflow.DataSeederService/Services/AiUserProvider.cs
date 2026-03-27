using CSharpFunctionalExtensions;
using Microsoft.Extensions.Options;
using Overflow.DataSeederService.Clients;
using Overflow.DataSeederService.Keycloak;
using Overflow.DataSeederService.Models;

namespace Overflow.DataSeederService.Services;

public class AiUserProvider(
    KeycloakAdminService keycloakAdmin,
    IProfileApiClient profileApi,
    IOptions<AiAnswerOptions> options,
    ILogger<AiUserProvider> logger)
{
    private const string DefaultLastName = "Bot";

    private readonly AiAnswerOptions _opts = options.Value;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private AiUser? _user;
    private bool _lazyAttempted;

    public async Task<Result<AiUser>> GetUserAsync(CancellationToken ct = default)
    {
        if (_user != null)
        {
            return _user;
        }

        if (string.IsNullOrWhiteSpace(_opts.AiEmail) || string.IsNullOrWhiteSpace(_opts.AiPassword))
        {
            return Result.Failure<AiUser>("AiEmail or AiPassword is not configured");
        }

        await _lock.WaitAsync(ct);
        try
        {
            if (_user != null)
            {
                return _user;
            }

            if (_lazyAttempted)
            {
                return Result.Failure<AiUser>("Previous bootstrap failed — restart to retry");
            }

            logger.LogInformation("Lazy bootstrap — AI user not ready yet");
            var result = await BootstrapAsync(ct);
            return result.IsFailure
                ? Result.Failure<AiUser>(result.Error)
                : _user!;
        }
        catch (Exception ex)
        {
            _lazyAttempted = true;
            logger.LogError(ex, "Lazy bootstrap failed");
            return Result.Failure<AiUser>(ex.Message);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<Result> BootstrapAsync(CancellationToken ct = default)
    {
        if (_user != null)
        {
            return Result.Success();
        }

        logger.LogInformation("Bootstrapping AI user '{Name}' ({Email})", _opts.AiDisplayName, _opts.AiEmail);

        var (firstName, lastName) = ParseDisplayName(_opts.AiDisplayName);

        var createResult = await keycloakAdmin.CreateUserAsync(
            _opts.AiEmail, firstName, lastName, _opts.AiPassword, ct);
        if (createResult.IsFailure)
        {
            return Result.Failure($"User creation failed: {createResult.Error}");
        }

        var tokenResult = await keycloakAdmin.GetUserTokenAsync(_opts.AiEmail, _opts.AiPassword, ct);
        if (tokenResult.IsFailure)
        {
            return Result.Failure($"Authentication failed: {tokenResult.Error}");
        }

        await EnsureProfileExistsAsync(tokenResult.Value, ct);

        _user = new AiUser(createResult.Value, _opts.AiEmail, tokenResult.Value);
        logger.LogInformation("AI user ready — ID: {UserId}", createResult.Value);
        return Result.Success();
    }

    public async Task<Result<string>> GetFreshTokenAsync(CancellationToken ct = default)
    {
        if (_user == null)
        {
            return Result.Failure<string>("AI user not bootstrapped");
        }

        var result = await keycloakAdmin.GetUserTokenAsync(_user.Email, _opts.AiPassword, ct);
        if (result.IsSuccess)
        {
            _user = _user with { Token = result.Value };
        }

        return result;
    }

    private async Task EnsureProfileExistsAsync(string token, CancellationToken ct)
    {
        try
        {
            await profileApi.GetMyProfileAsync($"Bearer {token}", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Profile auto-creation failed (will retry on next answer)");
        }
    }

    private static (string First, string Last) ParseDisplayName(string displayName)
    {
        var parts = displayName.Split(' ', 2);
        return (parts[0], parts.Length > 1 ? parts[1] : DefaultLastName);
    }
}