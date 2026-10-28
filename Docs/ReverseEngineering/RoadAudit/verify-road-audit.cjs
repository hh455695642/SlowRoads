// Read-only probes against the local game archive and the original road segment class.
// Run: node verify-road-audit.cjs [original game directory]
// No Unity Editor or game code is started. Geometry containers below are data-only stubs.
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const crypto = require('node:crypto');
const assert = require('node:assert/strict');

const projectRoot = path.resolve(__dirname, '../../..');
const root = process.argv[2] || path.join(projectRoot, 'Slow Roads');
const chunks = 'build/_app/immutable/chunks';
const manifest = JSON.parse(fs.readFileSync(path.join(projectRoot,
  'Docs/ReverseEngineering/SceneArchitecture/source-manifest.json'), 'utf8'));
const originals = [
  'DevMidlineGenerator.6316d1a7.js', 'SettingsManager.9657445f.js',
  'HillsHeightmap.2938f21e.js', 'CaliMidlineGenerator.98e60041.js',
  'normals.d4d92770.js', '2.c189d18b.js',
];
const archive = fs.openSync(path.join(root, 'resources/app.asar'), 'r');
const size = Buffer.alloc(8);
fs.readSync(archive, size, 0, 8, 0);
const headerLength = size.readUInt32LE(4);
const headerBytes = Buffer.alloc(headerLength);
fs.readSync(archive, headerBytes, 0, headerLength, 8);
const header = JSON.parse(headerBytes.subarray(8, 8 + headerBytes.readUInt32LE(4)));
const sourceBytes = new Map();
const provenance = originals.map(name => {
  const relative = `${chunks}/${name}`;
  let entry = header;
  for (const part of relative.split('/')) entry = entry.files[part];
  const bytes = Buffer.alloc(entry.size);
  let read = 0;
  while (read < bytes.length) {
    const count = fs.readSync(archive, bytes, read, bytes.length - read,
      8 + headerLength + Number(entry.offset) + read);
    assert(count > 0, 'Unexpected archive EOF');
    read += count;
  }
  sourceBytes.set(name, bytes);
  const sha256 = crypto.createHash('sha256').update(bytes).digest('hex');
  const listed = manifest.find(item => `${item.module}.js` === name);
  if (listed) assert.equal(sha256, listed.rawSHA256, `${name}: manifest hash mismatch`);
  return { name, size: entry.size, sha256, manifestVerified: Boolean(listed) };
});
fs.closeSync(archive);

class Vec {
  constructor(x = 0, y = 0, z = 0) { Object.assign(this, { x, y, z }); }
  copy(v) { return this.set(v.x, v.y, v.z); }
  set(x, y, z) { Object.assign(this, { x, y, z }); return this; }
  sub(v) { this.x -= v.x; this.y -= v.y; this.z -= v.z; return this; }
  normalize() { const l = Math.hypot(this.x, this.y, this.z) || 1;
    this.x /= l; this.y /= l; this.z /= l; return this; }
  cross(v) { const { x, y, z } = this;
    return this.set(y * v.z - z * v.y, z * v.x - x * v.z, x * v.y - y * v.x); }
}
class Attribute {
  constructor(array, itemSize) { Object.assign(this, { array, itemSize }); }
  setUsage() {}
}
class Geometry {
  constructor() { this.attributes = {}; }
  setAttribute(k, v) { this.attributes[k] = v; }
  setIndex(v) { this.index = { array: Uint16Array.from(v, x => x || 0) }; }
  setDrawRange(start, count) { this.drawRange = { start, count }; }
}
class Mesh {
  constructor(geometry, material) { this.geometry = geometry; this.material = material; this.position = new Vec; }
  updateMatrix() {}
}
class Sphere { constructor() { this.center = new Vec; } }

const source = sourceBytes.get(originals[0]).toString('utf8');
const begin = source.indexOf('let fe=0,');
const end = source.indexOf('let U=D;', begin);
assert(begin >= 0 && end > begin, 'Original segment markers changed');
const segmentSource = source.slice(begin, end) + '\nglobalThis.OriginalSegment=D;';

function makeContext(halfWidth) {
  const context = vm.createContext({ m: Vec, K: Geometry, J: Mesh, O: Attribute,
    B: 0, ee: Sphere, $: class {}, G: .02, i: { vehicleIndex: -1000 }, console,
    n: (object, key, value) => { object[key] = value; } });
  vm.runInContext(segmentSource, context);
  const Segment = context.OriginalSegment;
  Segment.prototype.getRoadWidth = () => halfWidth;
  Segment.prototype.customGenNext = () => {};
  Segment.prototype.customGenFine = () => {};
  return Segment;
}
function makeNodes(subdivisions) {
  const nodes = Array.from({ length: subdivisions.length + 2 }, (_, i) => ({
    i, p: new Vec(i * 10, i * 2, 0), n: new Vec(0, 0, 1), bridge: false,
  }));
  nodes.forEach((n, i) => { n.prev = nodes[i - 1]; n.next = nodes[i + 1]; });
  for (let i = 0; i < nodes.length - 1; i++) {
    const count = subdivisions[i] || 1;
    let previous = nodes[i];
    for (let j = 1; j <= count; j++) {
      const next = j === count ? nodes[i + 1] : {
        p: new Vec(i * 10 + 10 * j / count, i * 2 + 2 * j / count, 0),
        n: new Vec(0, 0, 1), prev: nodes[i], next: nodes[i + 1],
      };
      previous.nextFine = next; next.prevFine = previous; previous = next;
    }
  }
  return nodes;
}
function generate(Segment, start, coarseIntervals) {
  const segment = new Segment(coarseIntervals);
  segment.initGeneration(start);
  let calls = 0;
  while (!segment.genNext()) assert(++calls < coarseIntervals + 1);
  segment.finalise();
  return segment;
}
function near(a, b, epsilon = 2e-6) { assert(Math.abs(a - b) < epsilon, `${a} != ${b}`); }

const geometry = [];
for (const halfWidth of [1.8, 3.7, 4.7, 6]) {
  for (const subdivisions of [[1, 1], [2, 5], [10, 10]]) {
    const Segment = makeContext(halfWidth);
    const nodes = makeNodes(subdivisions);
    const segment = generate(Segment, nodes[0], 2);
    const fineIntervals = subdivisions.reduce((a, b) => a + b, 0);
    assert.equal(segment.genIndex / 3, (fineIntervals + 1) * 2);
    assert.equal(segment.lod0LiveIndex, 12);
    assert.equal(segment.lod1LiveIndex - segment.lod1IndexStart, fineIntervals * 6);
    near(segment.curUV, 2 * 10 / (halfWidth * 2));
    near(segment.posArr[5] - segment.posArr[2], halfWidth * 2);
    const firstNormalLength = Math.hypot(...segment.normArr.slice(0, 3));
    near(firstNormalLength, 1); // tangent and lateral normal are perpendicular in this fixture.
    geometry.push({ halfWidth, subdivisions, vertices: segment.genIndex / 3,
      coarseTriangles: segment.lod0LiveIndex / 3, fineTriangles: fineIntervals * 2,
      endV: segment.curUV, fullWidth: segment.posArr[5] - segment.posArr[2] });
  }
}

// Both segment objects are constructed before generation, matching the controller's lifecycle.
const Segment = makeContext(3.7);
const nodes = makeNodes([2, 5, 3, 4]);
const segmentA = new Segment(2), segmentB = new Segment(2);
segmentA.initGeneration(nodes[0]);
while (!segmentA.genNext()) {}
segmentA.finalise();
segmentB.initGeneration(nodes[2]);
near(segmentA.curUV, segmentB.uvArr[1]);
const last = segmentA.genIndex - 6;
for (let k = 0; k < 6; k++) {
  const component = k % 3;
  const offsetA = component === 0 ? segmentA.o.x : component === 2 ? segmentA.o.z : 0;
  const offsetB = component === 0 ? segmentB.o.x : component === 2 ? segmentB.o.z : 0;
  near(segmentA.posArr[last + k] + offsetA, segmentB.posArr[k] + offsetB);
}

const adaptiveSegments = dot => Math.max(1, Math.min(10,
  Math.floor(10 * Math.sqrt((1 - Math.max(Math.min(dot, 1), .99)) * 100))));
const adaptive = [1, .9999, .999, .995, .99, .98].map(dot => ({ dot, segments: adaptiveSegments(dot) }));
const unitySource = fs.readFileSync(path.resolve(__dirname,
  '../../../Assets/SlowRoads/Runtime/Legacy/Road/RoadMidline.cs'), 'utf8');
const step = unitySource.slice(unitySource.indexOf('public RoadNode Step()'),
  unitySource.indexOf('static float ClampAngleDelta'));
const withoutComments = step.replace(/\/\/[^\r\n]*/g, '');
assert.equal((withoutComments.match(/\bgradLon\b/g) || []).length, 1);
assert.equal((withoutComments.match(/\bgradLat\b/g) || []).length, 1);

console.log(JSON.stringify({ scope: 'Archive identity, original segment data construction, and static road-control checks; no Unity runtime or visual verification.',
  provenance, geometry, segmentSeam: { worldPositionsMatch: true, uvMatches: true,
    previousEndV: segmentA.curUV, nextStartV: segmentB.uvArr[1] },
  adaptive, unityGradLonAndGradLatHaveNoConsumers: true }, null, 2));
