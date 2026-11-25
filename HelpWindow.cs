using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;

// 마크다운 문법 도움말 창: 항목별 설명 + 입력 예시 + 실제 렌더 결과 (Md 변환기로 생성, 현재 테마 따라감)
class HelpWindow : Window
{
    class Topic
    {
        public string Id, Title, Desc, Example;
        public bool NoRender;   // 결과를 그릴 수 없는 예시(이미지 등)는 입력만 표시
        public Topic(string id, string title, string desc, string example, bool noRender = false)
        { Id = id; Title = title; Desc = desc; Example = example; NoRender = noRender; }
    }

    static readonly Topic[] Topics =
    {
        new Topic("h", "제목", "줄 맨 앞에 # 을 1~6개 붙입니다. 개수가 많을수록 작은 제목입니다. # 뒤에 공백이 필요합니다.",
            "# 제목 1\n## 제목 2\n### 제목 3"),
        new Topic("em", "강조 (굵게 / 기울임 / 취소선)", "별표 2개는 굵게, 1개는 기울임, 물결 2개는 취소선입니다. (Ctrl+B / Ctrl+I)",
            "**굵게**  *기울임*  ***굵고 기울임***  ~~취소선~~"),
        new Topic("p", "문단과 줄바꿈", "빈 줄로 문단을 나눕니다. 한 줄 안에서 Enter만 눌러도 줄이 바뀝니다.",
            "첫 번째 문단입니다.\n같은 문단 안의 다음 줄입니다.\n\n빈 줄 뒤의 새 문단입니다."),
        new Topic("ul", "목록 (순서 없음)", "- 또는 * 로 시작합니다. 앞에 공백 2~4칸을 두면 하위 목록이 됩니다.",
            "- 사과\n- 배\n  - 하위 항목 A\n  - 하위 항목 B\n- 포도"),
        new Topic("ol", "목록 (번호)", "숫자와 마침표로 시작합니다. 번호는 자동으로 이어집니다.",
            "1. 첫째\n2. 둘째\n3. 셋째"),
        new Topic("task", "작업 목록 (체크박스)", "목록 항목 앞에 [ ] 또는 [x] 를 붙입니다.",
            "- [x] 완료한 일\n- [ ] 해야 할 일"),
        new Topic("bq", "인용", "줄 맨 앞에 > 를 붙입니다. 여러 줄이나 중첩도 가능합니다.",
            "> 인용문입니다.\n> 두 번째 줄입니다.\n>\n> > 중첩 인용"),
        new Topic("code", "코드 (인라인 / 블록)", "백틱 ` 한 쌍은 글 속 코드, 백틱 3개 ``` 로 감싸면 코드 블록입니다. 코드 안의 마크다운은 해석되지 않습니다.",
            "변수 `count` 를 사용합니다.\n\n```\nfor (int i = 0; i < 3; i++)\n    Console.WriteLine(i);\n```"),
        new Topic("link", "링크", "[표시할 글](주소) 형식입니다. http/https/mailto 주소를 쓸 수 있고, 주소만 적어도 자동으로 링크가 됩니다.",
            "[구글](https://www.google.com)\n\nhttps://example.com"),
        new Topic("img", "이미지", "![설명](이미지 경로 또는 주소) 형식입니다. 로컬 파일은 file:///C:/폴더/그림.png 처럼 전체 경로를 씁니다. (이 도움말에서는 결과를 그리지 않습니다.)",
            "![로고](https://example.com/logo.png)", true),
        new Topic("table", "표", "| 로 칸을 나누고 둘째 줄에 --- 를 씁니다. 콜론으로 정렬을 지정합니다 (:--- 왼쪽, :---: 가운데, ---: 오른쪽). 도구 모음의 '표 만들기 / 행 추가 / 열 추가'를 쓰면 편합니다.",
            "| 항목 | 수량 | 비고 |\n| :--- | :---: | ---: |\n| 사과 | 3 | 신선 |\n| 배 | 12 | 할인 |"),
        new Topic("hr", "구분선", "줄에 --- 또는 *** 만 적습니다. (바로 윗줄이 글이면 제목으로 해석되니 윗줄을 비워 두세요.)",
            "위쪽 내용\n\n---\n\n아래쪽 내용"),
        new Topic("fn", "각주", "본문에 [^번호] 를 쓰고, 문서 아래쪽에 [^번호]: 설명 을 적습니다. 미리보기에서 번호가 자동으로 매겨지고 아래에 모아 보입니다. 도구 모음의 '각주' 버튼이 번호를 자동으로 넣어 줍니다.",
            "본문 내용입니다.[^1] 또 다른 주석도 있습니다.[^2]\n\n[^1]: 첫 번째 각주 설명\n[^2]: 두 번째 각주 설명"),
        new Topic("esc", "특수문자 그대로 쓰기", "마크다운 기호를 글자 그대로 보이려면 앞에 \\ 를 붙입니다.",
            "\\*별표\\* 는 기울임이 아닙니다.\n\n\\# 샵 기호"),
        new Topic("html", "HTML 태그", "보안을 위해 문서 안의 HTML 태그는 해석하지 않고 입력한 그대로 글자로 보여 줍니다. 굵게/기울임 등은 마크다운 기호(**, *)를 사용하세요.",
            "<b>굵게</b> 대신 **굵게**, <i>기울임</i> 대신 *기울임*"),
    };

    readonly WebBrowser browser = new WebBrowser();
    readonly bool dark;

    public HelpWindow(Window owner, bool dark)
    {
        this.dark = dark;
        Owner = owner;
        Title = "마크다운 문법 도움말";
        Icon = AppInfo.LoadIcon();
        Width = 900; Height = 760; MinWidth = 600; MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Theme.Bind(this, BackgroundProperty, "Bg");
        Content = browser;
        browser.Navigating += OnNavigating;
        browser.LoadCompleted += (s, e) => MakeSilent();
        browser.Navigated += (s, e) => MakeSilent();
        PreviewKeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Escape) Close(); };
        browser.NavigateToString(BuildHtml(dark));
    }

    internal object EvalForTest(string js)
    {
        try { return browser.InvokeScript("eval", new object[] { js }); } catch (Exception ex) { return "ERR " + ex.Message; }
    }

    void MakeSilent()
    {
        try
        {
            var f = typeof(WebBrowser).GetField("_axIWebBrowser2", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (f != null) { dynamic ax = f.GetValue(browser); if (ax != null) ax.Silent = true; }
        }
        catch { }
    }

    // 문서 안 #목차 이동만 허용, 그 외 이동은 막는다
    void OnNavigating(object s, System.Windows.Navigation.NavigatingCancelEventArgs e)
    {
        if (e.Uri == null) return;                        // NavigateToString 최초 로드
        if (e.Uri.Scheme == "about") return;              // about:blank#id 앵커
        e.Cancel = true;
    }

    // 목차 링크: 주소 이동(about:blank#id)은 NavigateToString 문서에서 빈 페이지/오류를 만들 수 있으므로, 이동 없이 스크립트로 해당 위치까지 스크롤한다
    internal static string TocLink(string id, string title)
    {
        return "<a href=\"#\" data-to=\"" + id + "\" onclick=\"var e=document.getElementById('" + id + "');if(e){e.scrollIntoView(true);}return false;\">" + Esc(title) + "</a>";
    }

    static string Esc(string s) { return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;"); }

    // 테스트용: 도움말 본문 HTML (Md 미리보기 CSS 재사용 + 도움말 전용 CSS)
    public static string BuildHtml(bool dark)
    {
        string page = Md.PreviewDocument("", dark);
        var body = new StringBuilder();
        body.Append("<h1>마크다운 문법 참고</h1>");
        body.Append("<p>왼쪽은 입력할 내용, 오른쪽은 미리보기에 표시되는 결과입니다. 예시를 복사해서 편집창에 붙여넣어 보세요.</p>");
        body.Append("<div class=\"toc\"><b>목차</b> ");
        for (int i = 0; i < Topics.Length; i++)
            body.Append(TocLink("t-" + Topics[i].Id, Topics[i].Title) + (i < Topics.Length - 1 ? " · " : ""));
        body.Append(" · " + TocLink("t-keys", "단축키") + "</div>");

        foreach (var t in Topics)
        {
            body.Append("<h2 id=\"t-" + t.Id + "\">" + Esc(t.Title) + "</h2>");
            body.Append("<p>" + Esc(t.Desc) + "</p>");
            body.Append("<table class=\"ex\"><tr><th>입력</th>" + (t.NoRender ? "" : "<th>결과</th>") + "</tr><tr>");
            body.Append("<td class=\"src\"><pre><code>" + Esc(t.Example) + "</code></pre></td>");
            if (!t.NoRender) body.Append("<td class=\"res\">" + Md.ToHtml(t.Example) + "</td>");
            body.Append("</tr></table>");
        }

        body.Append("<h2 id=\"t-keys\">단축키</h2><table><tr><th>키</th><th>동작</th></tr>");
        // generated from Shortcuts.All so help, tooltips and key bindings can never drift apart
        foreach (var k in Shortcuts.All)
            body.Append("<tr><td><kbd>" + Esc(k.Display(" + ")) + "</kbd></td><td>" + Esc(k.Desc + (k.EditorOnly && !k.Builtin ? " (편집창에서)" : "")) + "</td></tr>");
        foreach (var k in Shortcuts.BuiltinRows) body.Append("<tr><td><kbd>" + Esc(k[0]) + "</kbd></td><td>" + Esc(k[1]) + "</td></tr>");
        body.Append("</table>");

        string css = "<style>.toc{margin:12px 0 4px;line-height:2;}.toc a{text-decoration:none;}table.ex{table-layout:fixed;}"
                   + "table.ex td{vertical-align:top;}table.ex td.src pre{margin:0;white-space:pre-wrap;word-wrap:break-word;}"
                   + "table.ex td.res table{margin:.3em 0;}kbd{border:1px solid #888;border-radius:3px;padding:0 5px;font-family:Consolas,monospace;}</style>";
        int h = page.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        if (h >= 0) page = page.Insert(h, css);
        int b = page.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        if (b >= 0) return page.Insert(b, body.ToString());
        return page + body;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);

    // 개발용: `MdEditor.exe --help-shot <png> [dark]` — 도움말 창만 PrintWindow로 저장하고 종료 (화면 캡처 아님)
    public static int ShotAndExit(string png, bool dark)
    {
        var app = new Application();
        Theme.Apply(dark);
        var w = new HelpWindow(null, dark);
        w.WindowStartupLocation = WindowStartupLocation.Manual; w.Left = 40; w.Top = 40;
        var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        t.Tick += (s, e) =>
        {
            t.Stop();
            try
            {
                var h = new System.Windows.Interop.WindowInteropHelper(w).Handle;
                using (var bmp = new System.Drawing.Bitmap((int)w.ActualWidth, (int)w.ActualHeight))
                using (var g = System.Drawing.Graphics.FromImage(bmp))
                {
                    var dc = g.GetHdc(); PrintWindow(h, dc, 2); g.ReleaseHdc(dc);
                    bmp.Save(png, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            catch (Exception ex) { Log.Error("help-shot", ex); }
            app.Shutdown();
        };
        t.Start();
        return app.Run(w);
    }
}
