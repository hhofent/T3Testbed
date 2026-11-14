"""Small report writer shared by the deterministic river check scripts."""
from datetime import datetime, timezone
import json
import math
from pathlib import Path
import re
import struct
import zlib

ROOT = Path(__file__).resolve().parents[1]


def _chunk(tag, data):
    body = tag + data
    return struct.pack('!I', len(data)) + body + struct.pack('!I', zlib.crc32(body) & 0xffffffff)


def _png(path, failure):
    size = 240
    pixels = bytearray([255, 255, 255] * size * size)
    segments = failure.get('segments') or []
    points = [p for line in segments for p in line]
    if points:
        xs, ys = [p[0] for p in points], [p[1] for p in points]
        x0, x1 = min(xs), max(xs)
        y0, y1 = min(ys), max(ys)
        span = max(x1 - x0, y1 - y0, 1e-9)
        pad = span * .08
        x0 -= pad; x1 += pad; y0 -= pad; y1 += pad
    else:
        x0, y0, x1, y1 = -1, -1, 1, 1

    def xy(p):
        x = int(8 + (p[0] - x0) / max(1e-9, x1 - x0) * (size - 16))
        y = int(size - 8 - (p[1] - y0) / max(1e-9, y1 - y0) * (size - 16))
        return max(0, min(size - 1, x)), max(0, min(size - 1, y))

    def line(a, b, color):
        x0i, y0i = xy(a); x1i, y1i = xy(b)
        dx, dy = abs(x1i - x0i), abs(y1i - y0i)
        sx = 1 if x0i < x1i else -1; sy = 1 if y0i < y1i else -1
        err = dx - dy
        while True:
            off = (y0i * size + x0i) * 3
            pixels[off:off + 3] = bytes(color)
            if x0i == x1i and y0i == y1i: break
            e2 = 2 * err
            if e2 > -dy: err -= dy; x0i += sx
            if e2 < dx: err += dx; y0i += sy

    for segment in segments:
        line(segment[0], segment[1], (40, 90, 160))
    if failure.get('coordinates') and not segments:
        cx = cy = size // 2
        for d in range(-12, 13):
            for x, y in ((cx + d, cy), (cx, cy + d)):
                off = (y * size + x) * 3
                pixels[off:off + 3] = bytes((210, 30, 40))
    raw = b''.join(b'\0' + pixels[y * size * 3:(y + 1) * size * 3] for y in range(size))
    header = struct.pack('!2I5B', size, size, 8, 2, 0, 0, 0)
    path.write_bytes(b'\x89PNG\r\n\x1a\n' + _chunk(b'IHDR', header) + _chunk(b'IDAT', zlib.compress(raw)) + _chunk(b'IEND', b''))


def write_report(results, metrics, failures):
    stamp = datetime.now(timezone.utc).strftime('%Y%m%d-%H%M%S-%f')
    out = ROOT / 'check-results' / stamp
    out.mkdir(parents=True, exist_ok=False)
    rows = ['# River check run', '', f'Run time: {stamp} UTC', '', '| Check | Result | Measured value | Target |', '|---|---:|---|---|']
    for item in results:
        rows.append(f"| {item['name']} | {'PASS' if item['passed'] else 'FAIL'} | {item.get('measured','')} | {item.get('target','')} |")
    rows.extend(['', '## Failures', ''])
    if not failures:
        rows.append('None.')
    for i, failure in enumerate(failures, 1):
        safe_check = re.sub(r'[^A-Za-z0-9._-]+', '-', str(failure.get('check', 'check'))).strip('-') or 'check'
        image_name = f"failure-{i:03d}-{safe_check[:72]}.png"
        _png(out / image_name, failure)
        rows.append(f"- **{failure.get('check')}** seed `{failure.get('seed')}`, coordinates `{failure.get('coordinates')}`, features `{failure.get('feature_ids', [])}`. {failure.get('message','')}")
        rows.append(f"  Crop: `{image_name}`")
    (out / 'summary.md').write_text('\n'.join(rows) + '\n', encoding='utf-8')
    (out / 'metrics.json').write_text(json.dumps({'results': results, 'metrics': metrics, 'failures': failures}, indent=2, sort_keys=True) + '\n', encoding='utf-8')
    return out
