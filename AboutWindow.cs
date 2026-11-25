using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;

// 프로그램 정보 창 (Shift+F1): 사용 안내 + 사용한 기술 + 오픈소스 라이선스. 문법 도움말(F1)과는 별개.
class AboutWindow : Window
{
    readonly WebBrowser browser = new WebBrowser();

    public AboutWindow(Window owner, bool dark)
    {
        Owner = owner;
        Title = "프로그램 정보 · 사용 안내 · 라이선스";
        Icon = AppInfo.LoadIcon();
        Width = 860; Height = 740; MinWidth = 560; MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Theme.Bind(this, BackgroundProperty, "Bg");
        Content = browser;
        browser.Navigating += (s, e) => { if (e.Uri != null && e.Uri.Scheme != "about") e.Cancel = true; };   // 문서 안 #목차 이동만 허용
        browser.LoadCompleted += (s, e) => Silence();
        browser.Navigated += (s, e) => Silence();
        PreviewKeyDown += (s, e) => { if (e.Key == System.Windows.Input.Key.Escape) Close(); };
        browser.NavigateToString(BuildHtml(dark));
    }

    void Silence()   // 스크립트 오류 대화상자 억제
    {
        try
        {
            var f = typeof(WebBrowser).GetField("_axIWebBrowser2", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (f != null) { dynamic ax = f.GetValue(browser); if (ax != null) ax.Silent = true; }
        }
        catch { }
    }

    static string Esc(string s) { return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;"); }

    // (이름, 버전, 라이선스, 저작권, 용도, 주소)
    static readonly string[][] Libs =
    {
        new[] { "AvalonEdit", "6.3.0.90", "MIT", "© 2000-2023 AlphaSierraPapa for the SharpDevelop Team", "편집창 (줄 번호, 큰 문서 처리, 색 표시, 찾기)", "http://www.avalonedit.net/" },
        new[] { "Markdig", "0.37.0", "BSD-2-Clause", "© 2016-2026, Alexandre Mutel. All rights reserved.", "마크다운 → HTML 변환 (미리보기, 내보내기)", "https://github.com/xoofx/markdig" },
        new[] { "System.Memory", "4.6.31308", "MIT", "© Microsoft Corporation / .NET Foundation", "AvalonEdit·Markdig가 쓰는 .NET 보조 라이브러리", "https://github.com/dotnet/corefx" },
        new[] { "System.Buffers", "4.6.28619", "MIT", "© Microsoft Corporation / .NET Foundation", "위와 같음", "https://github.com/dotnet/corefx" },
        new[] { "System.Numerics.Vectors", "4.6.26515", "MIT", "© Microsoft Corporation / .NET Foundation", "위와 같음", "https://github.com/dotnet/corefx" },
        new[] { "System.Runtime.CompilerServices.Unsafe", "4.6.28619", "MIT", "© Microsoft Corporation / .NET Foundation", "위와 같음", "https://github.com/dotnet/corefx" },
    };

    const string MitText =
        "Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the \"Software\"), " +
        "to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, " +
        "and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:\n\n" +
        "The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.\n\n" +
        "THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, " +
        "FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER " +
        "LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.";

    const string BsdText =
        "Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:\n\n" +
        "1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.\n\n" +
        "2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation " +
        "and/or other materials provided with the distribution.\n\n" +
        "THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS \"AS IS\" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, " +
        "THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE " +
        "LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE " +
        "GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT " +
        "LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.";

    const string IscText =
        "Permission to use, copy, modify, and/or distribute this software for any purpose with or without fee is hereby granted, provided that the above " +
        "copyright notice and this permission notice appear in all copies.\n\n" +
        "THE SOFTWARE IS PROVIDED \"AS IS\" AND THE AUTHOR DISCLAIMS ALL WARRANTIES WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF " +
        "MERCHANTABILITY AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES " +
        "WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF OR IN " +
        "CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.";

    static string Section(string id, string title) { return "<h2 id=\"a-" + id + "\">" + Esc(title) + "</h2>"; }
    static string P(string s) { return "<p>" + Esc(s) + "</p>"; }
    static string Ul(params string[] items)
    {
        var sb = new StringBuilder("<ul>");
        foreach (var i in items) sb.Append("<li>").Append(Esc(i)).Append("</li>");
        return sb.Append("</ul>").ToString();
    }
    static string Pre(string s) { return "<pre><code>" + Esc(s) + "</code></pre>"; }

    // 테스트용: 본문 HTML (Md 미리보기 CSS 재사용)
    public static string BuildHtml(bool dark)
    {
        string page = Md.PreviewDocument("", dark);
        var b = new StringBuilder();
        b.Append("<h1>" + Esc(AppInfo.Title) + "</h1>");
        b.Append(P("버전 " + AppInfo.Version + " · 내 컴퓨터에서만 동작하는 오프라인 마크다운 에디터 (exe 한 개, 설치·인터넷 불필요)"));
        b.Append("<div class=\"toc\"><b>목차</b> " + HelpWindow.TocLink("a-use", "사용 안내") + " · " + HelpWindow.TocLink("a-data", "저장 위치·보안") + " · "
               + HelpWindow.TocLink("a-tech", "사용한 기술") + " · " + HelpWindow.TocLink("a-lic", "오픈소스 라이선스") + " · " + HelpWindow.TocLink("a-text", "라이선스 전문") + "</div>");

        b.Append(Section("use", "사용 안내"));
        b.Append(Ul(
            "화면: 위쪽 도구 모음(서식·표·찾기, 오른쪽 끝에 보기 전환·항상 위·테마·도움말·정보·옵션·내보내기·인쇄), 가운데 편집창과 미리보기, 아래쪽 상태바(줄·열 위치, 글자·단어 수, 파일 크기, 글자 크기, 보기 모드).",
            "편집: 왼쪽에 마크다운을 입력하면 오른쪽 미리보기가 바로 갱신됩니다. Ctrl+마우스 휠로 글자 크기를 바꾸고 Ctrl+0으로 되돌립니다.",
            "도움말: F1 = 마크다운 문법과 전체 단축키 표, Shift+F1 = 이 창, Ctrl+, = 옵션(글꼴·줄 번호·줄바꿈·들여쓰기·자동 저장 간격 등).",
            "단축키는 워드와 같은 방식입니다: Ctrl+B 굵게, Ctrl+I 기울임, Ctrl+K 링크, Ctrl+Alt+1~6 제목, Ctrl+Shift+L 글머리 목록, Ctrl+Alt+T 표, Ctrl+F 찾기, Ctrl+H 바꾸기, Ctrl+G 줄 이동.",
            "파일: Ctrl+O 열기, Ctrl+S 저장, F12 다른 이름으로 저장, .md 파일을 더블클릭하면 이미 실행 중인 창에서 열립니다. 저장은 임시 파일을 거쳐 교체하므로 도중에 실패해도 원본이 잘리지 않습니다 (UTF-8).",
            "자동 복구: 수정 중인 글은 일정 간격(기본 20초)마다 복구 파일에 보관되고, 비정상 종료 후 다시 열면 복구할지 묻습니다. 저장하거나 정상 종료하면 지워집니다.",
            "내보내기·인쇄: HTML로 내보내기, Ctrl+P 인쇄 창에서 \"Microsoft Print to PDF\"를 고르면 PDF로 저장됩니다."));

        b.Append(Section("data", "저장 위치와 보안"));
        b.Append(Ul(
            "문서는 사용자가 정한 경로에만 저장되며 외부로 전송되지 않습니다. 프로그램은 인터넷에 접속하지 않습니다.",
            "설정과 로그는 %AppData%\\MdEditor 폴더에 있습니다 (settings.json = 테마·창 크기, view.json = 보기·옵션, recovery.md = 자동 복구본, error.log = 진단 로그). 옵션 창의 \"설정 폴더 열기\"로 바로 열 수 있습니다.",
            "보안을 위해 문서 안의 HTML 태그는 해석하지 않고 글자 그대로 보여 주며, javascript: 같은 위험한 링크는 차단합니다.",
            "미리보기는 Windows에 내장된 웹 브라우저 컨트롤(IE 엔진)로 그려서 다른 브라우저와 출력이 조금 다를 수 있습니다."));

        b.Append(Section("tech", "사용한 기술"));
        b.Append("<table><tr><th>구분</th><th>내용</th></tr>"
               + "<tr><td>실행 환경</td><td>Windows 데스크톱, .NET Framework 4 (WPF) — Windows에 포함되어 있어 별도 설치가 필요 없습니다</td></tr>"
               + "<tr><td>언어·빌드</td><td>C# 7.3, Visual Studio 2017의 Roslyn 컴파일러(csc)로 코드만으로 빌드 (SDK·NuGet·XAML 파일 없음). 라이브러리는 exe 안에 내장되어 파일 하나로 배포됩니다</td></tr>"
               + "<tr><td>편집창</td><td>AvalonEdit (코드 에디터 컨트롤)</td></tr>"
               + "<tr><td>마크다운 변환</td><td>Markdig (CommonMark 호환, 표·각주·작업 목록 확장) + 자체 보안 필터</td></tr>"
               + "<tr><td>미리보기</td><td>WPF WebBrowser 컨트롤, 백그라운드 스레드에서 변환</td></tr>"
               + "<tr><td>화면 디자인</td><td>코드로 만든 라이트/다크 색 토큰, 선 아이콘(Lucide 계열 모양을 벡터로 그림)</td></tr>"
               + "<tr><td>글꼴</td><td>편집창 기본 Cascadia Mono → Consolas → 맑은 고딕 순으로 사용 (Windows에 설치된 글꼴을 사용하며 프로그램에 포함하지 않음)</td></tr>"
               + "<tr><td>검증</td><td>내장 자체 테스트(<code>--selftest</code>) 수백 건</td></tr></table>");

        b.Append(Section("lic", "오픈소스 라이선스"));
        b.Append(P("이 프로그램은 아래 오픈소스를 포함합니다. 각 라이선스에 따라 저작권 표시와 라이선스 전문을 함께 밝힙니다."));
        b.Append("<table><tr><th>이름</th><th>버전</th><th>라이선스</th><th>저작권</th><th>용도</th></tr>");
        foreach (var l in Libs)
            b.Append("<tr><td><b>" + Esc(l[0]) + "</b><br><small>" + Esc(l[5]) + "</small></td><td>" + Esc(l[1]) + "</td><td>" + Esc(l[2]) + "</td><td>" + Esc(l[3]) + "</td><td>" + Esc(l[4]) + "</td></tr>");
        b.Append("<tr><td><b>Lucide / Feather 아이콘 도형</b><br><small>https://lucide.dev</small></td><td>—</td><td>ISC (Feather 부분은 MIT)</td><td>© Lucide Contributors, © Cole Bemis (Feather)</td><td>도구 모음 선 아이콘의 모양 참고</td></tr></table>");

        b.Append(Section("text", "라이선스 전문"));
        b.Append("<h3>MIT License — AvalonEdit, System.* (.NET) 라이브러리</h3>");
        b.Append(P("Copyright (c) 2000-2023 AlphaSierraPapa for the SharpDevelop Team (AvalonEdit)"));
        b.Append(P("Copyright (c) Microsoft Corporation / .NET Foundation and Contributors (System.Memory, System.Buffers, System.Numerics.Vectors, System.Runtime.CompilerServices.Unsafe)"));
        b.Append(Pre(MitText));
        b.Append("<h3>BSD 2-Clause License — Markdig</h3>");
        b.Append(P("Copyright (c) 2016-2026, Alexandre Mutel"));
        b.Append(P("All rights reserved."));
        b.Append(Pre(BsdText));
        b.Append("<h3>ISC License — Lucide (아이콘 모양 참고)</h3>");
        b.Append(P("Copyright (c) Lucide Contributors. Portions copyright (c) Cole Bemis (Feather, MIT License)."));
        b.Append(Pre(IscText));

        string css = "<style>.toc{margin:12px 0 4px;line-height:2;}.toc a{text-decoration:none;}pre{white-space:pre-wrap;word-wrap:break-word;font-size:12px;}small{opacity:.7;}</style>";
        int h = page.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        if (h >= 0) page = page.Insert(h, css);
        int e = page.LastIndexOf("</body>", StringComparison.OrdinalIgnoreCase);
        if (e >= 0) return page.Insert(e, b.ToString());
        return page + b;
    }
}
