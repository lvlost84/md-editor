// Markdown -> HTML converter on Markdig 0.37 (lib\Markdig.dll). Target = IE11 WebBrowser; output normalised to the old hand-written converter's HTML.
// Intentional: raw HTML is escaped (DisableHtml; no tag passes). Never throws. Links/images/autolinks: only http/https/mailto/file/relative/#/drive-letter urls (AST walk).
// Footnotes: own semantics (not Markdig's) - pre-pass swaps [^label] refs for private-use markers, AST walk numbers them by first reference.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

static class Md
{
    const char MkOpen = '', MkClose = '';
    const int StackBytes = 64 * 1024 * 1024; // deep nesting (e.g. 5000 '>') recurses in Markdig's renderer
    const int TimeoutMs = 4000;

    static readonly Regex FnDefRx = new Regex(@"^\[\^([^\]]+)\]:[ \t]?(.*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    static readonly Regex FnRefRx = new Regex(@"\[\^([^\[\]\n]{1,200})\]", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    static readonly Regex MarkRx = new Regex("([0-9]+)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    static readonly Regex CodeRx = new Regex(@"<code[^>]*>.*?</code>", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    sealed class Fn
    {
        public List<string> Labels = new List<string>();                         // marker idx -> label
        public Dictionary<string, int> LabelIdx = new Dictionary<string, int>();
        public Dictionary<string, string> Defs = new Dictionary<string, string>();
        public List<string> Order = new List<string>();                          // labels by first reference
        public Dictionary<string, int> Num = new Dictionary<string, int>();
    }

    // ---------------------------------------------------------------- public API
    static string lastTimedOut;   // 마지막으로 시간 초과된 입력: 같은 글을 다시 변환하느라 4초씩 또 쓰지 않는다

    public static string ToHtml(string md)
    {
        md = md ?? "";
        string bad = lastTimedOut;
        if (bad != null && bad.Length == md.Length && string.Equals(bad, md, StringComparison.Ordinal)) return "<pre>" + Esc(md) + "</pre>";
        try
        {
            string res = null;
            var t = new Thread(() => { try { res = Convert(md); } catch (Exception) { res = null; } }, StackBytes);
            t.IsBackground = true;
            t.Start();
            if (!t.Join(TimeoutMs)) { lastTimedOut = md; try { t.Abort(); } catch (Exception) { } } // pathological input (Markdig has a few quadratic paths): give up -> escaped source
            else if (res != null) return res;
        }
        catch (Exception) { }
        return "<pre>" + Esc(md) + "</pre>";
    }

    public static string PreviewDocument(string md, bool dark)
    {
        string bg, ink, soft, acc, accSoft, bord, amber;
        if (dark) { bg = "#1e2024"; ink = "#e6e8ec"; soft = "#9aa1ad"; acc = "#7aa7ff"; accSoft = "#26324f"; bord = "#3a3e46"; amber = "#e0a458"; }
        else { bg = "#fff"; ink = "#1c1d20"; soft = "#6b6f76"; acc = "#2563eb"; accSoft = "#e8effd"; bord = "#d3d3cf"; amber = "#b45309"; }
        var css = new StringBuilder();
        css.Append("body{margin:0;background:" + bg + ";color:" + ink + ";font-family:'Malgun Gothic','Segoe UI',sans-serif;font-size:15px;line-height:1.75;padding:22px 30px;}");
        css.Append("h1,h2,h3{font-weight:700;letter-spacing:-.01em;margin-top:1.3em;}");
        css.Append("h1{font-size:1.7em;border-bottom:2px solid " + acc + ";padding-bottom:.25em;}");
        css.Append("h2{font-size:1.35em;color:" + acc + ";}");
        css.Append("code{font-family:Consolas,monospace;background:" + accSoft + ";padding:.15em .4em;border-radius:4px;font-size:.9em;}");
        css.Append("pre{background:#1a2332;color:#e8ecf1;padding:14px 16px;border-radius:6px;overflow-x:auto;}");
        css.Append("pre code{background:none;padding:0;color:inherit;}");
        css.Append("blockquote{border-left:3px solid " + amber + ";margin:1em 0;padding:.3em 1em;color:" + soft + ";background:" + accSoft + ";}");
        css.Append("table{border-collapse:collapse;width:100%;margin:1em 0;}");
        css.Append("th,td{border:1px solid " + bord + ";padding:6px 10px;font-size:.92em;}");
        css.Append("th{background:" + accSoft + ";}");
        css.Append("img{max-width:100%;}");
        css.Append("a{color:" + acc + ";}");
        css.Append("hr{border:none;border-top:1px solid " + bord + ";margin:1.5em 0;}");
        css.Append("li.task{list-style:none;}");
        css.Append(".footnotes{font-size:.88em;color:" + soft + ";}");
        css.Append(".footnotes ol{padding-left:20px;}");
        css.Append("sup a{color:" + acc + ";text-decoration:none;font-weight:600;}");
        css.Append(".fn-back{margin-left:4px;text-decoration:none;color:" + acc + ";}");
        return "<!DOCTYPE html><html><head><meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\"><meta charset=\"utf-8\"><style>"
            + css + "</style></head><body>" + ToHtml(md) + "</body></html>";
    }

    public static string ExportDocument(string md, string title)
    {
        return "<!DOCTYPE html><html lang=\"ko\"><head><meta charset=\"UTF-8\"><title>" + Esc(title ?? "") + "</title><style>"
            + "body{font-family:Georgia,serif;max-width:760px;margin:40px auto;padding:0 20px;line-height:1.7;color:#182233;} "
            + "code{background:#e7eff6;padding:.15em .4em;border-radius:4px;font-family:monospace;} "
            + "pre{background:#1a2332;color:#e8ecf1;padding:14px;border-radius:6px;overflow-x:auto;} "
            + "pre code{background:none;color:inherit;} li.task{list-style:none;} "
            + "blockquote{border-left:3px solid #b6742b;margin:1em 0;padding:.3em 1em;color:#5b6472;} "
            + "table{border-collapse:collapse;width:100%;} "
            + "th,td{border:1px solid #b9bcb2;padding:6px 10px;} "
            + ".footnotes{font-size:.88em;color:#5b6472;} "
            + "sup a{color:#2b5f8c;text-decoration:none;} "
            + "</style></head><body>" + ToHtml(md) + "</body></html>";
    }

    // ---------------------------------------------------------------- pipeline (cached; Markdig pipelines are thread-safe once built)
    static class PipeHolder
    {
        public static readonly MarkdownPipeline P = new MarkdownPipelineBuilder()
            .UsePipeTables()
            .UseTaskLists()
            .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
            .UseAutoLinks()
            .UseSoftlineBreakAsHardlineBreak()
            .DisableHtml()
            .Build();
    }

    // ---------------------------------------------------------------- driver
    [MethodImpl(MethodImplOptions.NoInlining)]
    static string Convert(string md)
    {
        md = md.Replace("\r\n", "\n").Replace('\r', '\n');
        if (md.Length > 0 && md[0] == '﻿') md = md.Substring(1);
        if (md.IndexOf(MkOpen) >= 0 || md.IndexOf(MkClose) >= 0) md = md.Replace(MkOpen, '�').Replace(MkClose, '�');
        md = GuardLongRuns(md);
        var fn = new Fn();
        string src = PrePass(md, fn);
        string html = RenderFragment(src, fn, false);
        var sb = new StringBuilder(html.Length + 256);
        sb.Append(html);
        if (fn.Order.Count > 0)
        {
            sb.Append("<hr><div class=\"footnotes\"><ol>");
            for (int k = 0; k < fn.Order.Count; k++) // Order may grow while rendering a def that references another footnote
            {
                string label = fn.Order[k];
                string def;
                if (!fn.Defs.TryGetValue(label, out def)) def = "";
                string inner = def.Trim().Length == 0 ? "<em>(내용 없음)</em>" : RenderFragment(MarkRefs(def, fn), fn, true);
                string a = Esc(label);
                sb.Append("<li id=\"fn-" + a + "\">" + inner + " <a href=\"#fnref-" + a + "\" class=\"fn-back\">↩</a></li>");
            }
            sb.Append("</ol></div>");
        }
        string all = sb.ToString();
        if (all.IndexOf(MkOpen) >= 0) all = RestoreMarkers(all, fn);
        return all;
    }

    // Markdig scans a link destination to the end of a whitespace-free run for every "](" -> quadratic on e.g. "[a](" x100000.
    // In a long whitespace-free run with many "](" they are escaped to "]\(" (no link there).
    static string GuardLongRuns(string md)
    {
        if (md.Length < 2000 || md.IndexOf("](", StringComparison.Ordinal) < 0) return md;
        StringBuilder sb = null;
        int n = md.Length, i = 0, copied = 0;
        while (i < n)
        {
            if (char.IsWhiteSpace(md[i])) { i++; continue; }
            int s = i;
            while (i < n && !char.IsWhiteSpace(md[i])) i++;
            if (i - s < 1000) continue;
            int cnt = 0, p = s;
            while ((p = md.IndexOf("](", p, i - p, StringComparison.Ordinal)) >= 0) { cnt++; p += 2; }
            if (cnt <= 20) continue;
            if (sb == null) sb = new StringBuilder(n + cnt);
            p = s;
            int q;
            while ((q = md.IndexOf("](", p, i - p, StringComparison.Ordinal)) >= 0)
            {
                sb.Append(md, copied, q + 1 - copied).Append('\\');
                copied = q + 1; p = q + 2;
            }
        }
        if (sb == null) return md;
        sb.Append(md, copied, n - copied);
        return sb.ToString();
    }

    // line pre-pass: footnote defs out (not inside fences), [^label] refs -> markers
    static string PrePass(string md, Fn fn)
    {
        if (md.IndexOf("[^", StringComparison.Ordinal) < 0) return md;
        string[] lines = md.Split('\n');
        bool inFence = false; char fch = '`'; int flen = 0;
        for (int k = 0; k < lines.Length; k++)
        {
            string l = lines[k];
            string t = l.TrimStart(' ');
            if (t.Length >= 3 && (t[0] == '`' || t[0] == '~'))
            {
                int r = 0; while (r < t.Length && t[r] == t[0]) r++;
                if (r >= 3)
                {
                    if (!inFence) { inFence = true; fch = t[0]; flen = r; }
                    else if (t[0] == fch && r >= flen && t.Substring(r).Trim().Length == 0) inFence = false;
                    continue;
                }
            }
            if (inFence) continue;
            Match m = FnDefRx.Match(l);
            if (m.Success) { fn.Defs[m.Groups[1].Value] = m.Groups[2].Value; lines[k] = ""; continue; }
            if (l.IndexOf("[^", StringComparison.Ordinal) >= 0) lines[k] = MarkRefs(l, fn);
        }
        return string.Join("\n", lines);
    }

    static string MarkRefs(string l, Fn fn)
    {
        return FnRefRx.Replace(l, mm =>
        {
            string label = mm.Groups[1].Value;
            int idx;
            if (!fn.LabelIdx.TryGetValue(label, out idx)) { idx = fn.Labels.Count; fn.Labels.Add(label); fn.LabelIdx[label] = idx; }
            return MkOpen + idx.ToString() + MkClose;
        });
    }

    // parse + sanitize + render one markdown fragment; stripP: unwrap a single <p>..</p> (footnote defs)
    static string RenderFragment(string src, Fn fn, bool stripP)
    {
        MarkdownDocument doc = Markdown.Parse(src, PipeHolder.P);
        Walk(doc, fn);
        string html = Markdown.ToHtml(doc, PipeHolder.P);
        html = html.Replace("<br />", "<br>").Replace("<hr />", "<hr>").Replace("\n</code></pre>", "</code></pre>")
            .Replace("<input disabled=\"disabled\" type=\"checkbox\" checked=\"checked\" />", "<input type=\"checkbox\" disabled checked>")
            .Replace("<input disabled=\"disabled\" type=\"checkbox\" />", "<input type=\"checkbox\" disabled>")
            .Replace("<li class=\"task-list-item\">", "<li class=\"task\">")
            .Replace("<ul class=\"contains-task-list\">", "<ul>").Replace("<ol class=\"contains-task-list\">", "<ol>");
        html = FixAlign(html);
        if (stripP)
        {
            html = html.Trim();
            if (html.StartsWith("<p>", StringComparison.Ordinal) && html.EndsWith("</p>", StringComparison.Ordinal) && html.IndexOf("<p>", 3, StringComparison.Ordinal) < 0)
                html = html.Substring(3, html.Length - 7);
        }
        return html;
    }

    // "text-align: left;" (Markdig) -> "text-align:left" ; only the three fixed values survive
    static string FixAlign(string html)
    {
        if (html.IndexOf("style=\"", StringComparison.Ordinal) < 0) return html;
        return html.Replace("style=\"text-align: left;\"", "style=\"text-align:left\"")
                   .Replace("style=\"text-align: center;\"", "style=\"text-align:center\"")
                   .Replace("style=\"text-align: right;\"", "style=\"text-align:right\"");
    }

    // markers left over (inside code / urls / failed parses) -> literal "[^label]" text; code regions first
    static string RestoreMarkers(string html, Fn fn)
    {
        html = CodeRx.Replace(html, m => m.Value.IndexOf(MkOpen) >= 0 ? MarkRx.Replace(m.Value, mm => MarkText(mm, fn)) : m.Value);
        return MarkRx.Replace(html, mm => MarkText(mm, fn)).Replace(MkOpen.ToString(), "").Replace(MkClose.ToString(), "");
    }
    static string MarkText(Match mm, Fn fn)
    {
        int idx;
        if (int.TryParse(mm.Groups[1].Value, out idx) && idx >= 0 && idx < fn.Labels.Count) return "[^" + Esc(fn.Labels[idx]) + "]";
        return "";
    }

    // ---------------------------------------------------------------- AST walk: link allow-list + footnote refs
    static void Walk(MarkdownDocument doc, Fn fn)
    {
        var all = new List<Inline>();
        foreach (Inline x in Markdig.Syntax.MarkdownObjectExtensions.Descendants<Inline>(doc)) all.Add(x);
        // pass 1: urls
        foreach (Inline x in all)
        {
            var li = x as LinkInline;
            if (li != null)
            {
                if (SafeUrl(li.Url) != null) continue;
                if (li.IsImage) { li.ReplaceBy(new LiteralInline(PlainText(li)), false); }
                else if (li.IsAutoLink) { li.ReplaceBy(new LiteralInline(li.Url ?? ""), false); }
                else
                {
                    Inline ch = li.FirstChild;
                    while (ch != null) { Inline nx = ch.NextSibling; ch.Remove(); li.InsertBefore(ch); ch = nx; }
                    li.Remove();
                }
                continue;
            }
            var au = x as AutolinkInline;
            if (au != null && SafeUrl(au.Url) == null) au.ReplaceBy(new LiteralInline(au.Url ?? ""));
        }
        // pass 2: footnote markers in text (document order -> numbering)
        foreach (Inline x in all)
        {
            var lit = x as LiteralInline;
            if (lit == null) continue;
            string text = lit.Content.ToString();
            if (text.IndexOf(MkOpen) < 0) continue;
            int pos = 0;
            Inline last = lit;
            string first = null;
            var m = MarkRx.Match(text);
            while (m.Success)
            {
                int idx = int.Parse(m.Groups[1].Value);
                string before = text.Substring(pos, m.Index - pos);
                if (first == null) first = before;
                else if (before.Length > 0) { var b = new LiteralInline(before); last.InsertAfter(b); last = b; }
                if (idx >= 0 && idx < fn.Labels.Count)
                {
                    string label = fn.Labels[idx];
                    int num;
                    if (!fn.Num.TryGetValue(label, out num)) { fn.Order.Add(label); num = fn.Order.Count; fn.Num[label] = num; }
                    string a = Esc(label);
                    var h = new HtmlInline("<sup id=\"fnref-" + a + "\"><a href=\"#fn-" + a + "\">[" + num + "]</a></sup>");
                    last.InsertAfter(h); last = h;
                }
                pos = m.Index + m.Length;
                m = m.NextMatch();
            }
            string rest = text.Substring(pos);
            if (first == null) continue;
            lit.Content = new Markdig.Helpers.StringSlice(first);
            if (rest.Length > 0) last.InsertAfter(new LiteralInline(rest));
        }
    }

    static string PlainText(ContainerInline c)
    {
        var sb = new StringBuilder();
        foreach (Inline x in Markdig.Syntax.MarkdownObjectExtensions.Descendants<Inline>(c))
        {
            var l = x as LiteralInline; if (l != null) sb.Append(l.Content.ToString());
            var cd = x as CodeInline; if (cd != null) sb.Append(cd.Content);
        }
        return sb.ToString();
    }

    // returns null if the url is not allowed
    static string SafeUrl(string u)
    {
        if (u == null) return null;
        if (u.Length == 0) return u;
        var chk = new StringBuilder(u.Length);
        foreach (char ch in u) if (ch > ' ' && ch != '\u007f' && !char.IsControl(ch) && ch != '­' && ch != '​' && ch != '﻿') chk.Append(ch);
        string t = chk.ToString();
        int colon = t.IndexOf(':');
        if (colon >= 0)
        {
            int stop = t.IndexOfAny(new[] { '/', '?', '#' });
            if (stop < 0 || colon < stop)
            {
                string sch = t.Substring(0, colon).ToLowerInvariant();
                bool drive = sch.Length == 1 && sch[0] >= 'a' && sch[0] <= 'z';
                if (!drive && sch != "http" && sch != "https" && sch != "mailto" && sch != "file") return null;
            }
        }
        return u;
    }

    // ---------------------------------------------------------------- escaping
    static string Esc(string s)
    {
        if (s == null) return "";
        if (s.IndexOfAny(new[] { '&', '<', '>', '"' }) < 0) return s;
        var sb = new StringBuilder(s.Length + 16);
        foreach (char ch in s)
        {
            switch (ch)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '"': sb.Append("&quot;"); break;
                default: sb.Append(ch); break;
            }
        }
        return sb.ToString();
    }
}
