namespace FreshColdChain.Models.DTOs;

// 团长带货「图文介绍」内容模型。
// 说明：数据库字段 CRM_PRODUCT_ENTRIES.PROMOTERDESC 不再直接保存介绍文字，
// 而是保存该图文介绍内容文件的<b>相对路径</b>（如 /uploads/promoter-desc/desc_xx.json），
// 实际图文内容以 JSON 文件存放于站点 wwwroot/uploads/promoter-desc/ 下。
// 兼容约定：若 PROMOTERDESC 为不以路径约定的其他字符串，视为历史纯文字介绍（读取时自动包装为单段文字）。
// 结构：title（可选标题）+ sections（段落数组）；每一段可只有文字、只有图片，或图文并存。
public class PromoterRichContent
{
    // 介绍标题（可空）
    public string? Title { get; set; }

    // 介绍段落（顺序即展示顺序）
    public List<PromoterIntroSection> Sections { get; set; } = new();

    // 是否无任何内容（标题为空且无段落）
    public bool IsEmpty()
    {
        if (!string.IsNullOrWhiteSpace(Title)) return false;
        return Sections == null || Sections.Count == 0;
    }

    // 规范化：清理标题与段落中的空值，删除无效图片地址与完全空的段落。
    public void Normalize()
    {
        Title = string.IsNullOrWhiteSpace(Title) ? null : Title.Trim();
        if (Title is { Length: > 200 }) Title = Title[..200];
        Sections ??= new List<PromoterIntroSection>();
        foreach (var section in Sections)
        {
            section?.Normalize();
        }
        Sections.RemoveAll(s => s == null || s.IsEmpty());
    }

    // 取纯文本形态（标题 + 各段文字，段与段换行分隔），供跨组消费者端简介使用。
    public string ToPlainText()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(Title)) parts.Add(Title.Trim());
        if (Sections != null)
        {
            foreach (var section in Sections)
            {
                if (!string.IsNullOrWhiteSpace(section.Text)) parts.Add(section.Text.Trim());
            }
        }
        return string.Join(Environment.NewLine, parts);
    }
}

// 图文介绍的一个段落：一段文字 + 若干张图片；文字、图片可任意为空（纯图片段 / 纯文字段）。
public class PromoterIntroSection
{
    // 段落文字（可空，空则表示本段为“纯图片”段）
    public string? Text { get; set; }

    // 段落图片（相对站点路径或 http(s) 地址，可空则表示本段为“纯文字”段）
    public List<string> Images { get; set; } = new();

    public bool IsEmpty()
    {
        if (!string.IsNullOrWhiteSpace(Text)) return false;
        return Images == null || Images.Count == 0;
    }

    // 规范化：清理文字、过滤非法图片地址并去重、控制图片数量上限。
    public void Normalize()
    {
        Text = string.IsNullOrWhiteSpace(Text) ? null : Text.Trim();
        if (Text is { Length: > 5000 }) Text = Text[..5000];

        Images ??= new List<string>();
        Images = Images
            .Where(u => IsSafeImageUrl(u))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();
    }

    // 图片地址白名单：相对路径必须以 / 开头（禁止协议相对、../、反斜杠、盘符冒号），或 http/https 链接。
    public static bool IsSafeImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        var u = url.Trim();
        if (u.Length > 500) return false;
        if (u.StartsWith("//", StringComparison.Ordinal)) return false;

        if (u.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return Uri.TryCreate(u, UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        return u.StartsWith('/') &&
               !u.Contains("..", StringComparison.Ordinal) &&
               !u.Contains('\\') &&
               !u.Contains(':');
    }
}

// 消费者端「团长推文」查询结果：团长对某商品的图文介绍（标题 + 顺序段落）。
// 段落图片为 /uploads/promoter-img/... 相对地址，消费者端可直接用于 <c>img src</c>。
public sealed class GroupC_PromoterIntroResult
{
    // 是否有图文内容（无标题且无段落时为 false，界面提示“团长暂未撰写推文”）
    public bool HasIntro { get; set; }

    // 推文标题（可空）
    public string? Title { get; set; }

    // 推文段落（按顺序展示；每段可只有文字、只有图片或图文并存）
    public List<PromoterIntroSection> Sections { get; set; } = new();
}
