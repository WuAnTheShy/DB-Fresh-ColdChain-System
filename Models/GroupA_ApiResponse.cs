namespace FreshColdChain.Models;

// 统一 API 返回格式
public class ApiResponse<T>
{
    public int Code { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public bool IsSuccess => Code == 200;

    public static ApiResponse<T> Success(T data, string message = "操作成功")
        => new() { Code = 200, Message = message, Data = data };

    public static ApiResponse<T> Fail(string message, int code = 400)
        => new() { Code = code, Message = message };
}

public class ApiResponse : ApiResponse<object>
{
    public static ApiResponse Success(string message = "操作成功")
        => new() { Code = 200, Message = message };

    public new static ApiResponse Fail(string message, int code = 400)
        => new() { Code = code, Message = message };
}
