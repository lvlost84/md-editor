using System.Text;
using System.Windows.Input;

// Single table of keyboard shortcuts: used for window KeyBindings, toolbar tooltips and the F1 help table (kept identical on purpose).
class ShortcutDef
{
    public string Id, Desc;
    public Key Key;
    public ModifierKeys Mod;
    public bool EditorOnly;   // acts only while the editor has keyboard focus
    public bool Builtin;      // handled by AvalonEdit itself (no window KeyBinding)

    public string Display(string sep)
    {
        var sb = new StringBuilder();
        if ((Mod & ModifierKeys.Control) != 0) sb.Append("Ctrl").Append(sep);
        if ((Mod & ModifierKeys.Alt) != 0) sb.Append("Alt").Append(sep);
        if ((Mod & ModifierKeys.Shift) != 0) sb.Append("Shift").Append(sep);
        sb.Append(KeyName(Key));
        return sb.ToString();
    }

    public static string KeyName(Key k)
    {
        if (k >= Key.D0 && k <= Key.D9) return ((int)k - (int)Key.D0).ToString();
        if (k == Key.OemTilde) return "`";
        if (k == Key.Return) return "Enter";
        if (k == Key.OemComma) return ",";
        return k.ToString();
    }
}

static class Shortcuts
{
    const ModifierKeys C = ModifierKeys.Control, A = ModifierKeys.Alt, S = ModifierKeys.Shift, N = ModifierKeys.None;
    static ShortcutDef D(string id, Key k, ModifierKeys m, string desc, bool editorOnly = false, bool builtin = false)
    {
        return new ShortcutDef { Id = id, Key = k, Mod = m, Desc = desc, EditorOnly = editorOnly, Builtin = builtin };
    }

    public static readonly ShortcutDef[] All =
    {
        D("new", Key.N, C, "새 문서"), D("open", Key.O, C, "파일 열기"), D("save", Key.S, C, "저장"),
        D("saveas", Key.F12, N, "다른 이름으로 저장"), D("saveas2", Key.S, C | S, "다른 이름으로 저장"), D("print", Key.P, C, "인쇄 / PDF"),
        D("bold", Key.B, C, "굵게", true), D("italic", Key.I, C, "기울임", true), D("strike", Key.X, C | S, "취소선", true),
        D("link", Key.K, C, "링크", true),
        D("h1", Key.D1, C | A, "제목 1 (같은 단축키를 다시 누르면 해제)", true), D("h2", Key.D2, C | A, "제목 2", true), D("h3", Key.D3, C | A, "제목 3", true),
        D("h4", Key.D4, C | A, "제목 4", true), D("h5", Key.D5, C | A, "제목 5", true), D("h6", Key.D6, C | A, "제목 6", true),
        D("normal", Key.D0, C | A, "본문으로 (제목/목록/인용 표시 제거)", true),
        D("bullet", Key.L, C | S, "글머리 목록", true), D("numbered", Key.O, C | S, "번호 목록", true), D("quote", Key.Q, C | S, "인용", true),
        D("code", Key.OemTilde, C, "인라인 코드", true), D("codeblock", Key.K, C | S, "코드 블록", true),
        D("table", Key.T, C | A, "표 만들기", true), D("footnote", Key.F, C | A, "각주", true), D("hr", Key.Return, C, "가로줄", true),
        D("find", Key.F, C, "찾기 (대/소문자, 정규식)", true, true), D("findnext", Key.F3, N, "다음 찾기", true, true), D("findprev", Key.F3, S, "이전 찾기", true, true),
        D("replace", Key.H, C, "바꾸기 창"), D("goto", Key.G, C, "줄 이동"),
        D("moveup", Key.Up, A | S, "현재 줄(선택한 줄) 위로 이동", true), D("movedown", Key.Down, A | S, "현재 줄(선택한 줄) 아래로 이동", true),
        D("indent", Key.Tab, N, "선택한 줄 들여쓰기 (칸 수는 옵션에서)", true, true), D("outdent", Key.Tab, S, "선택한 줄 내어쓰기", true, true),
        D("help", Key.F1, N, "이 도움말"), D("logfolder", Key.L, C | A, "로그 폴더 열기"), D("log", Key.F12, S, "진단 정보(로그) 보기"),
        D("dark", Key.D, C | A, "다크/라이트 모드 전환"), D("pin", Key.P, C | A, "항상 위 켜기/끄기"), D("view", Key.V, C | S, "보기 전환 (편집 / 분할 / 미리보기)"),
        D("zoomreset", Key.D0, C, "글자 크기 100%로 (Ctrl+마우스 휠로 확대/축소)"),
        D("options", Key.OemComma, C, "옵션 창 (글꼴, 줄 번호, 자동 저장 등)"),
    };

    public static ShortcutDef Get(string id)
    {
        foreach (var d in All) if (d.Id == id) return d;
        return null;
    }

    // " (Ctrl+B)" for tooltips
    public static string Tip(string text, string id)
    {
        var d = Get(id);
        return d == null ? text : text + " (" + d.Display("+") + ")";
    }

    // extra rows for AvalonEdit built-ins shown in help
    public static readonly string[][] BuiltinRows =
    {
        new[] { "Ctrl + Z / Ctrl + Y", "실행 취소 / 다시 실행" },
        new[] { "Ctrl + A / X / C / V", "전체 선택 / 잘라내기 / 복사 / 붙여넣기" },
        new[] { "Ctrl + Home / End", "문서 처음 / 끝으로" },
        new[] { "Ctrl + 방향키", "단어 단위 이동" },
        new[] { "Ctrl + D", "현재 줄 삭제" },
    };
}
