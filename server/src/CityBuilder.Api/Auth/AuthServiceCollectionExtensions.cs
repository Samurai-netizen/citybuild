using CityBuilder.Application.Players;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace CityBuilder.Api.Auth;

/// <summary>Registers the authentication of the access tokens in the composition root (core-instructions/07-security-model.md).</summary>
public static class AuthServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="AuthOptions"/> (validated on start: a signing key of at least <see cref="AuthOptions.MinSigningKeyBytes"/>
    /// bytes, a lifetime in range, and the dev login on only in Development), <see cref="AccessTokens"/>, the JWT bearer handler as the
    /// default scheme with <see cref="AccessTokens.ValidationParameters"/>, authorization, and <see cref="ICurrentPlayer"/>. The handler keeps
    /// the token's claim names (<c>sub</c> stays <c>sub</c>), and a refused request gets <see cref="BearerChallenge"/>'s Problem Details answer.
    /// Both outcomes are logged (<see cref="AuthLog"/>): an accepted token at Debug, a refusal with its kind, never the token.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The app's configuration; the section <see cref="AuthOptions.SectionName"/> is bound.</param>
    /// <param name="environment">The host environment; the dev login is allowed only in Development.</param>
    /// <returns>The same service collection.</returns>
    public static IServiceCollection AddCityBuilderAuthentication(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName))
            .Validate(options => options.HasLongEnoughSigningKey(),
                $"Auth:SigningKey must hold the token signing key, at least {AuthOptions.MinSigningKeyBytes} bytes (user-secrets or the " +
                "environment variable Auth__SigningKey; docs/getting-started.md → Configuration).")
            .Validate(options => options.TokenLifetimeMinutes is >= AuthOptions.MinTokenLifetimeMinutes
                    and <= AuthOptions.MaxTokenLifetimeMinutes,
                $"Auth:TokenLifetimeMinutes must be between {AuthOptions.MinTokenLifetimeMinutes} and {AuthOptions.MaxTokenLifetimeMinutes}.")
            .Validate(options => !options.DevLoginEnabled || environment.IsDevelopment(),
                "Auth:DevLoginEnabled may be true only in Development: the dev login lets anyone in by name alone " +
                "(core-instructions/07-security-model.md).")
            .ValidateOnStart();

        services.AddSingleton<AccessTokens>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<AccessTokens, ILoggerFactory>((jwt, tokens, loggers) =>
            {
                ILogger logger = loggers.CreateLogger(typeof(BearerChallenge).FullName!);
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters = tokens.ValidationParameters();
                jwt.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        // A signed token that names no player is as useless as an unsigned one: the same 401, not a 500 at the first read of the player.
                        if (HttpCurrentPlayer.Find(context.Principal) is { } playerId)
                        {
                            AuthLog.TokenAccepted(logger, playerId);
                        }
                        else
                        {
                            context.Fail("The token names no player.");
                        }

                        return Task.CompletedTask;
                    },
                    OnChallenge = context =>
                    {
                        // The kind of failure only: the exception's message can quote parts of the token.
                        string reason = context.AuthenticateFailure?.GetType().Name ?? "NoToken";
                        AuthLog.TokenRefused(logger, context.Request.Method, context.Request.Path.Value ?? string.Empty, reason);
                        return BearerChallenge.WriteProblemAsync(context);
                    },
                };
            });
        services.AddAuthorization();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentPlayer, HttpCurrentPlayer>();
        return services;
    }
}
