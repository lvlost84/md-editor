using System;
using System.Collections.Generic;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Search;

// Window-level KeyBindings built from the Shortcuts table (Shortcuts.cs). Editor-only commands act only while the editor has focus.
partial class MainWindow
{
    Dictionary<string, Action> actions;
    bool bypassFocus;   // test hook: run editor-only commands without real keyboard focus

    class Cmd : ICommand
    {
        readonly Action a;
        public Cmd(Action a) { this.a = a; }
        public bool CanExecute(object p) { return true; }
        public void Execute(object p) { a(); }
        public event EventHandler CanExecuteChanged { add { } remove { } }
    }

    void ApplyBindings()
    {
        actions = new Dictionary<string, Action>
        {
            { "new", NewDoc }, { "open", () => DoOpen() }, { "save", () => Save() }, { "saveas", () => SaveAs() }, { "saveas2", () => SaveAs() },
            { "print", () => PrintPdf() },
            { "bold", () => WrapSel("**") }, { "italic", () => WrapSel("*") }, { "strike", () => WrapSel("~~") },
            { "link", () => ApplyOp(EditOps.Link(editor.Text, editor.SelectionStart, editor.SelectionLength)) },
            { "h1", () => DoHeading(1) }, { "h2", () => DoHeading(2) }, { "h3", () => DoHeading(3) },
            { "h4", () => DoHeading(4) }, { "h5", () => DoHeading(5) }, { "h6", () => DoHeading(6) },
            { "normal", () => ApplyOp(LineOps.Normal(editor.Text, editor.SelectionStart, editor.SelectionLength)) },
            { "bullet", () => DoList("bullet") }, { "numbered", () => DoList("numbered") }, { "quote", () => DoList("quote") },
            { "code", () => WrapSel("`") },
            { "codeblock", () => ApplyOp(EditOps.CodeBlock(editor.Text, editor.SelectionStart)) },
            { "table", () => MakeTable() },
            { "footnote", () => ApplyOp(EditOps.InsertFootnote(editor.Text, editor.SelectionStart)) },
            { "hr", () => ApplyOp(EditOps.Block(editor.Text, editor.SelectionStart + editor.SelectionLength, "\n---\n")) },
            { "replace", ShowReplace }, { "goto", GoToLineCmd },
            { "moveup", () => MoveLines(true) }, { "movedown", () => MoveLines(false) },
            { "help", ShowHelp }, { "logfolder", OpenDataFolder }, { "log", ShowLog },
            { "dark", ToggleDark }, { "pin", TogglePin }, { "view", CycleView }, { "zoomreset", ZoomReset },
            { "options", ShowOptions }, { "about", ShowAbout },
        };
        foreach (var d in Shortcuts.All)
        {
            if (d.Builtin) continue;
            Action a;
            if (!actions.TryGetValue(d.Id, out a)) continue;
            var def = d; var act = a;
            InputBindings.Add(new KeyBinding(new Cmd(() =>
            {
                if (def.EditorOnly && !bypassFocus && !editor.IsKeyboardFocusWithin) return;
                act();
            }), def.Key, def.Mod));
        }
        InputBindings.Add(new KeyBinding(new Cmd(ZoomReset), Key.NumPad0, ModifierKeys.Control));
    }

    // ---- test support: "Ctrl+Alt+2" style strings
    internal bool ShortcutBound(string display)
    {
        foreach (var d in Shortcuts.All)
        {
            if (!string.Equals(d.Display("+"), display, StringComparison.OrdinalIgnoreCase)) continue;
            if (d.Builtin) return true;
            foreach (InputBinding b in InputBindings)
            {
                var kb = b as KeyBinding;
                if (kb != null && kb.Key == d.Key && kb.Modifiers == d.Mod) return true;
            }
        }
        return false;
    }

    internal bool RunShortcut(string display, bool bypass = true)
    {
        foreach (var d in Shortcuts.All)
        {
            if (!string.Equals(d.Display("+"), display, StringComparison.OrdinalIgnoreCase)) continue;
            if (d.Builtin)
            {
                switch (d.Id)
                {
                    case "find": ApplicationCommands.Find.Execute(null, editor.TextArea); return true;
                    case "findnext": SearchCommands.FindNext.Execute(null, editor.TextArea); return true;
                    case "findprev": SearchCommands.FindPrevious.Execute(null, editor.TextArea); return true;
                    case "indent": System.Windows.Documents.EditingCommands.TabForward.Execute(null, editor.TextArea); return true;
                    case "outdent": System.Windows.Documents.EditingCommands.TabBackward.Execute(null, editor.TextArea); return true;
                }
                return false;
            }
            foreach (InputBinding b in InputBindings)
            {
                var kb = b as KeyBinding;
                if (kb != null && kb.Key == d.Key && kb.Modifiers == d.Mod)
                {
                    bypassFocus = bypass;
                    try { kb.Command.Execute(null); } finally { bypassFocus = false; }
                    return true;
                }
            }
        }
        return false;
    }
}
