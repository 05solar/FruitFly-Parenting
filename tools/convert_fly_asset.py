"""Convert AroniaStudios' CC BY 3.0 Fly into a body and two articulated wing OBJs.
Requires numpy. Preserves flat normals and material boundaries; retains the original GLB.
"""
import json
import struct
from pathlib import Path
import numpy as np

ROOT = Path('Assets/_Project/Art/ThirdParty/Fly')
raw = (ROOT / 'Fly.glb').read_bytes()
size = struct.unpack_from('<I', raw, 12)[0]
doc = json.loads(raw[20:20+size])
binary = raw[28+size:]


def accessor(index):
    a = doc['accessors'][index]
    v = doc['bufferViews'][a['bufferView']]
    dtype = np.dtype({5123: '<u2', 5125: '<u4', 5126: '<f4'}[a['componentType']])
    width = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3}[a['type']]
    return np.ndarray((a['count'], width), dtype=dtype, buffer=binary,
                      offset=v.get('byteOffset', 0)+a.get('byteOffset', 0),
                      strides=(v.get('byteStride', width*dtype.itemsize), dtype.itemsize)).copy()


def matrix(node):
    if 'matrix' in node:
        return np.array(node['matrix']).reshape(4, 4).T
    x, y, z, w = node.get('rotation', [0, 0, 0, 1])
    m = np.eye(4)
    m[:3, :3] = np.array([
        [1-2*y*y-2*z*z, 2*x*y-2*z*w, 2*x*z+2*y*w],
        [2*x*y+2*z*w, 1-2*x*x-2*z*z, 2*y*z-2*x*w],
        [2*x*z-2*y*w, 2*y*z+2*x*w, 1-2*x*x-2*y*y]
    ]) @ np.diag(node.get('scale', [1, 1, 1]))
    m[:3, 3] = node.get('translation', [0, 0, 0])
    return m


# Source faces +X; canonical OBJ faces +Z. Unity's OBJ importer mirrors X.
canonical = np.array([[0, 0, -1, 0], [0, 1, 0, 0], [1, 0, 0, 0], [0, 0, 0, 1]])
materials = ['Body', 'Dark', 'Eyes', 'Wings', 'WingHighlight', 'Wings', 'WingHighlight']
report = []


def convert(index, parent):
    node = doc['nodes'][index]
    world = parent @ matrix(node)
    if 'mesh' in node:
        mesh = node['mesh']
        name = 'Fly' if mesh == 0 else f'Wings_{mesh-1}'
        out = ['mtllib Fly.mtl', 'o '+name]
        offset = 1
        transform = canonical @ world
        for primitive in doc['meshes'][mesh]['primitives']:
            mat = materials[primitive['material']]
            p = accessor(primitive['attributes']['POSITION'])
            p = (transform @ np.column_stack([p, np.ones(len(p))]).T).T[:, :3]
            normals = accessor(primitive['attributes']['NORMAL']) @ np.linalg.inv(transform[:3, :3])
            normals /= np.maximum(np.linalg.norm(normals, axis=1, keepdims=True), 1e-9)
            faces = accessor(primitive['indices']).astype(int).reshape((-1, 3))
            out.append('usemtl '+mat)
            out.extend('v '+' '.join(f'{v:.7f}' for v in row) for row in p)
            out.extend('vn '+' '.join(f'{v:.7f}' for v in row) for row in normals)
            out.extend('f '+' '.join(f'{i+offset}//{i+offset}' for i in face) for face in faces)
            offset += len(p)
            report.append({'name': name, 'material': mat, 'triangles': len(faces),
                           'min': p.min(0).tolist(), 'max': p.max(0).tolist()})
        (ROOT/(name+'.obj')).write_text('\n'.join(out)+'\n')
    for child in node.get('children', []):
        convert(child, world)


for node in doc['scenes'][doc.get('scene', 0)]['nodes']:
    convert(node, np.eye(4))
(ROOT/'Fly.mtl').write_text(
    'newmtl Body\nKd 0.35 0.72 0.3\nnewmtl Dark\nKd 0.19 0.26 0.26\n'
    'newmtl Eyes\nKd 1 0.24 0.2\nnewmtl Wings\nKd 0.65 0.87 0.91\n'
    'newmtl WingHighlight\nKd 0.91 0.91 0.91\n')
(ROOT/'mesh-parts.json').write_text(json.dumps(report, indent=2)+'\n')
print('AroniaStudios Fly:', sum(p['triangles'] for p in report), 'triangles')
