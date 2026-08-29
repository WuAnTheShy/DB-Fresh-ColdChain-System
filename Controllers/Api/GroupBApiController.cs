using FreshColdChain.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FreshColdChain.Controllers.Api;

[ServiceFilter(typeof(GroupBApiExceptionFilter))]
public abstract class GroupBApiController : ControllerBase
{
    protected string? SignedInCustomerId =>
        HttpContext.Session.GetString("CustomerId");

    protected IActionResult ApiUnauthorized(string message = "请先登录消费者账号")
    {
        return Unauthorized(new
        {
            message,
            traceId = HttpContext.TraceIdentifier
        });
    }

    protected IActionResult ApiForbidden(string message = "无权访问其他消费者的数据")
    {
        return StatusCode(StatusCodes.Status403Forbidden, new
        {
            message,
            traceId = HttpContext.TraceIdentifier
        });
    }

    protected IActionResult ApiNotFound(string message)
    {
        return NotFound(new
        {
            message,
            traceId = HttpContext.TraceIdentifier
        });
    }

    protected IActionResult ApiBadRequest(string message)
    {
        return BadRequest(new
        {
            message,
            traceId = HttpContext.TraceIdentifier
        });
    }
}

public sealed class GroupBApiExceptionFilter(
    ILogger<GroupBApiExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var isBusinessException = context.Exception is GroupBBusinessException
            or OrderBusinessException;
        var statusCode = isBusinessException
            ? StatusCodes.Status400BadRequest
            : StatusCodes.Status500InternalServerError;
        var message = isBusinessException
            ? context.Exception.Message
            : "系统暂时无法处理请求，请稍后重试";

        if (!isBusinessException)
        {
            logger.LogError(
                context.Exception,
                "GroupB API 请求处理失败，TraceId: {TraceId}",
                context.HttpContext.TraceIdentifier);
        }

        context.Result = new ObjectResult(new
        {
            message,
            traceId = context.HttpContext.TraceIdentifier
        })
        {
            StatusCode = statusCode
        };
        context.ExceptionHandled = true;
    }
}
