"""Fake executables for the tests of the PowerShell hooks and tools: a program the script under test finds on PATH,
for Windows (<name>.cmd) and POSIX (<name> with a shebang). Not a test module: unittest discover loads test*.py only.
"""
import sys


def install(bin_dir, name, script=None, target=None):
    """<name> in bin_dir for Windows (<name>.cmd) and POSIX: a fake run by this Python, or a wrapper of target."""
    bin_dir.mkdir(parents=True, exist_ok=True)
    if script is not None:
        (bin_dir / f'fake_{name}.py').write_text(script, encoding='utf-8')
        windows, posix = f'"{sys.executable}" "%~dp0fake_{name}.py"', f'"{sys.executable}" "$(dirname "$0")/fake_{name}.py"'
    else:
        windows = posix = f'"{target}"'
    (bin_dir / f'{name}.cmd').write_text(f'@{windows} %*\r\n', encoding='utf-8')
    (bin_dir / name).write_text(f'#!/bin/sh\nexec {posix} "$@"\n', encoding='utf-8')
    (bin_dir / name).chmod(0o755)
