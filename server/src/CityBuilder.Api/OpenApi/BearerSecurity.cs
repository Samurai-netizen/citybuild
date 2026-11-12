using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CityBuilder.Api.OpenApi;

/// <summary>
/// Puts the access token into the OpenAPI document (M0-61): the bearer scheme in the components, and a security requirement on every operation
/// that needs a token, the ones the <c>/api/v1</c> group's <c>RequireAuthorization</c> covers and that don't opt out with <c>AllowAnonymous</c>
/// (core-instructions/05-api-contract.md, authentication). <c>docs/reference/generated/endpoints.md</c> shows it as the Auth column.
/// </summary>
public static class BearerSecurity
{
    /// <summary>The scheme's name in the document.</summary>
    public const string SchemeName = "Bearer";

    /// <summary>Adds the document and operation transformers.</summary>
    /// <param name="options">The OpenAPI options of the API's document.</param>
    public static void AddTo(OpenApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.AddDocumentTransformer((document, _, _) =>
        {
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "The access token from the login: 'Authorization: Bearer <accessToken>'.",
            };
            return Task.CompletedTask;
        });
        options.AddOperationTransformer((operation, context, _) =>
        {
            IList<object> metadata = context.Description.ActionDescriptor.EndpointMetadata;
            if (metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any())
            {
                operation.Security ??= [];
                operation.Security.Add(new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(SchemeName, context.Document)] = [] });
            }

            return Task.CompletedTask;
        });
    }
}
