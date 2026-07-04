const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const ropePath = path.join(root, 'Assets', 'Scripts', 'RopeController.cs');
const playerPath = path.join(root, 'Assets', 'Scripts', 'PlayerController.cs');

const rope = fs.readFileSync(ropePath, 'utf8');
const player = fs.readFileSync(playerPath, 'utf8');

const checks = [
  {
    name: 'RopeController exposes ConstraintLength',
    pass: /public\s+float\s+ConstraintLength\s*=>/.test(rope),
  },
  {
    name: 'RopeController exposes Tension01',
    pass: /public\s+float\s+Tension01\s*\{\s*get;\s*private\s+set;\s*\}/.test(rope),
  },
  {
    name: 'RopeController exposes HasConstraint guard',
    pass: /public\s+bool\s+HasConstraint\s*=>\s*config\s*!=\s*null/.test(rope),
  },
  {
    name: 'Rope visual length no longer stretches to dist * 1.05',
    pass: !/currentExtendedLength\s*=\s*dist\s*\*\s*1\.05f/.test(rope),
  },
  {
    name: 'Spring force uses shared constraintLength',
    pass: /if\s*\(dist\s*>\s*constraintLength\)[\s\S]*float\s+stretch\s*=\s*dist\s*-\s*constraintLength/.test(rope),
  },
  {
    name: 'Player auto-wires RopeController when missing',
    pass: /ropeController\s*=\s*FindObjectOfType<RopeController>\(\)/.test(player),
  },
  {
    name: 'Anchored movement clamps candidate surfaceT before MovePosition',
    pass: /surfaceT\s*=\s*ClampSurfaceTByRope\(nextSurfaceT\);[\s\S]*Vector2\s+targetPos\s*=\s*AnchorSurfacePoint\(surfaceT\)/.test(player),
  },
  {
    name: 'Player has fallback binary clamp for surface movement',
    pass: /float\s+FindLastRopeSafeSurfaceT\(/.test(player),
  },
];

let failed = false;
for (const check of checks) {
  if (check.pass) {
    console.log(`PASS ${check.name}`);
  } else {
    failed = true;
    console.error(`FAIL ${check.name}`);
  }
}

if (failed) process.exit(1);
