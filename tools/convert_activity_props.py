"""Normalize the licensed activity props using the shared GLB-to-OBJ converter."""
import json
from pathlib import Path
import convert_fly_clothing as converter

converter.ROOT = Path('Assets/_Project/Art/ThirdParty/ActivityProps')
sources = json.loads((converter.ROOT/'sources.json').read_text())
report = [converter.convert(item['name']) for item in sources]
(converter.ROOT/'materials.json').write_text(json.dumps({'models': report}, indent=2)+'\n')
print([(m['name'], m['triangles']) for m in report])
