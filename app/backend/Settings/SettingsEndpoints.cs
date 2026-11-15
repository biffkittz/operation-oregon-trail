using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Oregon.Backend.Settings;

/// <summary>Settings API.</summary>
public static class SettingsEndpoints
{
    /// <summary>Maps <c>GET/PUT /api/settings</c>.</summary>
    /// <param name="api">The authenticated API route group.</param>
    /// <returns>The same route group.</returns>
    public static RouteGroupBuilder MapSettingsEndpoints(this RouteGroupBuilder api)
    {
        RouteGroupBuilder settings = api.MapGroup("/settings").WithTags("Settings");
        settings.MapGet("/", async (ISettingsService service, CancellationToken ct) => TypedResults.Ok(await service.GetAsync(ct)));
        settings.MapPut("/", UpdateAsync);
        return api;
    }

    private static async Task<Results<Ok<SettingsDto>, ValidationProblem>> UpdateAsync(
        [FromBody] SettingsDto body,
        ISettingsService service,
        CancellationToken cancellationToken)
    {
        // Enum values arriving as integers bypass the string converter's name check, so validate explicitly.
        Dictionary<string, string[]> errors = [];
        if (!Enum.IsDefined(body.DefaultTone))
        {
            errors["defaultTone"] = ["Unknown tone."];
        }

        if (!Enum.IsDefined(body.DefaultDifficulty))
        {
            errors["defaultDifficulty"] = ["Unknown difficulty."];
        }

        if (!Enum.IsDefined(body.TextSpeed))
        {
            errors["textSpeed"] = ["Unknown text speed."];
        }

        return errors.Count > 0 ? TypedResults.ValidationProblem(errors) : TypedResults.Ok(await service.UpdateAsync(body, cancellationToken));
    }
}
