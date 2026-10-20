using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Infrastructure.EditorImages;

public sealed class EditorImageApiFilter : IExceptionFilter, IAlwaysRunResultFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not (BadHttpRequestException or InvalidDataException)) return;
        var status = context.Exception is BadHttpRequestException http ? http.StatusCode : 400;
        context.Result = Error(status);
        context.ExceptionHandled = true;
    }
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Result is StatusCodeResult result && result.StatusCode >= 400) context.Result = Error(result.StatusCode);
    }
    public void OnResultExecuted(ResultExecutedContext context) { }
    private static ObjectResult Error(int status) => new(new
    {
        code = status == 413 ? "image_too_large" : "invalid_request",
        message = status == 413 ? "Yêu cầu upload ảnh vượt quá dung lượng cho phép." : "Dữ liệu upload ảnh không hợp lệ."
    }) { StatusCode = status };
}
