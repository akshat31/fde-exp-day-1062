@echo off
REM setup.bat — one-time participant setup for the FDE-Event-Final starter.
REM
REM Version 3 — PREPARE-ONLY, branch-based delivery. setup.bat does NOT push
REM and does NOT open a Pull Request. Those two steps are the participant's
REM job (until the manager says otherwise). This script:
REM   1. Initializes a local git repo and sets the unborn branch to the
REM      feature branch %BRANCH% (main is left for the remote/PR merge).
REM   2. Extracts the starter pack .zip (any name — auto-detected) into the
REM      working tree of that branch.
REM   3. Commits "starter pack - clean baseline" on %BRANCH%.
REM   4. Provisions Secrets and Variables from fde-credentials.env IF this
REM      repo already has an origin remote; otherwise prints the command to
REM      run provisioning after the participant pushes.
REM   5. Restores the solution at the repo root (fde-starter.slnx).
REM
REM The participant then does, in their own terminal:
REM   git remote add origin <repo-url>      (only if no origin yet)
REM   git push -u origin %BRANCH%
REM   gh pr create --base main --head %BRANCH% --title "Starter baseline" ...
REM
REM Assumes: git + gh CLI installed and authenticated (or GITHUB_TOKEN set);
REM the starter .zip, this script, and fde-credentials.env all sit together.

set BRANCH=feature/starter-baseline

REM ---- 1. Robust zip detection: pick the single *.zip in this folder, any name.
set "ZIPFILE="
for /f "delims=" %%f in ('dir /b *.zip 2^>nul') do set "ZIPFILE=%%~ff"
if not defined ZIPFILE (
    echo ERROR: no .zip found in this folder.
    echo Save the starter pack as a .zip here first, then re-run setup.bat.
    pause
    exit /b 1
)

if not exist "fde-credentials.env" (
    echo ERROR: fde-credentials.env not found in this folder.
    echo This file is distributed separately via MS Teams - it is NOT part of the zip.
    echo Copy it in now, then re-run setup.bat.
    pause
    exit /b 1
)

REM ---- 2. Init local repo on the feature branch (safe inside an existing clone too)
echo == Initializing local repo (branch: %BRANCH%) ==
git init -q
git symbolic-ref HEAD refs/heads/%BRANCH%
if errorlevel 1 (
    echo ERROR: could not point HEAD at %BRANCH%
    pause
    exit /b 1
)

REM ---- 3. Extract the starter pack into the working tree ----
echo == Extracting %ZIPFILE% into %BRANCH% ==
powershell -NoProfile -Command "$z=(Resolve-Path -LiteralPath '%ZIPFILE%').Path; $tmp=Join-Path (Get-Location) '__unzip__'; if(Test-Path $tmp){Remove-Item $tmp -Recurse -Force}; Expand-Archive -Path $z -DestinationPath $tmp; $hasFiles=@(Get-ChildItem -LiteralPath $tmp -File).Count; $dirs=@(Get-ChildItem -LiteralPath $tmp -Directory); $src=if($hasFiles -eq 0 -and $dirs.Count -eq 1){$dirs[0].FullName}else{$tmp}; Copy-Item -Path (Join-Path $src '*') -Destination (Get-Location).Path -Recurse -Force; Remove-Item $tmp -Recurse -Force"

REM ---- 4. Commit the starter pack on the feature branch ----
git add -A
git commit -q -m "starter pack - clean baseline (%BRANCH%)"
if errorlevel 1 (
    echo ERROR: commit failed.
    pause
    exit /b 1
)

REM ---- 5. Provision Secrets and Variables (only if origin already exists) ----
for /f "delims=" %%r in ('git remote get-url origin 2^>nul') do set "REMOTE=%%r"
if defined REMOTE (
    echo == Provisioning Secrets and Variables ==
    powershell -ExecutionPolicy Bypass -File provision-credentials.ps1
) else (
    echo NOTE: no origin remote configured yet. After you push the branch, run:
    echo     powershell -ExecutionPolicy Bypass -File provision-credentials.ps1
)

REM ---- 6. Restore the solution ----
dotnet restore

echo.
echo ============================================================
echo Starter pack is committed on branch:  %BRANCH%
echo.
echo PUSH and PR are YOUR steps (setup.bat does not do them):
echo.
echo   * if your repo has no origin remote set here:
echo       git remote add origin <your-repo-url>
echo.
echo   * push the branch:
echo       git push -u origin %BRANCH%
echo.
echo   * open a Pull Request from %BRANCH% to the default branch (main):
echo       gh pr create --base main --head %BRANCH% ^
echo          --title "Starter baseline" --body "FDE-Event-Final starter baseline"
echo     (or create it in the web UI). Merging the PR triggers cd.yml.
echo.
echo   * if provisioning was skipped above, run it after the push:
echo       powershell -ExecutionPolicy Bypass -File provision-credentials.ps1
echo ============================================================
pause