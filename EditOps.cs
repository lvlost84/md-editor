// Pure string editing ops (no UI types). Newline style of the document is preserved (\r\n if present, else \n).
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
struct EditResult
{
    public bool Ok; public string Error;          // Ok=false -> Error is a Korean user message
    public string Text; public int SelStart, SelLength;
}
static class EditOps
{
    static string Nz(string t) { return t ?? ""; }
    static int Cl(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }
    static string Nl(string t) { return t.IndexOf("\r\n", StringComparison.Ordinal) >= 0 ? "\r\n" : "\n"; }
    static string ToNl(string s, string nl) { s = s.Replace("\r\n", "\n"); return nl == "\n" ? s : s.Replace("\n", nl); }
    static EditResult Ok(string t, int s, int l) { return new EditResult { Ok = true, Text = t, SelStart = s, SelLength = l }; }
    static EditResult Fail(string t, string e) { return new EditResult { Ok = false, Error = e, Text = t, SelStart = 0, SelLength = 0 }; }

    public static EditResult Wrap(string text, int selStart, int selLen, string before, string after)
    {
        text = Nz(text); before = before ?? ""; after = after ?? "";
        int s = Cl(selStart, 0, text.Length); int l = Cl(selLen, 0, text.Length - s);
        string inner = l > 0 ? text.Substring(s, l) : "텍스트";
        string r = text.Substring(0, s) + before + inner + after + text.Substring(s + l);
        return Ok(r, s + before.Length, inner.Length);
    }

    public static EditResult LineStart(string text, int caret, string prefix)
    {
        text = Nz(text); prefix = prefix ?? "";
        int c = Cl(caret, 0, text.Length);
        int st = c == 0 ? 0 : text.LastIndexOf('\n', c - 1) + 1;
        return Ok(text.Substring(0, st) + prefix + text.Substring(st), st + prefix.Length, 0);
    }

    public static EditResult Block(string text, int caret, string block)
    {
        text = Nz(text); block = block ?? "";
        int c = Cl(caret, 0, text.Length);
        string nl = Nl(text);
        string ins = ToNl(block, nl);
        if (c > 0 && text[c - 1] != '\n') ins = nl + ins;
        return Ok(text.Substring(0, c) + ins + text.Substring(c), c + ins.Length, 0);
    }

    public static EditResult CodeBlock(string text, int caret) { return Block(text, caret, "\n```\n코드 입력\n```\n"); }

    public static EditResult Link(string text, int selStart, int selLen) { return Wrap(text, selStart, selLen, "[", "](https://)"); }

    public static EditResult InsertTable(string text, int caret, int rows, int cols)
    {
        rows = Cl(rows, 1, 30); cols = Cl(cols, 1, 15);
        var sb = new StringBuilder("\n|");
        for (int i = 1; i <= cols; i++) sb.Append(" 제목" + i + " |");
        sb.Append("\n|");
        for (int i = 0; i < cols; i++) sb.Append(" --- |");
        for (int r = 0; r < rows; r++) { sb.Append("\n|"); for (int i = 0; i < cols; i++) sb.Append("  |"); }
        sb.Append("\n");
        return Block(text, caret, sb.ToString());
    }

    // line bounds (end excludes \n and a preceding \r)
    static void LineBounds(string t, int pos, out int st, out int en)
    {
        st = pos == 0 ? 0 : t.LastIndexOf('\n', pos - 1) + 1;
        int nlp = t.IndexOf('\n', pos);
        en = nlp < 0 ? t.Length : nlp;
        if (en > st && t[en - 1] == '\r') en--;
    }
    static bool HasPipe(string t, int st, int en) { return en > st && t.IndexOf('|', st, en - st) >= 0; }

    static bool FindTable(string t, int caret, out int bst, out int ben)
    {
        bst = ben = 0;
        int st, en; LineBounds(t, caret, out st, out en);
        if (!HasPipe(t, st, en)) return false;
        bst = st; ben = en;
        while (bst > 0)
        {
            int ps, pe; LineBounds(t, bst - 1, out ps, out pe);
            if (!HasPipe(t, ps, pe)) break;
            bst = ps;
        }
        while (true)
        {
            int nlp = t.IndexOf('\n', ben);
            if (nlp < 0) break;
            int ns = nlp + 1, ne;
            if (ns >= t.Length) break;
            int dummy; LineBounds(t, ns, out dummy, out ne);
            if (!HasPipe(t, ns, ne)) break;
            ben = ne;
        }
        return true;
    }
    const string NoTable = "커서를 표 안의 줄에 놓고 다시 눌러주세요.";

    public static EditResult AddRow(string text, int caret)
    {
        text = Nz(text);
        int c = Cl(caret, 0, text.Length);
        int bs, be;
        if (!FindTable(text, c, out bs, out be)) return Fail(text, NoTable);
        int ls, le; LineBounds(text, be, out ls, out le);
        string last = text.Substring(ls, le - ls).Trim();
        var parts = new List<string>(last.Split('|'));
        if (parts.Count > 0 && parts[0].Trim() == "") parts.RemoveAt(0);
        if (parts.Count > 0 && parts[parts.Count - 1].Trim() == "") parts.RemoveAt(parts.Count - 1);
        int n = Math.Max(1, parts.Count);
        var sb = new StringBuilder("|");
        for (int i = 0; i < n; i++) sb.Append("  |");
        string nl = Nl(text);
        string r = text.Substring(0, be) + nl + sb + text.Substring(be);
        return Ok(r, be + nl.Length + 2, 0);
    }

    static readonly Regex SepRe = new Regex(@"^[\s|:\-]+$");

    public static EditResult AddCol(string text, int caret)
    {
        text = Nz(text);
        int c = Cl(caret, 0, text.Length);
        int bs, be;
        if (!FindTable(text, c, out bs, out be)) return Fail(text, NoTable);
        string nl = Nl(text);
        string[] lines = text.Substring(bs, be - bs).Split('\n');
        int cls, cle; LineBounds(text, c, out cls, out cle);
        int caretLine = 0, caretCol = c - cls;
        { int p = bs; for (int i = 0; i < lines.Length; i++) { if (p == cls) { caretLine = i; break; } p += lines[i].Length + 1; } }
        var outp = new StringBuilder();
        int newCaret = bs;
        for (int i = 0; i < lines.Length; i++)
        {
            string ln = lines[i].TrimEnd(' ', '\t', '\r');
            string cell = (SepRe.IsMatch(ln) && ln.IndexOf('-') >= 0) ? " --- " : "  ";
            string nw = ln.EndsWith("|") ? ln.Substring(0, ln.Length - 1) + cell + "|" : ln + " | " + cell.Trim();
            if (i > 0) outp.Append(nl);
            if (i == caretLine) newCaret = bs + outp.Length + Math.Min(caretCol, nw.Length);
            outp.Append(nw);
        }
        string r = text.Substring(0, bs) + outp + text.Substring(be);
        return Ok(r, newCaret, 0);
    }

    static readonly Regex FnRe = new Regex(@"\[\^(\d+)\]");

    public static EditResult InsertFootnote(string text, int caret)
    {
        text = Nz(text);
        int c = Cl(caret, 0, text.Length);
        long max = 0;
        foreach (Match m in FnRe.Matches(text))
        {
            long v; if (m.Groups[1].Value.Length < 10 && long.TryParse(m.Groups[1].Value, out v) && v > max) max = v;
        }
        long n = max + 1;
        string nl = Nl(text);
        string t = text.Substring(0, c) + "[^" + n + "]" + text.Substring(c);
        t = t + (t.EndsWith("\n") ? nl : nl + nl) + "[^" + n + "]: ";
        return Ok(t, t.Length, 0);
    }

    public static void Count(string text, out int chars, out int words)
    {
        text = Nz(text);
        chars = text.Length;
        words = 0;
        bool inW = false;
        foreach (char ch in text)
        {
            bool ws = char.IsWhiteSpace(ch);
            if (!ws && !inW) words++;
            inW = !ws;
        }
    }
}
