# SPDX-FileCopyrightText: 2026 Amin Khosrozadeh
# SPDX-License-Identifier: GPL-3.0-or-later
"""Synthetic process-lifetime fixture; never processes microscope data."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time

if len(sys.argv) > 1 and sys.argv[1] == '--child':
    root = Path(sys.argv[2])
    (root / 'pid-child.txt').write_text(str(os.getpid()))
    time.sleep(20)
    sys.exit(0)

parser = argparse.ArgumentParser()
for name in ('input', 'output', 'mode', 'level'):
    parser.add_argument('--' + name)
args = parser.parse_args()
root = Path(args.input).parent
scenario = (Path(__file__).parent / 'scenario.txt').read_text()
(root / 'pid-worker.txt').write_text(str(os.getpid()))

if scenario == 'child':
    subprocess.Popen([sys.executable, '-B', __file__, '--child', str(root)],
                     stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                     creationflags=subprocess.CREATE_NO_WINDOW)
    deadline = time.monotonic() + 5
    while not (root / 'pid-child.txt').exists() and time.monotonic() < deadline:
        time.sleep(.01)

if scenario in ('locked_error', 'locked_success'):
    locked = Path(os.environ['CZISHRINK_WORK_DIRECTORY']) / 'owned-lock.bin'
    locked.write_bytes(b'owned synthetic scratch')
    (root / 'lock-path.txt').write_text(str(locked))

if scenario == 'malformed':
    print(json.dumps({'progress': 'invalid progress value'}), flush=True)
else:
    print(json.dumps({'progress': 10}), flush=True)

if scenario in ('callback', 'callback_json', 'child', 'malformed', 'cancel'):
    time.sleep(20)
    Path(args.output).write_bytes(b'late output must never appear')
elif scenario == 'locked_error':
    print(json.dumps({'error': 'Original synthetic codec failure'}), flush=True)
    sys.exit(1)
else:
    shutil.copyfile(args.input, args.output)
    first = hashlib.sha256(Path(args.input).read_bytes()).hexdigest()
    second = hashlib.sha256(Path(args.output).read_bytes()).hexdigest()
    assert first == second
    print(json.dumps({'result': {'unchanged': True, 'reason': 'Copied synthetic fixture',
                                'input_sha256': first, 'output_sha256': second,
                                'container_byte_identical': True}}), flush=True)
