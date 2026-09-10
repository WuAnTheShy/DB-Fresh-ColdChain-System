using FreshColdChain.Models.DTOs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FreshColdChain.Services;

// 团长带货「图文介绍」的文件存取与图片上传服务。
// 图文介绍以 JSON 文件存放在 <c>wwwroot/uploads/promoter-desc/</c>，数据库仅存相对路径；
// 团长自行上传的介绍插图存放在 <c>wwwroot/uploads/promoter-img/</c>。
public sealed class PromoterIntroStore
{
    public const string DescFolder = "uploads/promoter-desc";
    public const string ImgFolder = "uploads/promoter-img";
    public const string DescUrlPrefix = "/uploads/promoter-desc/";

    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    private static readonly long MaxImageBytes = 5 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly string _webRoot;

    public PromoterIntroStore(IWebHostEnvironment env)
    {
        _webRoot = Path.GetFullPath(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"));
    }

    // 判断某存储值是否为“图文内容文件相对路径”。
    public static bool LooksLikeIntroPath(string value) =>
        value.StartsWith(DescUrlPrefix, StringComparison.OrdinalIgnoreCase) &&
        value.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
        !value.Contains("..", StringComparison.Ordinal) &&
        !value.Contains('\\');

    // 根据（团长、商品、供应商）生成图文内容文件的相对路径（无扩展外拼接风险）。
    public string BuildRelativePath(string promoterId, string productId, string supplierId) =>
        $"{DescUrlPrefix}desc_{Sanitize(promoterId)}_{Sanitize(productId)}_{Sanitize(supplierId)}.json";

    private static string Sanitize(string? id)
    {
        var s = string.IsNullOrWhiteSpace(id) ? "unknown" : id.Trim();
        return Regex.Replace(s, "[^0-9A-Za-z._-]", "_");
    }

    // 解析团长端提交的介绍 JSON。返回 (null,null) 表示“无内容（清空）”，(null,error) 表示格式错误。
    public static (PromoterRichContent? Content, string? Error) TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return (null, null);
        try
        {
            var content = JsonSerializer.Deserialize<PromoterRichContent>(json, JsonOptions);
            if (content == null) return (null, null);
            content.Normalize();
            return (content, null);
        }
        catch (JsonException)
        {
            return (null, "介绍内容格式不正确，请重新保存。");
        }
    }

    // 读取“存储值”（PROMOTERDESC）为图文内容：
    // 空 → null；相对路径 → 读取 JSON 文件；其它字符串（历史纯文字介绍）→ 自动包装为单段文字。
    // 任何读取异常均返回 null，避免影响页面。
    public async Task<PromoterRichContent?> LoadAsync(string? stored)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(stored)) return null;
            var value = stored.Trim();
            if (!LooksLikeIntroPath(value))
            {
                var legacy = new PromoterRichContent
                {
                    Sections = { new PromoterIntroSection { Text = value } }
                };
                legacy.Normalize();
                return legacy;
            }

            var file = ResolveFile(value);
            if (file == null || !File.Exists(file)) return null;
            var json = await File.ReadAllTextAsync(file, Encoding.UTF8);
            var content = JsonSerializer.Deserialize<PromoterRichContent>(json, JsonOptions);
            content?.Normalize();
            return content;
        }
        catch
        {
            return null;
        }
    }

    // 保存图文内容到相对路径文件并返回相对路径；内容为空（或 null）时删除已有文件并返回 null（表示清除介绍）。
    public async Task<string?> SaveAsync(string promoterId, string productId, string supplierId, PromoterRichContent? content)
    {
        content?.Normalize();
        var rel = BuildRelativePath(promoterId, productId, supplierId);
        var full = ResolveFile(rel)
                   ?? throw new InvalidOperationException("无效的介绍内容路径。");

        if (content == null || content.IsEmpty())
        {
            if (File.Exists(full)) File.Delete(full);
            return null;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllTextAsync(full, JsonSerializer.Serialize(content, JsonOptions), new UTF8Encoding(false));
        return rel;
    }

    // 取图文内容的纯文本摘要（供跨组消费者端“简介”字段使用；路径指向内容文件时读取后再拼接）。
    public async Task<string?> ToPlainTextAsync(string? stored, int maxLength = 2000)
    {
        var content = await LoadAsync(stored);
        if (content == null) return null;
        var text = content.ToPlainText();
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim();
        if (maxLength > 0 && text.Length > maxLength) text = text[..maxLength];
        return text;
    }

    // 保存团长上传的介绍插图，返回可访问的相对路径。文件名为随机生成，杜绝覆盖/穿越。
    public async Task<(bool Ok, string? Url, string? Message)> UploadImageAsync(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return (false, null, "请选择要上传的图片。");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return (false, null, "仅支持 JPG/PNG/GIF/WebP 格式的图片。");
        if (file.Length > MaxImageBytes)
            return (false, null, "图片大小不能超过 5MB。");

        var dir = Path.Combine(_webRoot, "uploads", "promoter-img");
        Directory.CreateDirectory(dir);

        var name = $"img_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid():N}{ext}";
        var full = Path.Combine(dir, name);
        await using (var stream = new FileStream(full, FileMode.CreateNew))
        {
            await file.CopyToAsync(stream);
        }

        return (true, $"/uploads/promoter-img/{name}", null);
    }

    // 将内容文件相对路径解析为 wwwroot 下的物理路径；防止越出 wwwroot 的路径穿越返回 null。
    private string? ResolveFile(string rel)
    {
        var relative = rel.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        var full = Path.GetFullPath(Path.Combine(_webRoot, relative));
        var rootPrefix = _webRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return full.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase) ? full : null;
    }
}
