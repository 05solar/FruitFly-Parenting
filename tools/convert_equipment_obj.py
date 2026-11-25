"""Convert decoded static GLB meshes to Unity-importable OBJ, keeping node transforms/normals.
Run tools/decode_equipment_glb.mjs first. Requires numpy; no runtime dependencies.
Original CC0 assets remain alongside the derived OBJ/MTL files.
"""
import json
from pathlib import Path
import numpy as np

ROOT = Path('Assets/_Project/Art/ThirdParty/PlayerEquipment')
for name in ['Wheelchair', 'Baton']:
    source = Path('Logs/ArtDownloads') / name
    doc = json.loads((source / 'model.json').read_text())
    def accessor(index):
        a = doc['accessors'][index]
        view = doc['bufferViews'][a['bufferView']]
        dtype = np.dtype({5120:'i1', 5121:'u1', 5122:'<i2', 5123:'<u2', 5125:'<u4', 5126:'<f4'}[a['componentType']])
        width = {'SCALAR':1, 'VEC2':2, 'VEC3':3, 'VEC4':4}[a['type']]
        raw = (source / f"{a['bufferView']}.bin").read_bytes()
        data = np.ndarray((a['count'],width), dtype=dtype, buffer=raw,
            offset=a.get('byteOffset',0), strides=(view.get('byteStride',width*dtype.itemsize), dtype.itemsize)).astype(float)
        if a.get('normalized') and dtype.kind in 'iu':
            data /= np.iinfo(dtype).max
            data = np.maximum(data,-1)
        return data
    def matrix(node):
        if 'matrix' in node: return np.array(node['matrix']).reshape((4,4),order='F')
        x,y,z,w = node.get('rotation',[0,0,0,1])
        m = np.eye(4)
        m[:3,:3] = np.array([[1-2*y*y-2*z*z,2*x*y-2*z*w,2*x*z+2*y*w],
            [2*x*y+2*z*w,1-2*x*x-2*z*z,2*y*z-2*x*w],
            [2*x*z-2*y*w,2*y*z+2*x*w,1-2*x*x-2*y*y]]) @ np.diag(node.get('scale',[1,1,1]))
        m[:3,3] = node.get('translation',[0,0,0])
        return m
    out = [f'mtllib {name}.mtl']; offset = [1]; total = [0]
    def walk(index,parent):
        node = doc['nodes'][index]; world = parent @ matrix(node)
        if 'mesh' in node:
            for part,p in enumerate(doc['meshes'][node['mesh']]['primitives']):
                assert p.get('mode',4)==4
                positions = accessor(p['attributes']['POSITION'])
                positions = (world @ np.column_stack([positions,np.ones(len(positions))]).T).T[:,:3]
                normals = accessor(p['attributes']['NORMAL']) @ np.linalg.inv(world[:3,:3])
                normals /= np.maximum(np.linalg.norm(normals,axis=1,keepdims=True),1e-9)
                indices = accessor(p['indices']).astype(int).reshape((-1,3))
                if np.linalg.det(world[:3,:3])<0: indices=indices[:,::-1]
                out.append(f"o {node.get('name','Part')}_{part}")
                out.append('usemtl '+doc['materials'][p.get('material',0)].get('name','Material'))
                out.extend('v '+' '.join(f'{v:.7f}' for v in row) for row in positions)
                out.extend('vn '+' '.join(f'{v:.7f}' for v in row) for row in normals)
                for row in indices+offset[0]: out.append('f '+' '.join(f'{i}//{i}' for i in row))
                offset[0]+=len(positions);total[0]+=len(indices)
        for child in node.get('children',[]):walk(child,world)
    for node in doc['scenes'][doc.get('scene',0)]['nodes']:walk(node,np.eye(4))
    (ROOT/(name+'.obj')).write_text('\n'.join(out)+'\n')
    mats=[]
    for m in doc['materials']:
        rgb=m.get('pbrMetallicRoughness',{}).get('baseColorFactor',[.11,.14,.16,1])[:3]
        mats.extend(['newmtl '+m.get('name','Material'),'Kd '+' '.join(str(v) for v in rgb)])
    (ROOT/(name+'.mtl')).write_text('\n'.join(mats)+'\n')
    print(name,total[0],'triangles')
