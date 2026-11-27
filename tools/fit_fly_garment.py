"""Tailor a sleeveless garment to the downloaded fly abdomen, with collar/hem bands.

Uses the licensed fly geometry, rather than scaling a human shirt over the wings.
The small surface clearance follows the actual abdomen facets. Requires numpy.
"""
from pathlib import Path
import numpy as np

source = Path('Assets/_Project/Art/ThirdParty/Fly/Fly.obj')
target = Path('Assets/_Project/Art/ThirdParty/FlyClothing')
vertices, normals, faces = [], [], []
material = ''
for line in source.read_text().splitlines():
    data = line.split()
    if not data:
        continue
    if data[0] == 'v':
        vertices.append([float(v) for v in data[1:4]])
    elif data[0] == 'vn':
        normals.append([float(v) for v in data[1:4]])
    elif data[0] == 'usemtl':
        material = data[1]
    elif data[0] == 'f' and material == 'Body':
        faces.append([(int(v.split('/')[0])-1, int(v.split('/')[-1])-1) for v in data[1:]])
vertices, normals = np.array(vertices), np.array(normals)
used = sorted({index for face in faces for index, _ in face})
center = (vertices[used].min(0)+vertices[used].max(0))*.5
vertices = center+(vertices-center)*[1.045, 1.045, 1.025]


def clip(polygon, limit, keep_above):
    result = []
    for a, b in zip(polygon, polygon[1:]+polygon[:1]):
        inside_a = a[0][2] >= limit if keep_above else a[0][2] <= limit
        inside_b = b[0][2] >= limit if keep_above else b[0][2] <= limit
        if inside_a:
            result.append(a)
        if inside_a != inside_b:
            t = (limit-a[0][2])/(b[0][2]-a[0][2])
            result.append((a[0]+t*(b[0]-a[0]), a[1]+t*(b[1]-a[1])))
    return result


out = ['mtllib FittedVest.mtl', 'o FittedVest']
offset = 1
count = 0
for low, high, surface in [(-4.35, -4.15, 'FittedTrim'), (-4.15, -1.1, 'FittedFabric'), (-1.1, -.83, 'FittedTrim')]:
    out.append('usemtl '+surface)
    for face in faces:
        polygon = [(vertices[i], normals[n]) for i, n in face]
        polygon = clip(clip(polygon, low, True), high, False)
        if len(polygon) < 3:
            continue
        for p, n in polygon:
            out.append('v '+' '.join(f'{v:.7f}' for v in p))
        for p, n in polygon:
            n = n/np.linalg.norm(n)
            out.append('vn '+' '.join(f'{v:.7f}' for v in n))
        for i in range(1, len(polygon)-1):
            out.append('f '+' '.join(f'{j+offset}//{j+offset}' for j in [0, i, i+1]))
            count += 1
        offset += len(polygon)
(target/'FittedVest.obj').write_text('\n'.join(out)+'\n')
(target/'FittedVest.mtl').write_text('newmtl FittedFabric\nKd 0.4 0.6 0.7\nnewmtl FittedTrim\nKd 0.9 0.8 0.6\n')
print('Fitted sleeveless garment:', count, 'triangles; abdomen surface clearance 4.5%')
