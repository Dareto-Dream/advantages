"""Compile every project script against Unity's assemblies without opening the Editor.

Reads the references out of the .csproj files Unity generates, writes a throwaway SDK-style
project that globs Assets/Scripts and Assets/Editor, and builds it. Reports compiler errors only.

Usage:  python Tools/typecheck.py
"""

import os
import re
import subprocess
import sys
import tempfile

# Unity's BCL facades collide with the .NET SDK's own reference assemblies; let the SDK win.
SKIP_DIRS = ("netstandard", "monobleedingedge", "\\data\\mono", "unityreferenceassemblies")
CSPROJS = ("Assembly-CSharp.csproj", "Assembly-CSharp-Editor.csproj")
SOURCE_GLOBS = ("Assets\\Scripts\\**\\*.cs", "Assets\\Editor\\**\\*.cs")


def collect(project_dir):
    references = {}
    defines = ""

    for name in CSPROJS:
        path = os.path.join(project_dir, name)
        if not os.path.exists(path):
            continue

        text = open(path, encoding="utf-8").read()

        for match in re.finditer(r"<HintPath>(.*?)</HintPath>", text):
            hint = match.group(1)
            if not os.path.isabs(hint):
                hint = os.path.normpath(os.path.join(project_dir, hint))

            if any(part in hint.lower() for part in SKIP_DIRS) or not os.path.exists(hint):
                continue

            references[os.path.splitext(os.path.basename(hint))[0]] = hint

        if not defines:
            found = re.search(r"<DefineConstants>(.*?)</DefineConstants>", text)
            if found:
                defines = found.group(1)

    return references, defines


def write_project(work_dir, project_dir, references, defines):
    items = "\n".join(
        f'    <Reference Include="{name}"><HintPath>{path}</HintPath><Private>false</Private></Reference>'
        for name, path in sorted(references.items())
    )
    sources = "\n".join(
        f'    <Compile Include="{os.path.join(project_dir, glob)}" />' for glob in SOURCE_GLOBS
    )

    body = f"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <AssemblyName>AdvantageTypecheck</AssemblyName>
    <DefineConstants>{defines}</DefineConstants>
    <NoWarn>CS0169;CS0414;CS0649;CS0108;CS0618;CS0672;CS0067;CS0436</NoWarn>
    <Nullable>disable</Nullable>
    <ProduceReferenceAssembly>false</ProduceReferenceAssembly>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
  </PropertyGroup>
  <ItemGroup>
{sources}
  </ItemGroup>
  <ItemGroup>
{items}
  </ItemGroup>
</Project>
"""

    path = os.path.join(work_dir, "typecheck.csproj")
    open(path, "w", encoding="utf-8").write(body)
    return path


def main():
    project_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
    references, defines = collect(project_dir)

    if not references:
        print("No .csproj references found. Open the project in Unity once so it generates them.")
        return 1

    with tempfile.TemporaryDirectory() as work_dir:
        csproj = write_project(work_dir, project_dir, references, defines)
        result = subprocess.run(
            ["dotnet", "build", csproj, "-nologo", "-v", "q"],
            capture_output=True,
            text=True,
        )

    errors = sorted({
        re.sub(r" \[[A-Za-z]:.*", "", line).strip()
        for line in result.stdout.splitlines()
        if "error CS" in line
    })

    if errors:
        print("\n".join(errors))
        print(f"\n{len(errors)} error(s).")
        return 1

    print(f"Build succeeded against {len(references)} Unity assemblies.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
