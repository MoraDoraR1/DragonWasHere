"""Unity를 열지 않고 C# 컴파일 오류를 빠르게 확인한다 (dotnet SDK 필요).
Unity가 만든 csproj(DWH.*.csproj)의 참조는 그대로 쓰고, 소스 목록만 폴더 전체로 바꾼 임시 csproj(_chk.*.csproj)를 만들어 빌드한다.
사용: python Tools/compile_check.py   (Unity가 csproj를 한 번 만들어 둔 뒤)"""
import re, subprocess, sys, os
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
projects = {
    "DWH.Runtime": r"Assets\_Project\Scripts\**\*.cs",
    "DWH.Editor": r"Assets\_Project\Editor\**\*.cs",
    "DWH.Tests.EditMode": r"Assets\_Project\Tests\EditMode\**\*.cs",
    "DWH.Tests.PlayMode": r"Assets\_Project\Tests\PlayMode\**\*.cs",
}
for name, glob in projects.items():
    src = os.path.join(root, name + ".csproj")
    text = open(src, encoding="utf-8").read()
    text = re.sub(r'\s*<Compile Include="[^"]*" />', "", text)
    text = text.replace("<ItemGroup>", f'<ItemGroup>\n    <Compile Include="{glob}" />', 1)
    for other in projects:
        text = text.replace(f'"{other}.csproj"', f'"_chk.{other}.csproj"')
    open(os.path.join(root, f"_chk.{name}.csproj"), "w", encoding="utf-8").write(text)
ok = True
for name in projects:
    r = subprocess.run(["dotnet", "build", f"_chk.{name}.csproj", "-nologo", "-v", "q", "-clp:NoSummary"], cwd=root, capture_output=True, text=True, encoding="utf-8", errors="replace")
    errors = sorted({l.strip() for l in r.stdout.splitlines() if ": error " in l})
    print(f"== {name}: {'OK' if r.returncode == 0 else f'{len(errors)} errors'}")
    for e in errors[:60]: print(re.sub(r"^.*?Assets", "Assets", e).split(" [")[0])
    if r.returncode != 0: ok = False; break
sys.exit(0 if ok else 1)
