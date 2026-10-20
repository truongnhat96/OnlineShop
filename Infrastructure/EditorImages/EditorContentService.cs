using System.Text;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using Ganss.Xss;

namespace Infrastructure.EditorImages;

public sealed class EditorContentService(EditorImagePolicy policy)
{
    public const int MaxHtmlBytes = 2621440;
    public const string ProductPrefix = "<!--motoshop-richtext:v1-->\n";
    public static bool IsRichProduct(string? content) => content?.StartsWith(ProductPrefix, StringComparison.Ordinal) == true;
    public static string ProductEditorHtml(string? content) => IsRichProduct(content)
        ? content![ProductPrefix.Length..]
        : string.Join("<br>", (content ?? "").Replace("\r\n", "\n").Replace("\r", "\n").Split('\n').Select(line => HtmlEncoder.Default.Encode(line)));

    public string PrepareForSave(string? content, bool product = false)
    {
        var html = content ?? "";
        if (product && IsRichProduct(html)) html = html[ProductPrefix.Length..];
        if (Encoding.UTF8.GetByteCount(html) > MaxHtmlBytes)
            throw new EditorImageException("content_too_large", "Nội dung vượt quá 2,5 MiB. Hãy hoàn tất upload ảnh và giảm kích thước nội dung.", 413);
        var document = new HtmlParser().ParseDocument(html);
        if (document.QuerySelector("svg, object, embed, iframe, video, audio") is not null)
            throw new EditorImageException("unsupported_media", "Nội dung chứa media không được hỗ trợ. Vui lòng sử dụng nút chèn ảnh.");
        foreach (var element in document.All)
        {
            var css = element.GetAttribute("style") ?? "";
            if (element.LocalName == "style") css += element.TextContent;
            css = Regex.Replace(css, @"\\([0-9a-fA-F]{1,6})\s?|\\(.)", match => match.Groups[1].Success
                ? DecodeCssCodePoint(match.Groups[1].Value) : match.Groups[2].Value);
            css = Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);
            // Computed styles copied from websites include harmless image resets. Ignore only
            // complete reset values for detection; the sanitizer removes these CSS properties.
            css = Regex.Replace(css, @"(^|[;{])\s*(?:background-image|border-image|list-style-image|mask-image)\s*:\s*(?:none|initial|inherit|unset|revert(?:-layer)?)\s*(?:!important\s*)?(?=;|}|$)", "$1", RegexOptions.IgnoreCase);
            if (Regex.IsMatch(css, @"(?:url|image-set|image|cross-fade|element)\s*\(|(?:background-image|border-image|list-style-image|mask-image)\s*:", RegexOptions.IgnoreCase))
                throw new EditorImageException("unsupported_image_style", "Ảnh nền CSS chưa được hỗ trợ. Vui lòng chèn ảnh bằng nút hình ảnh.");
        }
        foreach (var picture in document.QuerySelectorAll("picture"))
            if (picture.QuerySelector("img[src]") is null)
                throw new EditorImageException("unsupported_picture", "Ảnh responsive thiếu ảnh đại diện. Vui lòng chèn lại ảnh.");
        foreach (var image in document.QuerySelectorAll("img"))
        {
            if (!policy.IsOwnedUrl(image.GetAttribute("src")))
                throw new EditorImageException("unresolved_image", "Ảnh chưa được upload thành công lên Cloudinary. Vui lòng thử lại hoặc xóa ảnh lỗi.");
            image.RemoveAttribute("srcset");
            image.RemoveAttribute("sizes");
        }
        foreach (var source in document.QuerySelectorAll("source"))
        {
            if (source.ParentElement?.LocalName != "picture")
                throw new EditorImageException("unsupported_picture", "Nguồn ảnh chưa được hỗ trợ. Vui lòng chèn lại ảnh.");
            source.Remove();
        }
        foreach (var picture in document.QuerySelectorAll("picture")) picture.OuterHtml = picture.InnerHtml;
        var clean = CreateSanitizer().Sanitize(document.Body?.InnerHtml ?? "");
        foreach (var image in new HtmlParser().ParseDocument(clean).QuerySelectorAll("img"))
            if (!policy.IsOwnedUrl(image.GetAttribute("src")))
                throw new EditorImageException("unresolved_image", "Nguồn ảnh không hợp lệ.");
        if (Encoding.UTF8.GetByteCount(clean) > MaxHtmlBytes)
            throw new EditorImageException("content_too_large", "Nội dung vượt quá 2,5 MiB.", 413);
        return product ? ProductPrefix + clean : clean;
    }

    public string ProductDisplayHtml(string content) => CreateSanitizer().Sanitize(ProductEditorHtml(content));

    private static string DecodeCssCodePoint(string value)
    {
        var number = Convert.ToInt32(value, 16);
        return number is <= 0 or > 0x10ffff or (>= 0xd800 and <= 0xdfff) ? "\ufffd" : char.ConvertFromUtf32(number);
    }

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        foreach (var tag in "p br div span h1 h2 h3 h4 h5 h6 strong b em i u s strike sup sub ul ol li blockquote pre code a img table thead tbody tfoot tr th td hr".Split(' ')) sanitizer.AllowedTags.Add(tag);
        sanitizer.AllowedAttributes.Clear();
        foreach (var attribute in "href src alt title width height style target rel colspan rowspan".Split(' ')) sanitizer.AllowedAttributes.Add(attribute);
        sanitizer.AllowedCssProperties.Clear();
        foreach (var property in "color background-color font-weight font-style font-size font-family text-align text-decoration float width height max-width margin margin-left margin-right".Split(' ')) sanitizer.AllowedCssProperties.Add(property);
        sanitizer.AllowedAtRules.Clear();
        sanitizer.AllowedSchemes.Clear();
        foreach (var scheme in new[] { "http", "https", "mailto" }) sanitizer.AllowedSchemes.Add(scheme);
        return sanitizer;
    }
}
