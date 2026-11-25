# md-editor — 로컬 마크다운 에디터 (WPF)

Windows용 **오프라인 마크다운 에디터**. 왼쪽에 쓰면 오른쪽 미리보기가 바로 갱신됩니다. exe 하나로 동작하고 설치와 인터넷이 필요 없습니다.
(A small offline Markdown editor for Windows: WPF, AvalonEdit + Markdig, single exe.)

## 특징

- **빠른 편집창** — 코드 에디터(AvalonEdit): 줄 번호, 현재 줄 강조, 마크다운 색 표시, 수 MB 문서도 빠른 입력·스크롤
- **실시간 미리보기** — Markdig 변환(표, 각주, 작업 목록), 위험한 링크 차단, 문서 안 HTML 태그는 글자 그대로 표시
- **워드식 단축키 약 40개** — Ctrl+B/I/K, Ctrl+Alt+1~6 제목, Ctrl+F/H/G 찾기·바꾸기·줄 이동 등 (F1 도움말에 전체 표)
- **옵션 창 (Ctrl+,)** — 테마, 보기 모드, 항상 위, 글꼴(설치된 모든 글꼴 목록), 글자 크기, 줄 번호, 줄바꿈, 들여쓰기, 자동 저장 간격. 바꾸는 즉시 적용
- **도움말 2종** — F1 마크다운 문법 참고(예시와 결과), Shift+F1 프로그램 정보·사용 안내·라이선스
- 라이트/다크 테마, 선 아이콘 도구 모음, 보기 전환(편집/분할/미리보기)
- **안전한 저장** — 임시 파일에 쓴 뒤 교체, 자동 임시 저장과 비정상 종료 후 복구
- HTML 내보내기, 인쇄/PDF, `.md` 더블클릭으로 열기(이미 실행 중이면 기존 창에서)

사용법: [사용설명서.md](사용설명서.md) · 변경 내역: [변경이력.md](변경이력.md) · 코드 구조: [구조.md](구조.md)

## 빌드

SDK, NuGet, MSBuild가 필요 없습니다. Visual Studio 2017의 Roslyn 컴파일러(`csc.exe`, C# 7.3)와 Windows에 포함된 WPF 어셈블리만 씁니다.

```bat
build.bat
```

결과물은 `out\MdEditor.exe` (환경 변수 `OUTDIR`로 출력 폴더 변경). `lib\*.dll`은 exe 안에 리소스로 내장되어 파일 하나로 배포됩니다.

## 테스트

```bat
out\MdEditor.exe --selftest <새로 만든 빈 폴더>
```

실제 `%AppData%`를 쓰지 않는 격리된 프로필에서 수백 건의 자체 검사를 실행하고 결과를 폴더 안 `result.txt`에 기록합니다.

## 설정·데이터 위치

`%AppData%\MdEditor\` — `settings.json`(테마, 창 크기), `view.json`(보기, 옵션), `recovery.md`(자동 복구본), `error.log`. 문서는 사용자가 정한 경로에만 저장되며 외부로 전송되지 않습니다.

## 라이선스

이 프로그램: [MIT License](LICENSE)

포함된 오픈소스:

| 이름 | 버전 | 라이선스 |
|---|---|---|
| [AvalonEdit](http://avalonedit.net/) | 6.3.0.90 | MIT |
| [Markdig](https://github.com/lunet-io/markdig) | 0.37.0 | BSD-2-Clause |
| System.Memory / Buffers / Numerics.Vectors / Runtime.CompilerServices.Unsafe | 4.6.x | MIT |
| [Lucide](https://lucide.dev) 아이콘 도형 참고 | — | ISC (Feather 부분 MIT) |

라이선스 전문은 프로그램의 Shift+F1 창에서 볼 수 있습니다.
