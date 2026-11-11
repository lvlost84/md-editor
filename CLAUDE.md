# markdown-editor-wpf — detail note

Read `구조.md` first (file → responsibility map). User guide: `사용설명서.md`.

- **v3.0.0**: WPF code-only (VS2017 csc, C# 7.3, `build.bat`, `OUTDIR` env) rewrite of `electron-app/` (original untouched). Editor = AvalonEdit 6.3.0.90 (MIT), converter = Markdig 0.37.0 (BSD-2), both vendored in `lib\` (net462 + System.Memory deps) and **embedded in the exe as resources** (single-file; `Program.Embedded` AssemblyResolve). `*.dll` is globally gitignored → `git add -f lib/*.dll`.
- `Md.cs` = Markdig pipeline + AST sanitizer (URL allow-list, raw HTML escaped via DisableHtml, own footnote pre-pass, 4 s timeout → escaped `<pre>`), `EditOps.cs`/`LineOps.cs` pure string ops applied as ONE `Document.Replace` (`ApplyEdit`), `Shortcuts.cs` = 41 Word-style shortcuts table driving bindings, tooltips and F1 help. UI: `Theme.cs` (tokens + styles), `Icons.cs` (vector line icons), `MainWindow*.cs`.
- **Gotchas learned**: never set `TextBlock.LineHeight/BlockLineHeight` on a TextBox/editor (TextBox line geometry drifts on long docs, line numbers vanish — fixed in v2.2.1); `FontFamily` named instances are immutable; perl/sed edits mangle `\r\n` in C# string literals — use the Edit tool; CRLF files need CRLF-aware replacements.
- **Dev flags** (isolated temp profile, bypass single-instance mutex): `--shot <png> [dark] [file.md]`, `--help-shot <png> [dark]`. `--opt-shot <png> [dark]` (options window). Options window (v3.1): Ctrl+, / gear icon, live-apply, stored in `view.json` (Prefs). `--selftest <new empty dir>` (~640 checks, child runs with redirected USERPROFILE/APPDATA).
- **Never** kill/overwrite the user's deployed copy (`C:\TEMP\app\MD_Editor\`); build to `OUTDIR=out_dev` for checks. Real %AppData%\MdEditor holds settings.json/view.json/recovery.md/error.log.
- Unverified: real print-to-PDF, close-confirm dialog branches, real key routing vs AvalonEdit (tests call commands directly), NavigateToString memory growth.
