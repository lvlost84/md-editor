using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

// Pure line-based markdown edits for the keyboard shortcuts / toolbar (headings, lists, quote, normal text).
// Works on every line touched by the selection; newline style of each line is preserved. No UI types.
static class LineOps
{
    static readonly Regex HeadRe = new Regex(@"^([ ]{0,3})(#{1,6})([ \t]+|$)");
    static readonly Regex BulletRe = new Regex(@"^(\s*)[-*+][ \t]+");
    static readonly Regex NumRe = new Regex(@"^(\s*)\d+[.)][ \t]+");
    static readonly Regex QuoteRe = new Regex(@"^(\s*)>[ \t]?");

    class Blk
    {
        public int Start, End;          // block = text[Start..End), last line's delimiter excluded
        public string[] Lines;          // without '\n'; may keep a trailing '\r'
        public int CaretLine;           // index of the line holding selStart
        public int CaretCol;            // column of selStart inside that line
    }

    static Blk Get(string text, int selStart, int selLen)
    {
        text = text ?? "";
        int s = Math.Max(0, Math.Min(selStart, text.Length));
        int e = Math.Max(s, Math.Min(s + Math.Max(0, selLen), text.Length));
        if (e > s && text[e - 1] == '\n') e--;                       // selection ending at a line start does not include that line
        int st = s == 0 ? 0 : text.LastIndexOf('\n', s - 1) + 1;
        int en = text.IndexOf('\n', e);
        if (en < 0) en = text.Length;
        var b = new Blk { Start = st, End = en, Lines = text.Substring(st, en - st).Split('\n') };
        int pos = st;
        for (int i = 0; i < b.Lines.Length; i++)
        {
            int next = pos + b.Lines[i].Length + 1;
            if (s < next || i == b.Lines.Length - 1) { b.CaretLine = i; b.CaretCol = s - pos; break; }
            pos = next;
        }
        return b;
    }

    static string Body(string line) { return line.EndsWith("\r") ? line.Substring(0, line.Length - 1) : line; }
    static string Cr(string line) { return line.EndsWith("\r") ? "\r" : ""; }
    static bool Blank(string line) { return Body(line).Trim().Length == 0; }

    static EditResult Done(string text, Blk b, string[] lines, int selLen)
    {
        string nb = string.Join("\n", lines);
        string r = text.Substring(0, b.Start) + nb + text.Substring(b.End);
        if (selLen > 0) return new EditResult { Ok = true, Text = r, SelStart = b.Start, SelLength = nb.Length };
        int p = b.Start;
        for (int i = 0; i < b.CaretLine; i++) p += lines[i].Length + 1;
        int delta = lines[b.CaretLine].Length - b.Lines[b.CaretLine].Length;
        int col = Math.Max(0, Math.Min(b.CaretCol + delta, lines[b.CaretLine].Length));
        return new EditResult { Ok = true, Text = r, SelStart = p + col, SelLength = 0 };
    }

    static string StripAll(string body)
    {
        // heading / quote / list markers (a list marker keeps its indentation removed too)
        string t = body;
        for (int guard = 0; guard < 8; guard++)
        {
            Match m;
            if ((m = HeadRe.Match(t)).Success) { t = t.Substring(m.Length); continue; }
            if ((m = QuoteRe.Match(t)).Success) { t = t.Substring(m.Length); continue; }
            if ((m = BulletRe.Match(t)).Success) { t = t.Substring(m.Length); continue; }
            if ((m = NumRe.Match(t)).Success) { t = t.Substring(m.Length); continue; }
            break;
        }
        return t;
    }

    // Ctrl+Alt+1..6: set heading level; pressing the same level again removes it.
    public static EditResult Heading(string text, int selStart, int selLen, int level)
    {
        level = Math.Max(1, Math.Min(6, level));
        var b = Get(text, selStart, selLen);
        bool multi = b.Lines.Length > 1;
        bool allSame = true, any = false;
        foreach (var l in b.Lines)
        {
            if (multi && Blank(l)) continue;
            any = true;
            var m = HeadRe.Match(Body(l));
            if (!m.Success || m.Groups[2].Length != level) allSame = false;
        }
        if (!any) allSame = false;
        var res = new string[b.Lines.Length];
        for (int i = 0; i < res.Length; i++)
        {
            string l = b.Lines[i];
            if (multi && Blank(l)) { res[i] = l; continue; }
            string body = Body(l);
            var m = HeadRe.Match(body);
            string rest = m.Success ? body.Substring(m.Length) : body;
            res[i] = (allSame ? rest : new string('#', level) + " " + rest) + Cr(l);
        }
        return Done(text, b, res, selLen);
    }

    // Ctrl+Alt+0: plain paragraph text.
    public static EditResult Normal(string text, int selStart, int selLen)
    {
        var b = Get(text, selStart, selLen);
        var res = new string[b.Lines.Length];
        for (int i = 0; i < res.Length; i++) res[i] = StripAll(Body(b.Lines[i])) + Cr(b.Lines[i]);
        return Done(text, b, res, selLen);
    }

    // kind: "bullet" | "numbered" | "quote". Toggles; switching between list kinds replaces the old marker.
    public static EditResult List(string text, int selStart, int selLen, string kind)
    {
        var b = Get(text, selStart, selLen);
        bool multi = b.Lines.Length > 1;
        Regex re = kind == "bullet" ? BulletRe : kind == "numbered" ? NumRe : QuoteRe;
        bool all = true, any = false;
        foreach (var l in b.Lines)
        {
            if (multi && Blank(l)) continue;
            any = true;
            if (!re.IsMatch(Body(l))) all = false;
        }
        if (!any) all = false;
        var res = new string[b.Lines.Length];
        int n = 0;
        for (int i = 0; i < res.Length; i++)
        {
            string l = b.Lines[i];
            if (multi && Blank(l)) { res[i] = l; continue; }
            string body = Body(l);
            string indent = "";
            string rest = body;
            Match m;
            if (kind == "quote")
            {
                if (all) { m = QuoteRe.Match(body); rest = body.Substring(m.Length); res[i] = m.Groups[1].Value + rest + Cr(l); }
                else res[i] = "> " + body + Cr(l);
                continue;
            }
            if ((m = BulletRe.Match(body)).Success || (m = NumRe.Match(body)).Success) { indent = m.Groups[1].Value; rest = body.Substring(m.Length); }
            else { var w = Regex.Match(body, @"^\s*"); indent = w.Value; rest = body.Substring(w.Length); m = null; }
            if (all) res[i] = indent + rest + Cr(l);
            else { n++; res[i] = indent + (kind == "bullet" ? "- " : n + ". ") + rest + Cr(l); }
        }
        return Done(text, b, res, selLen);
    }

    // ---- move lines (Alt+Shift+Up/Down): returns new text and shifted selection, Ok=false when already at the edge
    public static EditResult Move(string text, int selStart, int selLen, bool up)
    {
        text = text ?? "";
        var b = Get(text, selStart, selLen);
        string nl = text.IndexOf("\r\n", StringComparison.Ordinal) >= 0 ? "\r\n" : "\n";
        if (up)
        {
            if (b.Start == 0) return new EditResult { Ok = false, Text = text, SelStart = selStart, SelLength = selLen };
            int pe = b.Start - 1;                                   // index of '\n' ending the previous line
            int ps = pe == 0 ? 0 : text.LastIndexOf('\n', pe - 1) + 1;
            int bodyEnd = pe > ps && text[pe - 1] == '\r' ? pe - 1 : pe;
            string prev = text.Substring(ps, bodyEnd - ps);
            string sep = text.Substring(bodyEnd, b.Start - bodyEnd);
            int blkEnd = b.End > b.Start && text[b.End - 1] == '\r' ? b.End - 1 : b.End;
            string blk = text.Substring(b.Start, blkEnd - b.Start);
            string r = text.Substring(0, ps) + blk + sep + prev + text.Substring(blkEnd);
            int shift = prev.Length + sep.Length;
            return new EditResult { Ok = true, Text = r, SelStart = selStart - shift, SelLength = selLen };
        }
        else
        {
            if (b.End >= text.Length) return new EditResult { Ok = false, Text = text, SelStart = selStart, SelLength = selLen };
            int ne = b.End;                                         // index of '\n' ending the block
            int sepStart = ne > b.Start && text[ne - 1] == '\r' ? ne - 1 : ne;
            string sep = text.Substring(sepStart, ne + 1 - sepStart);
            int ns = ne + 1;
            int nn = text.IndexOf('\n', ns);
            int nextEnd = nn < 0 ? text.Length : nn;
            int nextBodyEnd = nextEnd > ns && text[nextEnd - 1] == '\r' ? nextEnd - 1 : nextEnd;
            string next = text.Substring(ns, nextBodyEnd - ns);
            string blk = text.Substring(b.Start, sepStart - b.Start);
            string r = text.Substring(0, b.Start) + next + sep + blk + text.Substring(nextBodyEnd);
            int shift = next.Length + sep.Length;
            return new EditResult { Ok = true, Text = r, SelStart = selStart + shift, SelLength = selLen };
        }
    }
}
