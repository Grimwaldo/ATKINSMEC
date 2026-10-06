# ATKINSMEC — installing

Five Revit add-ins in one package: **AUTOVENT**, **CHW VOLUME**,
**DUCT INSULATION**, **SPRINKLERS** and **ALIGN TERMINALS**. They appear
together on the **ATKINS → Mechanical** ribbon panel.

## Installing

1. **Close Revit.**
2. Run **`ATKINSMEC-Setup.exe`**.
3. Tick the Revit versions you want and press **Install**.

One file, nothing to unzip. **No administrator rights are needed.**

Versions of Revit that are not on the computer are shown unticked.

## Why no admin rights

Revit reads add-ins from two places:

| Folder | Applies to | Admin needed |
|---|---|---|
| `%AppData%\Autodesk\Revit\Addins\<version>\` | the current user | **no** |
| `%ProgramData%\Autodesk\Revit\Addins\<version>\` | everyone on the PC | yes |

This installer uses the first — your own Windows profile. Nothing is
written to `Program Files`, no registry keys are set, no service is
touched, so it runs under a standard locked-down account.

The installer also carries a manifest asking Windows for *asInvoker*
rights. Without it, Windows would pop a UAC prompt purely because the
filename contains the word "setup" — it guesses that such programs need
elevating. This one does not.

If you need it for **every user** of a shared machine, that does need
administrator rights; see *Installing for all users* below.

## Close Revit first

Windows will not let a DLL be replaced while Revit has it open. The
installer checks and refuses rather than half-installing — an install
that appears to work while Revit keeps running the old version is worse
than one that stops and says so.

## Uninstalling

Run `ATKINSMEC-Setup.exe` again and press **Uninstall**.

Only files this installer wrote are removed. The add-ins folder is shared
with other tools such as RevitLookup and the Add-In Manager, so the
uninstall reads `ATKINSMEC.files.txt` — the record written at install
time — instead of clearing the folder.

It then asks whether to remove your settings as well: the saved ACC model
list and the duct insulation rules, in `%AppData%\ATKINSMEC\`. Answer
**No** to keep them for a later reinstall.

## Rolling it out to a team

```
ATKINSMEC-Setup.exe /silent
ATKINSMEC-Setup.exe /silent /uninstall
```

No window, exit code 0 on success. It returns 2 if Revit is running and 3
if no Revit installation was found, so a deployment script can tell the
difference.

## If IT blocks unsigned executables

`ATKINSMEC-Setup.zip` holds exactly the same files as a plain script.
Unzip it and run `Install.cmd` — same result, no executable involved.
`Uninstall.cmd` reverses it.

## Installing for all users (needs admin)

Copy the contents of `payload\<version>\` from the zip into

```
C:\ProgramData\Autodesk\Revit\Addins\<version>\
```

from an administrator command prompt. Revit loads add-ins from there for
every user of the machine. The uninstaller does not manage that location.

---

## Building the package (for whoever maintains this)

From the repository root, with Revit closed:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build-installer.ps1
```

That builds all six Revit versions in Release, stages them into
`installer\payload\`, packs them into `installer\exe\payload.zip`, and
writes both

- `installer\output\ATKINSMEC-Setup.exe` — one self-contained file
- `installer\output\ATKINSMEC-Setup.zip` — the script version

`-SkipBuild` packs whatever is already in `bin\Release` instead.

### How the exe carries the add-in

`installer\exe\` is a small .NET Framework 4.8 WinForms project.
**net48 is deliberate**: it ships with every Windows 10 and 11, so the
result runs anywhere with no runtime to install first. The add-in files
are embedded as a single compressed zip resource — stored file by file
instead, the same DLLs repeated across six versions came to 32 MB against
13 MB compressed.

`app.manifest` is what keeps it from asking for administrator rights.
Do not remove it.

### An Inno Setup version

`ATKINSMEC.iss` would produce a wizard-style installer with an entry in
*Apps & features*. It needs [Inno Setup 6](https://jrsoftware.org/isdl.php)
on the build machine, and `build-installer.ps1` picks it up if present.

It is not needed for a single exe — `ATKINSMEC-Setup.exe` already is one
— and **it has never been compiled**, since Inno Setup is not installed
on the machine it was written on.
