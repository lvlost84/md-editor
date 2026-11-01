// OWNER: implementer A. Markdown -> HTML converter (no deps). STUB: replace body, keep signatures.
using System;
static class Md
{
    // body fragment (footnotes included), HTML-escaped/safe, marked-GFM-like, breaks:true
    public static string ToHtml(string md) { throw new NotImplementedException(); }
    // full page for WPF WebBrowser (IE11 engine: add X-UA-Compatible edge, no modern CSS vars/flex reliance)
    public static string PreviewDocument(string md, bool dark) { throw new NotImplementedException(); }
    // standalone export page (title HTML-escaped)
    public static string ExportDocument(string md, string title) { throw new NotImplementedException(); }
}
