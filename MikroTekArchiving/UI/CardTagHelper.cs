using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace MikroTek.Archiving.UI;

[HtmlTargetElement("ui-card")]
public class CardTagHelper : TagHelper
{
    public string? Title { get; set; }
    public string? Subtitle { get; set; }
    public string? Icon { get; set; }
    public bool Collapsible { get; set; }
    public bool Closable { get; set; }
    public bool Flush { get; set; }
    public string? Class { get; set; }
    public string? BodyClass { get; set; }
    public string? Id { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {        
        var holder = new CardToolsTagHelper.Holder();
        context.Items[typeof(CardToolsTagHelper)] = holder;
        var body = (await output.GetChildContentAsync()).GetContent();
        var tools = holder.Html;
        var id = Id ?? "card" + Guid.NewGuid().ToString("N")[..6];
        var enc = HtmlEncoder.Default;

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", $"card ui-card {Class}".Trim());
        output.Attributes.SetAttribute("id", id);

        var header = "";
        if (Title is not null || tools.Length > 0 || Collapsible || Closable)
        {
            var icon = Icon is null ? "" : $"<i class=\"bi {enc.Encode(Icon)} ui-card-icon\"></i>";
            var sub = Subtitle is null ? "" : $"<div class=\"ui-card-sub\">{enc.Encode(Subtitle)}</div>";
            var btns = "";
            if (Collapsible) btns += $"<button type=\"button\" class=\"ui-card-btn\" data-bs-toggle=\"collapse\" data-bs-target=\"#{id}-body\" aria-expanded=\"true\" title=\"Collapse\"><i class=\"bi bi-chevron-up\"></i></button>";
            if (Closable) btns += "<button type=\"button\" class=\"ui-card-btn\" data-ui-close-card title=\"Close\"><i class=\"bi bi-x-lg\"></i></button>";
            header = $"<div class=\"card-header ui-card-header\">{icon}<div class=\"ui-card-titles\"><div class=\"ui-card-title\">{enc.Encode(Title ?? "")}</div>{sub}</div><div class=\"ui-card-tools\">{tools}{btns}</div></div>";
        }
        var collapse = Collapsible ? $" collapse show\" id=\"{id}-body" : "";
        output.Content.SetHtmlContent($"{header}<div class=\"ui-card-bodywrap{collapse}\"><div class=\"{(Flush ? "ui-card-flush" : "card-body")} {BodyClass}\">{body}</div></div>");
    }
}

[HtmlTargetElement("ui-card-tools", ParentTag = "ui-card")]
public class CardToolsTagHelper : TagHelper
{
    public class Holder { public string Html { get; set; } = ""; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (context.Items.TryGetValue(typeof(CardToolsTagHelper), out var h) && h is Holder holder)
            holder.Html = (await output.GetChildContentAsync()).GetContent();
        output.SuppressOutput();
    }
}
