using ClinicQueue.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace ClinicQueue.Api.Common;

public static class ControllerResultExtensions
{
    public static ActionResult ToActionResult<T>(this ControllerBase controller, Result<T> result)
    {
        if (result.IsSuccess && result.Value is not null)
        {
            return controller.Ok(result.Value);
        }

        var message = result.Error ?? "Request failed.";

        if (message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return controller.NotFound(new { error = message });
        }

        if (message.Contains("already", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("not available", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("no extracted text", StringComparison.OrdinalIgnoreCase))
        {
            return controller.BadRequest(new { error = message });
        }

        return controller.StatusCode(StatusCodes.Status500InternalServerError, new { error = message });
    }
}
