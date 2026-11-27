"""Convert the licensed clothing GLBs to normalized OBJ meshes and material metadata.

Preserves source normals, material boundaries and jacket UVs. Requires numpy.
Original downloads and author/license links live beside the derived files.
"""
import json
import struct
from pathlib import Path
import numpy as np

ROOT = Path('Assets/_Project/Art/ThirdParty/FlyClothing')


def transform(node):
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


def convert(name):
    raw = (ROOT/(name+'.glb')).read_bytes()
    size = struct.unpack_from('<I', raw, 12)[0]
    doc = json.loads(raw[20:20+size])
    binary = raw[28+size:]

    def accessor(index):
        a = doc['accessors'][index]
        view = doc['bufferViews'][a['bufferView']]
        dtype = np.dtype({5121: 'u1', 5123: '<u2', 5125: '<u4', 5126: '<f4'}[a['componentType']])
        width = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4}[a['type']]
        return np.ndarray((a['count'], width), dtype=dtype, buffer=binary,
                          offset=view.get('byteOffset', 0)+a.get('byteOffset', 0),
                          strides=(view.get('byteStride', width*dtype.itemsize), dtype.itemsize)).copy()

    canonical = np.eye(4)
    if name == 'Cap':
        canonical[:3, :3] = [[0, 0, -1], [0, 1, 0], [1, 0, 0]]
    elif name == 'Jacket':
        canonical[:3, :3] = [[0, 0, 1], [0, 1, 0], [-1, 0, 0]]
    parts = []

    def walk(index, parent):
        node = doc['nodes'][index]
        world = parent @ transform(node)
        if 'mesh' in node:
            m = canonical @ world
            for primitive in doc['meshes'][node['mesh']]['primitives']:
                attrs = primitive['attributes']
                p = accessor(attrs['POSITION'])
                faces = accessor(primitive['indices']).astype(int).reshape(-1, 3)
                uv = accessor(attrs['TEXCOORD_0']) if 'TEXCOORD_0' in attrs else np.zeros((len(p), 2))
                # glTF images use top-left V; OBJ/Unity textures use bottom-left V.
                uv[:, 1] = 1-uv[:, 1]
                if 'NORMAL' in attrs:
                    normals = accessor(attrs['NORMAL'])
                else:
                    # glTF permits omitted normals; expand triangles for flat face shading.
                    p = p[faces.reshape(-1)]
                    uv = uv[faces.reshape(-1)]
                    faces = np.arange(len(p)).reshape(-1, 3)
                    triangles = p.reshape(-1, 3, 3)
                    normals = np.repeat(np.cross(triangles[:, 1]-triangles[:, 0], triangles[:, 2]-triangles[:, 0]), 3, axis=0)
                p = (m @ np.column_stack([p, np.ones(len(p))]).T).T[:, :3]
                normals = normals @ np.linalg.inv(m[:3, :3])
                normals /= np.maximum(np.linalg.norm(normals, axis=1, keepdims=True), 1e-9)
                parts.append((p, normals, uv, faces, primitive['material']))
        for child in node.get('children', []):
            walk(child, world)

    for index in doc['scenes'][doc.get('scene', 0)]['nodes']:
        walk(index, np.eye(4))
    points = np.concatenate([part[0] for part in parts])
    center = (points.min(0)+points.max(0))*.5
    normalization = np.ptp(points, axis=0).max()
    out = ['mtllib '+name+'.mtl', 'o '+name]
    offset = 1
    for p, normals, uv, faces, material in parts:
        p = (p-center)/normalization
        out.append(f'usemtl {name}_{material}')
        out.extend('v '+' '.join(f'{v:.7f}' for v in row) for row in p)
        out.extend('vt '+' '.join(f'{v:.7f}' for v in row) for row in uv)
        out.extend('vn '+' '.join(f'{v:.7f}' for v in row) for row in normals)
        out.extend('f '+' '.join(f'{i+offset}/{i+offset}/{i+offset}' for i in face) for face in faces)
        offset += len(p)
    (ROOT/(name+'.obj')).write_text('\n'.join(out)+'\n')

    metadata = []
    mtl = []
    for i, material in enumerate(doc['materials']):
        pbr = material.get('pbrMetallicRoughness', {})
        rgba = pbr.get('baseColorFactor', [1, 1, 1, 1])
        entry = {'name': f'{name}_{i}', 'color': rgba, 'texture': ''}
        mtl.extend([f'newmtl {name}_{i}', 'Kd '+' '.join(str(v) for v in rgba[:3])])
        if 'baseColorTexture' in pbr:
            img = doc['images'][doc['textures'][pbr['baseColorTexture']['index']]['source']]
            view = doc['bufferViews'][img['bufferView']]
            file = name+f'-albedo-{i}'+('.jpg' if img.get('mimeType') == 'image/jpeg' else '.png')
            start = view.get('byteOffset', 0)
            (ROOT/file).write_bytes(binary[start:start+view['byteLength']])
            entry['texture'] = file
            mtl.append('map_Kd '+file)
        metadata.append(entry)
    (ROOT/(name+'.mtl')).write_text('\n'.join(mtl)+'\n')
    return {'name': name, 'triangles': sum(len(p[3]) for p in parts), 'materials': metadata}


if __name__ == '__main__':
    report = [convert(name) for name in ['Cap', 'Shirt', 'Jacket', 'Backpack', 'Bowtie']]
    (ROOT/'materials.json').write_text(json.dumps({'models': report}, indent=2)+'\n')
    print([(m['name'], m['triangles']) for m in report])
