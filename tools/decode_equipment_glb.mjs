// Offline import preparation; npm install --prefix Logs/ArtTools meshoptimizer
import fs from 'node:fs';
import { MeshoptDecoder } from '../Logs/ArtTools/node_modules/meshoptimizer/index.js';
await MeshoptDecoder.ready;
for (const name of ['Wheelchair', 'Baton']) {
  const raw = fs.readFileSync(`Assets/_Project/Art/ThirdParty/PlayerEquipment/${name}.glb`);
  const size = raw.readUInt32LE(12);
  const doc = JSON.parse(raw.subarray(20, 20 + size).toString());
  const bin = raw.subarray(28 + size);
  const output = `Logs/ArtDownloads/${name}`;
  fs.mkdirSync(output, { recursive: true });
  doc.bufferViews.forEach((view, index) => {
    const ext = view.extensions?.EXT_meshopt_compression;
    let bytes;
    if (ext) {
      bytes = new Uint8Array(ext.count * ext.byteStride);
      MeshoptDecoder.decodeGltfBuffer(bytes, ext.count, ext.byteStride,
        bin.subarray(ext.byteOffset || 0, (ext.byteOffset || 0) + ext.byteLength), ext.mode, ext.filter);
    } else bytes = bin.subarray(view.byteOffset || 0, (view.byteOffset || 0) + view.byteLength);
    fs.writeFileSync(`${output}/${index}.bin`, bytes);
  });
  fs.writeFileSync(`${output}/model.json`, JSON.stringify(doc));
  console.log(`Decoded ${name}`);
}
