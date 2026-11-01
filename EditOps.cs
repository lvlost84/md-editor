// OWNER: implementer B. Pure string editing ops (no UI types). STUB: replace bodies, keep signatures.
using System;
struct EditResult
{
    public bool Ok; public string Error;          // Ok=false -> Error is a Korean user message
    public string Text; public int SelStart, SelLength;
}
static class EditOps
{
    public static EditResult Wrap(string text, int selStart, int selLen, string before, string after) { throw new NotImplementedException(); }
    public static EditResult LineStart(string text, int caret, string prefix) { throw new NotImplementedException(); }
    public static EditResult Block(string text, int caret, string block) { throw new NotImplementedException(); }
    public static EditResult CodeBlock(string text, int caret) { throw new NotImplementedException(); }
    public static EditResult Link(string text, int selStart, int selLen) { throw new NotImplementedException(); }
    public static EditResult InsertTable(string text, int caret, int rows, int cols) { throw new NotImplementedException(); }
    public static EditResult AddRow(string text, int caret) { throw new NotImplementedException(); }
    public static EditResult AddCol(string text, int caret) { throw new NotImplementedException(); }
    public static EditResult InsertFootnote(string text, int caret) { throw new NotImplementedException(); }
    public static void Count(string text, out int chars, out int words) { throw new NotImplementedException(); }
}
