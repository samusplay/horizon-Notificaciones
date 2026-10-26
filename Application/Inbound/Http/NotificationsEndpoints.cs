using System.Security.Claims;
using Horizon.Notifications.Application.UseCases;

namespace Horizon.Notifications.Adapters.Inbound.Http;

// Adaptador de ENTRADA: el frontend pide la campanita por HTTP
internal static class NotificationsEndpoints
{
    public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app)
    {
        // todo el grupo exige un JWT válido
        var group = app.MapGroup("/api/notifications").RequireAuthorization();

        // GET /api/notifications?limit=20
        group.MapGet("", async (ClaimsPrincipal user, int? limit, GetMyNotifications useCase, CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            if (userId is null) return Results.Unauthorized();

            return Results.Ok(await useCase.ExecuteAsync(userId.Value, limit, ct));
        });

        // POST /api/notifications/read-all
        group.MapPost("/read-all", async (ClaimsPrincipal user, MarkAllAsRead useCase, CancellationToken ct) =>
        {
            var userId = GetUserId(user);
            if (userId is null) return Results.Unauthorized();

            return Results.Ok(await useCase.ExecuteAsync(userId.Value, ct));
        });

        return app;
    }

    // el usuario SIEMPRE sale del token (claim "sub"), nunca de la URL
    private static Guid? GetUserId(ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue("sub"), out var id) ? id : null;
}