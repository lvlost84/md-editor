using System;

// 도움말 창 본문 검사 (창은 만들지 않고 HelpWindow.BuildHtml만 검사)
static partial class SelfTest
{
    static void HelpTests()
    {
        string light = HelpWindow.BuildHtml(false), dark = HelpWindow.BuildHtml(true);
        string[] ids = { "h", "em", "p", "ul", "ol", "task", "bq", "code", "link", "img", "table", "hr", "fn", "esc", "html", "keys" };
        foreach (var id in ids)
        {
            Check("help section t-" + id, light.Contains("id=\"t-" + id + "\""), null);
            Check("help toc link t-" + id, light.Contains("href=\"about:blank#t-" + id + "\""), null);
        }
        Check("help renders example table", light.Contains("<th") && light.Contains("사과"), null);
        Check("help renders bold example", light.Contains("<strong>굵게</strong>"), null);
        Check("help renders footnote example", light.Contains("class=\"footnotes\""), null);
        Check("help source shown escaped", light.Contains("&lt;b&gt;굵게&lt;/b&gt;"), null);
        Check("help image example not rendered", !light.Contains("<img"), null);
        Check("help dark differs from light", light != dark && dark.Contains("#1e2024"), null);
        Check("help no script", !light.ToLowerInvariant().Contains("<script"), null);
    }
}
