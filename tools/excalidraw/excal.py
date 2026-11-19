"""Python port of excal.ps1 (same output format) for machines without PowerShell.

Usage:
  python3 tools/excalidraw/excal.py <layout.py> <out.excalidraw>

A layout file is plain Python that calls Box, Arrow, Note and Frame (all injected).
"""
import json
import math
import sys

els = []
boxes = {}
_seed = [1000]

EDGE, SVC, DATA, ASYNC, GREY = '#ffc9c9', '#a5d8ff', '#b2f2bb', '#ffec99', '#e9ecef'
W, R, RED, NOTE = '#1971c2', '#2f9e44', '#e03131', '#495057'


def _next_seed():
    _seed[0] += 7
    return _seed[0]


def _base(id, type, x, y, w, h):
    return {
        'id': id, 'type': type, 'x': float(x), 'y': float(y), 'width': float(w), 'height': float(h), 'angle': 0,
        'strokeColor': '#1e1e1e', 'backgroundColor': 'transparent', 'fillStyle': 'solid', 'strokeWidth': 2,
        'strokeStyle': 'solid', 'roughness': 1, 'opacity': 100, 'groupIds': [], 'frameId': None, 'roundness': None,
        'seed': _next_seed(), 'version': 1, 'versionNonce': _next_seed(), 'isDeleted': False, 'boundElements': [],
        'updated': 1700000000000, 'link': None, 'locked': False,
    }


def _text(id, x, y, w, h, text, container, size):
    t = _base(id, 'text', x, y, w, h)
    t['boundElements'] = None
    t.update(text=text, fontSize=size, fontFamily=1, textAlign='center', verticalAlign='middle',
             containerId=container, originalText=text, lineHeight=1.25, autoResize=True)
    if not container:
        t['textAlign'], t['verticalAlign'] = 'left', 'top'
    return t


def Box(id, x, y, w, h, text, bg):
    r = _base(id, 'rectangle', x, y, w, h)
    r['backgroundColor'] = bg
    r['roundness'] = {'type': 3}
    th = 20 * len(text.split('\n'))
    t = _text(f'{id}-t', x + 5, y + (h - th) / 2, w - 10, th, text, id, 16)
    r['boundElements'].append({'type': 'text', 'id': f'{id}-t'})
    els.extend([r, t])
    boxes[id] = r
    return r


def _edge(b, tx, ty):
    cx, cy = b['x'] + b['width'] / 2, b['y'] + b['height'] / 2
    dx, dy = tx - cx, ty - cy
    if dx == 0 and dy == 0:
        return cx, cy
    sx = (b['width'] / 2) / abs(dx) if dx else math.inf
    sy = (b['height'] / 2) / abs(dy) if dy else math.inf
    s = min(sx, sy)
    return cx + dx * s, cy + dy * s


def Arrow(id, frm, to, label='', color=None, dashed=False):
    a, b = boxes[frm], boxes[to]
    acx, acy = a['x'] + a['width'] / 2, a['y'] + a['height'] / 2
    bcx, bcy = b['x'] + b['width'] / 2, b['y'] + b['height'] / 2
    x1, y1 = _edge(a, bcx, bcy)
    x2, y2 = _edge(b, acx, acy)
    ar = _base(id, 'arrow', x1, y1, abs(x2 - x1), abs(y2 - y1))
    ar['roundness'] = {'type': 2}
    if color:
        ar['strokeColor'] = color
    if dashed:
        ar['strokeStyle'] = 'dashed'
    ar.update(points=[[0, 0], [x2 - x1, y2 - y1]], lastCommittedPoint=None,
              startBinding={'elementId': frm, 'focus': 0, 'gap': 4},
              endBinding={'elementId': to, 'focus': 0, 'gap': 4},
              startArrowhead=None, endArrowhead='arrow', elbowed=False)
    a['boundElements'].append({'type': 'arrow', 'id': id})
    b['boundElements'].append({'type': 'arrow', 'id': id})
    els.append(ar)
    if label:
        lines = label.split('\n')
        w = 8 * max(len(l) for l in lines)
        h = 17 * len(lines)
        t = _text(f'{id}-t', (x1 + x2) / 2 - w / 2, (y1 + y2) / 2 - h / 2, w, h, label, id, 14)
        if color:
            t['strokeColor'] = color
        ar['boundElements'].append({'type': 'text', 'id': f'{id}-t'})
        els.append(t)


def Note(id, x, y, text, size, color=None):
    lines = text.split('\n')
    w = round(size * 0.55 * max(len(l) for l in lines))
    t = _text(id, x, y, w, round(size * 1.25 * len(lines)), text, None, size)
    if color:
        t['strokeColor'] = color
    els.append(t)


def Frame(id, x, y, w, h):
    f = _base(id, 'rectangle', x, y, w, h)
    f.update(strokeStyle='dashed', strokeColor='#868e96', strokeWidth=1, roundness={'type': 3})
    els.append(f)


def Col(n):
    return 40 + 290 * n


if __name__ == '__main__':
    layout, out = sys.argv[1], sys.argv[2]
    ns = {k: v for k, v in globals().items() if not k.startswith('__')}
    exec(open(layout, encoding='utf-8').read(), ns)
    doc = {'type': 'excalidraw', 'version': 2, 'source': 'https://excalidraw.com', 'elements': els,
           'appState': {'gridSize': 20, 'viewBackgroundColor': '#ffffff'}, 'files': {}}
    with open(out, 'w', encoding='utf-8') as f:
        json.dump(doc, f, indent=2, ensure_ascii=False)
    print(f'wrote {len(els)} elements to {out}')
