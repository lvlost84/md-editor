// Markdown -> HTML converter (no deps). marked(gfm, breaks:true)+DOMPurify replacement; target = IE11 WebBrowser.
// Intentional: raw HTML is escaped (differs from marked, which passes it through) - safer for an IE-engine preview.
// Never throws; raw HTML is escaped (only attribute-less b/i/u/s/sub/sup/kbd/br/strong/em tags pass); only http/https/mailto/file/relative/# urls.
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

static class Md
{
    sealed class Ctx
    {
        public Dictionary<string, string> Defs = new Dictionary<string, string>();
        public List<string> Order = new List<string>();
        public Dictionary<string, int> Num = new Dictionary<string, int>();
        public long Budget; // scan budget per top-level Inline call (guards quadratic lookahead)
    }

    const RegexOptions RO = RegexOptions.Compiled | RegexOptions.CultureInvariant;
    static readonly Regex FenceRx = new Regex(@"^( {0,3})(`{3,}|~{3,})(.*)$", RO);
    static readonly Regex AtxRx = new Regex(@"^ {0,3}(#{1,6})(?:[ \t]+(.*))?$", RO);
    static readonly Regex HrRx = new Regex(@"^ {0,3}([-*_])(?: *\1){2,} *$", RO);
    static readonly Regex BqRx = new Regex(@"^ {0,3}>", RO);
    static readonly Regex ListRx = new Regex(@"^( *)([-*+]|[0-9]{1,9}[.)])(?:( +)(.*))?$", RO);
    static readonly Regex ListIntRx = new Regex(@"^ {0,3}(?:[-*+]|[0-9]{1,9}[.)]) +\S", RO);
    static readonly Regex FnDefRx = new Regex(@"^\[\^([^\]]+)\]:[ \t]?(.*)$", RO);
    static readonly Regex TaskRx = new Regex(@"^\[([ xX])\](?:[ \t]+|$)(.*)$", RO);
    static readonly Regex SepCellRx = new Regex(@"^:?-+:?$", RO);
    static readonly Regex SetextRx = new Regex(@"^ {0,3}(=+|-+) *$", RO);
    static readonly Regex AutoRx = new Regex(@"\G<((?:https?://|mailto:)[^\s<>]*)>", RO | RegexOptions.IgnoreCase);
    static readonly Regex TagRx = new Regex(@"\G<(/?)(b|i|u|s|sub|sup|kbd|br|strong|em)[ \t]*/?>", RO | RegexOptions.IgnoreCase);
    static readonly Regex EntRx = new Regex(@"\G&(?:#[0-9]{1,7}|#[xX][0-9a-fA-F]{1,6}|[A-Za-z][A-Za-z0-9]{1,31});", RO);

    // ---------------------------------------------------------------- public API
    public static string ToHtml(string md)
    {
        try { return Convert(md ?? ""); }
        catch (Exception) { return "<pre>" + Esc(md ?? "") + "</pre>"; }
    }

    public static string PreviewDocument(string md, bool dark)
    {
        string bg, ink, soft, acc, accSoft, bord, amber;
        if (dark) { bg = "#123a5e"; ink = "#eaf3fb"; soft = "#9fbdd6"; acc = "#7ecbe8"; accSoft = "#1d456b"; bord = "#3e6c9c"; amber = "#e0a458"; }
        else { bg = "#fff"; ink = "#182233"; soft = "#5b6472"; acc = "#2b5f8c"; accSoft = "#e7eff6"; bord = "#b9bcb2"; amber = "#b6742b"; }
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

    // ---------------------------------------------------------------- driver
    static string Convert(string md)
    {
        md = md.Replace("\r\n", "\n").Replace('\r', '\n');
        if (md.Length > 0 && md[0] == '﻿') md = md.Substring(1);
        string[] raw = md.Split('\n');
        var lines = new List<string>(raw.Length);
        var c = new Ctx();
        // pre-pass: expand leading tabs, extract footnote defs (not inside fences)
        bool inFence = false; char fch = '`'; int flen = 0;
        for (int k = 0; k < raw.Length; k++)
        {
            string l = ExpandTabs(raw[k]);
            string t = l.TrimStart(' ');
            if (t.Length >= 3 && (t[0] == '`' || t[0] == '~'))
            {
                int r = 0; while (r < t.Length && t[r] == t[0]) r++;
                if (r >= 3)
                {
                    if (!inFence) { inFence = true; fch = t[0]; flen = r; }
                    else if (t[0] == fch && r >= flen && t.Substring(r).Trim().Length == 0) inFence = false;
                    lines.Add(l); continue;
                }
            }
            if (!inFence)
            {
                Match m = FnDefRx.Match(l);
                if (m.Success) { c.Defs[m.Groups[1].Value] = m.Groups[2].Value; lines.Add(""); continue; }
            }
            lines.Add(l);
        }
        var sb = new StringBuilder(md.Length * 2 + 64);
        Blocks(lines, c, sb, false, 0);
        if (c.Order.Count > 0)
        {
            sb.Append("<hr><div class=\"footnotes\"><ol>");
            for (int k = 0; k < c.Order.Count; k++)
            {
                string label = c.Order[k];
                string def;
                if (!c.Defs.TryGetValue(label, out def)) def = "";
                string inner = def.Trim().Length == 0 ? "<em>(내용 없음)</em>" : Inline(def, c, false, 0);
                string a = Esc(label);
                sb.Append("<li id=\"fn-" + a + "\">" + inner + " <a href=\"#fnref-" + a + "\" class=\"fn-back\">↩</a></li>");
            }
            sb.Append("</ol></div>");
        }
        return sb.ToString();
    }

    static string ExpandTabs(string l)
    {
        if (l.IndexOf('\t') < 0) return l;
        var sb = new StringBuilder();
        int i = 0, col = 0;
        while (i < l.Length && (l[i] == ' ' || l[i] == '\t'))
        {
            if (l[i] == '\t') { int add = 4 - (col % 4); sb.Append(' ', add); col += add; }
            else { sb.Append(' '); col++; }
            i++;
        }
        sb.Append(l, i, l.Length - i);
        return sb.ToString();
    }

    // ---------------------------------------------------------------- escaping / urls
    static string Esc(string s)
    {
        if (s == null) return "";
        if (s.IndexOfAny(new[] { '&', '<', '>', '"' }) < 0) return s;
        var sb = new StringBuilder(s.Length + 16);
        foreach (char ch in s) AppendEsc(sb, ch);
        return sb.ToString();
    }
    static void AppendEsc(StringBuilder sb, char ch)
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

    // returns null if the url is not allowed
    static string SafeUrl(string u)
    {
        if (u == null) return null;
        u = u.Trim();
        var chk = new StringBuilder(u.Length);
        foreach (char ch in u) if (ch > ' ' && ch != '\u007f') chk.Append(ch);
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
        return u.Replace(" ", "%20");
    }

    static bool IsPunct(char ch)
    {
        return ch < 128 && ch > ' ' && !char.IsLetterOrDigit(ch) && ch != 127;
    }

    // ---------------------------------------------------------------- inline
    const int CloseLimit = 1500;

    static string Inline(string s, Ctx c, bool inLink, int depth)
    {
        if (depth > 12) return Esc(s);
        int n = s.Length, i = 0;
        if (depth == 0) c.Budget = 40L * n + 20000;
        var sb = new StringBuilder(n + 16);
        var noClose = new HashSet<int>();
        while (i < n)
        {
            char ch = s[i];
            if (ch == '\\')
            {
                if (i + 1 < n && IsPunct(s[i + 1])) { AppendEsc(sb, s[i + 1]); i += 2; continue; }
                sb.Append('\\'); i++; continue;
            }
            if (ch == '`')
            {
                int k = 0; while (i + k < n && s[i + k] == '`') k++;
                int close = -1;
                if (!noClose.Contains(k))
                {
                    int p = i + k;
                    while (p < n)
                    {
                        int q = s.IndexOf('`', p);
                        if (q < 0) break;
                        int r = 0; while (q + r < n && s[q + r] == '`') r++;
                        if (r == k) { close = q; break; }
                        p = q + r;
                    }
                    if (close < 0) noClose.Add(k);
                }
                if (close < 0) { sb.Append('`', k); i += k; continue; }
                string code = s.Substring(i + k, close - i - k).Replace('\n', ' ');
                if (code.Length > 2 && code[0] == ' ' && code[code.Length - 1] == ' ' && code.Trim().Length > 0) code = code.Substring(1, code.Length - 2);
                sb.Append("<code>").Append(Esc(code)).Append("</code>");
                i = close + k; continue;
            }
            if (ch == '*' || ch == '_' || ch == '~')
            {
                int run = 0; while (i + run < n && s[i + run] == ch) run++;
                bool done = false;
                if (ch == '~')
                {
                    if (run >= 2 && TryEmph(s, i, "~~", "del", null, c, inLink, depth, sb, ref i)) done = true;
                }
                else
                {
                    if (run >= 3 && TryEmph(s, i, new string(ch, 3), "strong><em", "em></strong", c, inLink, depth, sb, ref i)) done = true;
                    else if (run >= 2 && TryEmph(s, i, new string(ch, 2), "strong", null, c, inLink, depth, sb, ref i)) done = true;
                    else if (TryEmph(s, i, ch.ToString(), "em", null, c, inLink, depth, sb, ref i)) done = true;
                }
                if (done) continue;
                sb.Append(ch, run); i += run; continue;
            }
            if (ch == '!' && i + 1 < n && s[i + 1] == '[')
            {
                if (TryLink(s, i + 1, true, c, inLink, depth, sb, ref i)) continue;
                sb.Append('!'); i++; continue;
            }
            if (ch == '[')
            {
                if (i + 1 < n && s[i + 1] == '^')
                {
                    int e = s.IndexOf(']', i + 2);
                    if (e > i + 2 && e - i < 200)
                    {
                        string label = s.Substring(i + 2, e - i - 2);
                        if (label.IndexOf('[') < 0 && true)
                        {
                            int num;
                            if (!c.Num.TryGetValue(label, out num)) { c.Order.Add(label); num = c.Order.Count; c.Num[label] = num; }
                            string a = Esc(label);
                            sb.Append("<sup id=\"fnref-" + a + "\"><a href=\"#fn-" + a + "\">[" + num + "]</a></sup>");
                            i = e + 1; continue;
                        }
                    }
                }
                if (!inLink && TryLink(s, i, false, c, inLink, depth, sb, ref i)) continue;
                sb.Append('['); i++; continue;
            }
            if (ch == '<')
            {
                Match m = AutoRx.Match(s, i);
                if (m.Success && !inLink)
                {
                    string url = m.Groups[1].Value;
                    string safe = SafeUrl(url);
                    if (safe != null) { sb.Append("<a href=\"" + Esc(safe) + "\">" + Esc(url) + "</a>"); i += m.Length; continue; }
                }
                m = TagRx.Match(s, i);
                if (m.Success)
                {
                    string name = m.Groups[2].Value.ToLowerInvariant();
                    if (name == "br") sb.Append("<br>");
                    else sb.Append("<" + m.Groups[1].Value + name + ">");
                    i += m.Length; continue;
                }
                sb.Append("&lt;"); i++; continue;
            }
            if (ch == '&')
            {
                Match m = EntRx.Match(s, i);
                if (m.Success) { sb.Append(m.Value); i += m.Length; continue; }
                sb.Append("&amp;"); i++; continue;
            }
            if (ch == '\n') { sb.Append("<br>\n"); i++; continue; }
            if (ch == 'h' && !inLink && i + 7 < n && (i == 0 || !char.IsLetterOrDigit(s[i - 1]))
                && (string.CompareOrdinal(s, i, "http://", 0, 7) == 0 || string.CompareOrdinal(s, i, "https://", 0, 8) == 0))
            {
                int j = i; while (j < n && !char.IsWhiteSpace(s[j]) && s[j] != '<') j++;
                while (j > i)
                {
                    char t = s[j - 1];
                    if (".,;:!?'\"*_~".IndexOf(t) >= 0) j--;
                    else if (t == ')' && s.IndexOf('(', i, j - i) < 0) j--;
                    else break;
                }
                int hl = s[i + 4] == 's' ? 8 : 7;
                if (j - i > hl)
                {
                    string url = s.Substring(i, j - i);
                    sb.Append("<a href=\"" + Esc(url) + "\">" + Esc(url) + "</a>");
                    i = j; continue;
                }
            }
            if (ch == '>') { sb.Append("&gt;"); i++; continue; }
            if (ch == '"') { sb.Append("&quot;"); i++; continue; }
            sb.Append(ch); i++;
        }
        return sb.ToString();
    }

    // tries emphasis with delimiter d at s[i]; on success appends html and advances i
    static bool TryEmph(string s, int i, string d, string tag, string closeTag, Ctx c, bool inLink, int depth, StringBuilder sb, ref int pos)
    {
        int n = s.Length, dl = d.Length;
        char dc = d[0];
        int cs = i + dl;
        if (cs >= n || char.IsWhiteSpace(s[cs])) return false;
        if (dc == '_' && i > 0 && char.IsLetterOrDigit(s[i - 1])) return false;
        if (c.Budget <= 0) return false;
        int lim = Math.Min(n, cs + CloseLimit);
        c.Budget -= lim - cs;
        int j = cs;
        int found = -1;
        while (j < lim)
        {
            char x = s[j];
            if (x == '\\') { j += 2; continue; }
            if (x == dc)
            {
                int run = 0; while (j + run < n && s[j + run] == dc) run++;
                if (dl == 1)
                {
                    if (run == 1 && j > cs && !char.IsWhiteSpace(s[j - 1]) && (dc != '_' || j + 1 >= n || !char.IsLetterOrDigit(s[j + 1]))) { found = j; break; }
                    j += run; continue;
                }
                if (run >= dl && j > cs && !char.IsWhiteSpace(s[j - 1]))
                {
                    int cj = j + run - dl; // use the last dl chars of the run
                    if (dc != '_' || cj + dl >= n || !char.IsLetterOrDigit(s[cj + dl])) { found = cj; break; }
                }
                j += run; continue;
            }
            j++;
        }
        if (found < 0) return false;
        string inner = Inline(s.Substring(cs, found - cs), c, inLink, depth + 1);
        if (tag == "strong><em") sb.Append("<strong><em>").Append(inner).Append("</em></strong>");
        else sb.Append('<').Append(tag).Append('>').Append(inner).Append("</").Append(tag).Append('>');
        pos = found + dl;
        return true;
    }

    // s[open]=='[' ; parses [text](dest "title"); pos is advanced past the construct
    static bool TryLink(string s, int open, bool image, Ctx c, bool inLink, int depth, StringBuilder sb, ref int pos)
    {
        int n = s.Length;
        int lvl = 0, k = open;
        int lim = Math.Min(n, open + 2000);
        if (c.Budget <= 0) return false;
        c.Budget -= lim - open;
        int close = -1;
        for (; k < lim; k++)
        {
            char x = s[k];
            if (x == '\\') { k++; continue; }
            if (x == '[') lvl++;
            else if (x == ']') { lvl--; if (lvl == 0) { close = k; break; } }
        }
        if (close < 0 || close + 1 >= n || s[close + 1] != '(') return false;
        string url, title; int end;
        if (!ParseDest(s, close + 2, out url, out title, out end)) return false;
        string text = s.Substring(open + 1, close - open - 1);
        string safe = SafeUrl(url);
        if (image)
        {
            string alt = Esc(Unescape(text));
            if (safe == null) sb.Append(alt);
            else
            {
                sb.Append("<img src=\"" + Esc(safe) + "\" alt=\"" + alt + "\"");
                if (title != null) sb.Append(" title=\"" + Esc(title) + "\"");
                sb.Append('>');
            }
        }
        else
        {
            string inner = Inline(text, c, true, depth + 1);
            if (safe == null) sb.Append(inner);
            else
            {
                sb.Append("<a href=\"" + Esc(safe) + "\"");
                if (title != null) sb.Append(" title=\"" + Esc(title) + "\"");
                sb.Append('>').Append(inner).Append("</a>");
            }
        }
        pos = end;
        return true;
    }

    static string Unescape(string s)
    {
        if (s.IndexOf('\\') < 0) return s;
        var sb = new StringBuilder();
        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 1 < s.Length && IsPunct(s[i + 1])) { sb.Append(s[i + 1]); i++; }
            else sb.Append(s[i]);
        }
        return sb.ToString();
    }

    static bool ParseDest(string s, int p, out string url, out string title, out int end)
    {
        url = ""; title = null; end = p;
        int n = Math.Min(s.Length, p + 3000);
        while (p < n && (s[p] == ' ' || s[p] == '\t' || s[p] == '\n')) p++;
        if (p >= n) return false;
        var u = new StringBuilder();
        if (s[p] == '<')
        {
            p++;
            while (p < n && s[p] != '>' && s[p] != '\n') { u.Append(s[p]); p++; }
            if (p >= n || s[p] != '>') return false;
            p++;
        }
        else
        {
            int depth = 0;
            while (p < n)
            {
                char x = s[p];
                if (x == '\\' && p + 1 < n && IsPunct(s[p + 1])) { u.Append(s[p + 1]); p += 2; continue; }
                if (x == ' ' || x == '\t' || x == '\n') break;
                if (x == '(') depth++;
                else if (x == ')') { if (depth == 0) break; depth--; }
                u.Append(x); p++;
            }
        }
        url = u.ToString();
        int q = p;
        while (q < n && (s[q] == ' ' || s[q] == '\t' || s[q] == '\n')) q++;
        if (q < n && q > p && (s[q] == '"' || s[q] == '\'' || s[q] == '('))
        {
            char cl = s[q] == '(' ? ')' : s[q];
            int r = q + 1;
            var t = new StringBuilder();
            while (r < n && s[r] != cl)
            {
                if (s[r] == '\\' && r + 1 < n && IsPunct(s[r + 1])) { t.Append(s[r + 1]); r += 2; continue; }
                t.Append(s[r]); r++;
            }
            if (r >= n) return false;
            title = t.ToString();
            q = r + 1;
            while (q < n && (s[q] == ' ' || s[q] == '\t' || s[q] == '\n')) q++;
        }
        if (q >= n || s[q] != ')') return false;
        end = q + 1;
        return true;
    }

    // ---------------------------------------------------------------- blocks
    static bool IsBlank(string l) { return l.Length == 0 || l.Trim().Length == 0; }
    static int Indent(string l) { int i = 0; while (i < l.Length && l[i] == ' ') i++; return i; }
    static string StripIndent(string l, int k) { int i = 0; while (i < k && i < l.Length && l[i] == ' ') i++; return l.Substring(i); }
    static bool IsHr(string l) { return HrRx.IsMatch(l); }

    static bool IsStarter(string l)
    {
        if (IsBlank(l)) return true;
        if (Indent(l) >= 4) return false;
        if (FenceRx.IsMatch(l)) return true;
        if (AtxRx.IsMatch(l)) return true;
        if (IsHr(l)) return true;
        if (BqRx.IsMatch(l)) return true;
        if (ListIntRx.IsMatch(l)) return true;
        return false;
    }

    static List<string> SplitRow(string r)
    {
        r = r.Trim();
        if (r.StartsWith("|")) r = r.Substring(1);
        if (r.EndsWith("|") && !r.EndsWith("\\|")) r = r.Substring(0, r.Length - 1);
        var cells = new List<string>();
        var cur = new StringBuilder();
        for (int i = 0; i < r.Length; i++)
        {
            char ch = r[i];
            if (ch == '\\' && i + 1 < r.Length) { cur.Append(ch).Append(r[i + 1]); i++; }
            else if (ch == '|') { cells.Add(cur.ToString().Trim()); cur.Length = 0; }
            else cur.Append(ch);
        }
        cells.Add(cur.ToString().Trim());
        return cells;
    }

    // header line + separator line => aligns (0 none,1 left,2 center,3 right) or null
    static int[] TableAligns(string head, string sep)
    {
        if (head.IndexOf('|') < 0 || sep.IndexOf('-') < 0) return null;
        if (Indent(sep) >= 4) return null;
        List<string> sc = SplitRow(sep);
        var al = new int[sc.Count];
        for (int i = 0; i < sc.Count; i++)
        {
            string x = sc[i];
            if (!SepCellRx.IsMatch(x)) return null;
            bool l = x[0] == ':', r = x[x.Length - 1] == ':';
            al[i] = l && r ? 2 : l ? 1 : r ? 3 : 0;
        }
        if (SplitRow(head).Count != al.Length) return null;
        return al;
    }

    static void Blocks(List<string> L, Ctx c, StringBuilder o, bool tight, int depth)
    {
        int n = L.Count, i = 0;
        if (depth > 20)
        {
            var all = new StringBuilder();
            foreach (string l in L) all.Append(l.Trim()).Append('\n');
            o.Append("<p>").Append(Inline(all.ToString().Trim(), c, false, 0)).Append("</p>\n");
            return;
        }
        while (i < n)
        {
            string ln = L[i];
            if (IsBlank(ln)) { i++; continue; }
            int ind = Indent(ln);

            // indented code
            if (ind >= 4)
            {
                var code = new List<string>();
                int j = i;
                while (j < n && (IsBlank(L[j]) || Indent(L[j]) >= 4)) { code.Add(IsBlank(L[j]) ? "" : L[j].Substring(4)); j++; }
                while (code.Count > 0 && code[code.Count - 1].Length == 0) code.RemoveAt(code.Count - 1);
                o.Append("<pre><code>").Append(Esc(string.Join("\n", code))).Append("</code></pre>\n");
                i = j; continue;
            }

            // fenced code
            Match fm = FenceRx.Match(ln);
            if (fm.Success && !(fm.Groups[2].Value[0] == '`' && fm.Groups[3].Value.IndexOf('`') >= 0))
            {
                char fc = fm.Groups[2].Value[0]; int fl = fm.Groups[2].Length; int fi = fm.Groups[1].Length;
                var code = new List<string>();
                int j = i + 1;
                while (j < n)
                {
                    string l = L[j];
                    string t = l.TrimStart(' ');
                    int r = 0; while (r < t.Length && t[r] == fc) r++;
                    if (Indent(l) < 4 && r >= fl && t.Substring(r).Trim().Length == 0) { j++; break; }
                    code.Add(StripIndent(l, fi));
                    j++;
                }
                o.Append("<pre><code>").Append(Esc(string.Join("\n", code))).Append("</code></pre>\n");
                i = j; continue;
            }

            // atx heading
            Match am = AtxRx.Match(ln);
            if (am.Success)
            {
                string t = am.Groups[2].Success ? am.Groups[2].Value.Trim() : "";
                int e = t.Length; while (e > 0 && t[e - 1] == '#') e--;
                if (e == 0 || t[e - 1] == ' ' || t[e - 1] == '\t') t = t.Substring(0, e).TrimEnd();
                int lv = am.Groups[1].Length;
                o.Append("<h" + lv + ">").Append(Inline(t, c, false, 0)).Append("</h" + lv + ">\n");
                i++; continue;
            }

            if (IsHr(ln)) { o.Append("<hr>\n"); i++; continue; }

            // blockquote
            if (BqRx.IsMatch(ln))
            {
                var q = new List<string>();
                int j = i;
                while (j < n)
                {
                    string l = L[j];
                    if (BqRx.IsMatch(l))
                    {
                        string t = l.TrimStart(' ').Substring(1);
                        if (t.Length > 0 && t[0] == ' ') t = t.Substring(1);
                        q.Add(t);
                    }
                    else if (!IsBlank(l) && q.Count > 0 && !IsBlank(q[q.Count - 1]) && !IsStarter(l)) q.Add(l);
                    else break;
                    j++;
                }
                o.Append("<blockquote>\n");
                Blocks(q, c, o, false, depth + 1);
                o.Append("</blockquote>\n");
                i = j; continue;
            }

            // list
            if (ListRx.IsMatch(ln))
            {
                i = ParseList(L, i, c, o, depth);
                continue;
            }

            // table
            if (i + 1 < n)
            {
                int[] al = TableAligns(ln, L[i + 1]);
                if (al != null)
                {
                    int j = i + 2;
                    while (j < n && !IsBlank(L[j]) && !IsStarter(L[j])) j++;
                    EmitTable(L, i, j, al, c, o);
                    i = j; continue;
                }
            }

            // paragraph (+ setext heading)
            {
                var para = new StringBuilder();
                int j = i; int setext = 0;
                while (j < n)
                {
                    string l = L[j];
                    if (IsBlank(l)) break;
                    if (j > i)
                    {
                        Match sm = SetextRx.Match(l);
                        if (sm.Success) { setext = sm.Groups[1].Value[0] == '=' ? 1 : 2; j++; break; }
                        if (IsStarter(l)) break;
                        if (j + 1 < n && TableAligns(l, L[j + 1]) != null) break;
                    }
                    if (j > i) para.Append('\n');
                    para.Append(l.Trim());
                    j++;
                }
                string html = Inline(para.ToString(), c, false, 0);
                if (setext > 0) o.Append("<h" + setext + ">").Append(html).Append("</h" + setext + ">\n");
                else if (tight) o.Append(html).Append('\n');
                else o.Append("<p>").Append(html).Append("</p>\n");
                i = j > i ? j : i + 1;
            }
        }
    }

    static void EmitTable(List<string> L, int start, int end, int[] al, Ctx c, StringBuilder o)
    {
        o.Append("<table>\n<thead>\n<tr>\n");
        List<string> head = SplitRow(L[start]);
        for (int k = 0; k < al.Length; k++) AppendCell(o, "th", al[k], k < head.Count ? head[k] : "", c);
        o.Append("</tr>\n</thead>\n");
        if (end > start + 2)
        {
            o.Append("<tbody>\n");
            for (int r = start + 2; r < end; r++)
            {
                List<string> cells = SplitRow(L[r]);
                o.Append("<tr>\n");
                for (int k = 0; k < al.Length; k++) AppendCell(o, "td", al[k], k < cells.Count ? cells[k] : "", c);
                o.Append("</tr>\n");
            }
            o.Append("</tbody>\n");
        }
        o.Append("</table>\n");
    }

    static void AppendCell(StringBuilder o, string tag, int al, string text, Ctx c)
    {
        o.Append('<').Append(tag);
        if (al == 1) o.Append(" style=\"text-align:left\"");
        else if (al == 2) o.Append(" style=\"text-align:center\"");
        else if (al == 3) o.Append(" style=\"text-align:right\"");
        o.Append('>').Append(Inline(text, c, false, 0)).Append("</").Append(tag).Append(">\n");
    }

    static int ParseList(List<string> L, int i, Ctx c, StringBuilder o, int depth)
    {
        int n = L.Count;
        Match m0 = ListRx.Match(L[i]);
        int sInd = m0.Groups[1].Length;
        string mk0 = m0.Groups[2].Value;
        bool ord = char.IsDigit(mk0[0]);
        int startNum = 1;
        if (ord) int.TryParse(mk0.Substring(0, mk0.Length - 1), out startNum);
        var items = new List<List<string>>();
        bool loose = false;
        int j = i;
        while (j < n)
        {
            string ln = L[j];
            Match m = ListRx.Match(ln);
            if (!m.Success || m.Groups[1].Length > sInd || IsHr(ln)) break;
            if (char.IsDigit(m.Groups[2].Value[0]) != ord) break;
            int sThis = m.Groups[1].Length;
            string marker = m.Groups[2].Value;
            int sp = m.Groups[3].Success ? m.Groups[3].Length : 1;
            string first = m.Groups[4].Success ? m.Groups[4].Value : "";
            if (sp > 4) { first = new string(' ', sp - 1) + first; sp = 1; }
            int cOff = sThis + marker.Length + sp;
            var item = new List<string>();
            item.Add(first);
            int k = j + 1;
            while (k < n)
            {
                string l = L[k];
                if (IsBlank(l))
                {
                    int q = k + 1; while (q < n && IsBlank(L[q])) q++;
                    if (q >= n) { k = q; break; }
                    string nl = L[q];
                    if (Indent(nl) > sThis)
                    {
                        if (!ListRx.IsMatch(nl)) loose = true;
                        for (int x = k; x < q; x++) item.Add("");
                        k = q; continue;
                    }
                    Match sm = ListRx.Match(nl);
                    if (sm.Success && sm.Groups[1].Length <= sInd && !IsHr(nl) && char.IsDigit(sm.Groups[2].Value[0]) == ord) loose = true;
                    k = q; break;
                }
                if (Indent(l) > sThis) { item.Add(StripIndent(l, cOff)); k++; continue; }
                if (ListRx.IsMatch(l) || IsStarter(l)) break;
                if (!IsBlank(item[item.Count - 1])) { item.Add(l.TrimStart()); k++; continue; }
                break;
            }
            while (item.Count > 0 && IsBlank(item[item.Count - 1])) item.RemoveAt(item.Count - 1);
            items.Add(item);
            j = k;
        }
        if (items.Count == 0) { o.Append("<p>").Append(Inline(L[i].Trim(), c, false, 0)).Append("</p>\n"); return i + 1; }

        string tg = ord ? "ol" : "ul";
        o.Append('<').Append(tg);
        if (ord && startNum != 1) o.Append(" start=\"" + startNum + "\"");
        o.Append(">\n");
        foreach (List<string> item in items)
        {
            string checkbox = null;
            Match tm = TaskRx.Match(item[0]);
            if (tm.Success)
            {
                checkbox = "<input type=\"checkbox\" disabled" + (tm.Groups[1].Value == " " ? "" : " checked") + "> ";
                item[0] = tm.Groups[2].Value;
            }
            o.Append(checkbox != null ? "<li class=\"task\">" : "<li>");
            if (checkbox != null) o.Append(checkbox);
            Blocks(item, c, o, !loose, depth + 1);
            while (o.Length > 0 && o[o.Length - 1] == '\n') o.Length--;
            o.Append("</li>\n");
        }
        o.Append("</" + tg + ">\n");
        return j;
    }
}
