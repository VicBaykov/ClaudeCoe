// ============================================================
// «Эллипсоид неопределённости VIO в коридоре» — главный модуль.
// Сцена, телефон, эллипсоид P = Λ⁻¹, HUD и график λ_min.
// Математика — в math.js, сцены-пресеты — в scenes.js,
// описание модели и упрощений — в README.md.
// ============================================================

import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { zero3, addBearing, addNormal, quadForm, jacobiEig3, EPS_DARK } from './math.js';
import { PRESETS } from './scenes.js';

// ---------- параметры модели наблюдения ----------
const FOV_HALF = THREE.MathUtils.degToRad(35); // полуугол конуса обзора камеры
const COS_HALF = Math.cos(FOV_HALF);
const MAX_DIST = 9;                  // радиус видимости фичи, м
// Шум измерения растёт с расстоянием: R(d) = R0·(1 + (d/D0)²), вес w = 1/R
const R0 = 0.5, D0 = 4;
const ELL_SCALE = 1.2;               // полуось эллипсоида = ELL_SCALE/√(λ+eps)
const AXIS_CAP = 9;                  // потолок полуоси — «упирается далеко вдоль коридора»
const MAX_FEATURES = 220;
const RUN_TIME = 14;                 // длительность «Пройти маршрут», с

// ---------- рендерер / сцена / камера ----------
const renderer = new THREE.WebGLRenderer({ antialias: true });
renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
renderer.setSize(innerWidth, innerHeight);
document.body.prepend(renderer.domElement);

const scene = new THREE.Scene();
scene.background = new THREE.Color(0x0e1117);
scene.fog = new THREE.Fog(0x0e1117, 22, 70);

const camera = new THREE.PerspectiveCamera(55, innerWidth / innerHeight, 0.05, 200);
const controls = new OrbitControls(camera, renderer.domElement);
controls.enableDamping = true;
controls.dampingFactor = 0.08;

addEventListener('resize', () => {
  camera.aspect = innerWidth / innerHeight;
  camera.updateProjectionMatrix();
  renderer.setSize(innerWidth, innerHeight);
  sizeGraph();
});

// мягкий общий свет (точечные источники добавляют сами сцены)
scene.add(new THREE.HemisphereLight(0xbcd2ff, 0x39322a, 0.55));
const sun = new THREE.DirectionalLight(0xfff3e0, 0.7);
sun.position.set(6, 12, 4);
scene.add(sun);
const phoneLight = new THREE.PointLight(0xaad4ff, 5, 7, 2); // подсветка вокруг телефона
scene.add(phoneLight);

// ---------- телефон с конусом поля зрения ----------
function makePhone() {
  const g = new THREE.Group();
  const body = new THREE.Mesh(
    new THREE.BoxGeometry(0.2, 0.4, 0.03),
    new THREE.MeshStandardMaterial({ color: 0x262b36, roughness: 0.35, metalness: 0.7 }));
  g.add(body);
  const screen = new THREE.Mesh(
    new THREE.PlaneGeometry(0.17, 0.36),
    new THREE.MeshStandardMaterial({ color: 0x9ad7ff, emissive: 0x3a78b8, emissiveIntensity: 0.8, roughness: 0.2 }));
  screen.position.z = -0.016;
  screen.rotation.y = Math.PI;
  g.add(screen);
  // конус поля зрения: вершина в камере, раскрывается вдоль +Z (вперёд)
  const L = 3.2, r = Math.tan(FOV_HALF) * L;
  const coneGeo = new THREE.ConeGeometry(r, L, 32, 1, true);
  coneGeo.translate(0, -L / 2, 0);   // вершина в начало координат
  coneGeo.rotateX(-Math.PI / 2);     // ось — вдоль +Z
  const cone = new THREE.Mesh(coneGeo, new THREE.MeshBasicMaterial({
    color: 0x9ad7ff, transparent: true, opacity: 0.07, side: THREE.DoubleSide, depthWrite: false,
  }));
  g.add(cone);
  return g;
}
const phone = makePhone();
scene.add(phone);

// ---------- эллипсоид неопределённости ----------
// Единичная сфера, в каждый кадр получает матрицу M = [v1·a1 | v2·a2 | v3·a3]:
// полуоси вдоль собственных векторов Λ, длины aᵢ = ELL_SCALE/√(λᵢ+eps)
const ellMat = new THREE.MeshStandardMaterial({
  color: 0x6fb7ff, transparent: true, opacity: 0.22,
  roughness: 0.35, metalness: 0, side: THREE.DoubleSide, depthWrite: false,
});
const ellipsoid = new THREE.Mesh(new THREE.SphereGeometry(1, 48, 32), ellMat);
ellipsoid.matrixAutoUpdate = false;
scene.add(ellipsoid);

// ---------- стрелки собственных векторов + подписи ----------
const EIG_COLORS = [0x4ec9b0, 0x8fb6e8, 0xb58fe8];
const arrows = [], labels = [];
function makeLabel() {
  const cnv = document.createElement('canvas');
  cnv.width = 256; cnv.height = 64;
  const c2 = cnv.getContext('2d');
  const tex = new THREE.CanvasTexture(cnv);
  const spr = new THREE.Sprite(new THREE.SpriteMaterial({ map: tex, transparent: true, depthTest: false }));
  spr.scale.set(1.0, 0.25, 1);
  let last = '';
  return {
    spr,
    set(text, color = '#d7dde8') {
      const key = text + color;
      if (key === last) return;
      last = key;
      c2.clearRect(0, 0, 256, 64);
      c2.font = '600 30px Consolas, monospace';
      c2.fillStyle = color;
      c2.fillText(text, 6, 42);
      tex.needsUpdate = true;
    },
  };
}
for (let i = 0; i < 3; i++) {
  const a = new THREE.ArrowHelper(new THREE.Vector3(1, 0, 0), new THREE.Vector3(), 1, EIG_COLORS[i], 0.14, 0.07);
  scene.add(a);
  arrows.push(a);
  const lb = makeLabel();
  scene.add(lb.spr);
  labels.push(lb);
}

// ---------- пробное направление d ----------
const probeArrow = new THREE.ArrowHelper(new THREE.Vector3(0, 0, 1), new THREE.Vector3(), 1.1, 0xffd24a, 0.18, 0.1);
scene.add(probeArrow);

// ---------- лучи к видимым фичам ----------
const rayPos = new Float32Array(MAX_FEATURES * 2 * 3);
const rayGeo = new THREE.BufferGeometry();
rayGeo.setAttribute('position', new THREE.BufferAttribute(rayPos, 3));
rayGeo.setDrawRange(0, 0);
const rays = new THREE.LineSegments(rayGeo,
  new THREE.LineBasicMaterial({ color: 0x9ad7ff, transparent: true, opacity: 0.3 }));
rays.frustumCulled = false;
scene.add(rays);

// ---------- фичи-точки (светящиеся) ----------
function makeDotTexture() {
  const c = document.createElement('canvas');
  c.width = c.height = 64;
  const g2 = c.getContext('2d');
  const grd = g2.createRadialGradient(32, 32, 2, 32, 32, 30);
  grd.addColorStop(0, 'rgba(255,255,255,1)');
  grd.addColorStop(0.4, 'rgba(255,255,255,0.7)');
  grd.addColorStop(1, 'rgba(255,255,255,0)');
  g2.fillStyle = grd;
  g2.fillRect(0, 0, 64, 64);
  return new THREE.CanvasTexture(c);
}
const dotTex = makeDotTexture();

// цвета фич: видимая bearing-фича / видимая фича однородной стены / невидимая
const C_VIS = new THREE.Color(0xffe28a);
const C_VIS_N = new THREE.Color(0x7fe0d0);
const C_DIM = new THREE.Color(0x39404e);

function buildPoints(features) {
  const n = features.length;
  const pos = new Float32Array(n * 3);
  const col = new Float32Array(n * 3);
  features.forEach((f, i) => f.pos.toArray(pos, i * 3));
  const geo = new THREE.BufferGeometry();
  geo.setAttribute('position', new THREE.BufferAttribute(pos, 3));
  geo.setAttribute('color', new THREE.BufferAttribute(col, 3));
  const points = new THREE.Points(geo, new THREE.PointsMaterial({
    size: 0.13, vertexColors: true, map: dotTex,
    transparent: true, depthWrite: false, sizeAttenuation: true,
  }));
  points.frustumCulled = false;
  return { points, colAttr: geo.getAttribute('color') };
}

// ---------- HUD ----------
const $ = (id) => document.getElementById(id);
const mcells = [...Array(9)].map((_, i) => $('m' + i));
const elL = [$('l1'), $('l2'), $('l3')];
const elLmin = $('lminVal'), elQ = $('qVal'), darkBox = $('dark');
const routeSlider = $('routeT'), runBtn = $('runBtn');
const azS = $('az'), elS = $('el'), azV = $('azV'), elV = $('elV');

function fmt(x) {
  if (!isFinite(x)) return '∞';
  const ax = Math.abs(x);
  if (ax !== 0 && ax < 1e-3) return x.toExponential(1);
  return x.toFixed(3);
}

// ---------- состояние ----------
let cur = null;        // текущий пресет: {group, features, route, points, colAttr}
let tParam = 0;        // позиция на маршруте, 0..1
let running = false;   // идёт ли анимация «Пройти маршрут»
let samples = [];      // [{t, l}] — λ_min вдоль маршрута для графика
const tmp = new THREE.Vector3(), tgt = new THREE.Vector3(), look = new THREE.Vector3();

function disposeObj(o) {
  o.traverse?.((c) => {
    c.geometry?.dispose();
    const m = c.material;
    if (Array.isArray(m)) m.forEach((x) => x.dispose());
    else m?.dispose();
  });
}

function setPreset(name) {
  if (cur) {
    scene.remove(cur.group, cur.points);
    disposeObj(cur.group);
    cur.points.geometry.dispose();
    cur.points.material.dispose();
  }
  const built = PRESETS[name].build();
  const pts = buildPoints(built.features);
  scene.add(built.group, pts.points);
  cur = { ...built, ...pts, name };
  tParam = 0;
  routeSlider.value = 0;
  samples = [];
  running = false;
  runBtn.textContent = '▶ Пройти маршрут';
  routeSlider.disabled = false;
  camera.position.copy(built.camPos);
  controls.target.copy(built.route(0).pos);
  document.querySelectorAll('#presets button').forEach((b) =>
    b.classList.toggle('active', b.dataset.p === name));
}

document.getElementById('presets').addEventListener('click', (e) => {
  if (e.target.dataset.p) setPreset(e.target.dataset.p);
});
routeSlider.addEventListener('input', () => { tParam = routeSlider.value / 1000; });
runBtn.addEventListener('click', () => {
  if (running) {
    running = false;
    runBtn.textContent = '▶ Пройти маршрут';
    routeSlider.disabled = false;
  } else {
    running = true;
    tParam = 0;
    samples = [];
    runBtn.textContent = '■ Стоп';
    routeSlider.disabled = true;
  }
});
azS.addEventListener('input', () => { azV.textContent = azS.value + '°'; });
elS.addEventListener('input', () => { elV.textContent = elS.value + '°'; });

// ============================================================
// Пересчёт Λ — O(числа фич), в каждый кадр.
// ============================================================
function recompute(p, dir) {
  const L = zero3();
  let vis = 0;
  const feats = cur.features;
  for (let k = 0; k < feats.length; k++) {
    const f = feats[k];
    tmp.subVectors(f.pos, p);
    const dist = tmp.length();
    let isVis = false;
    if (dist < MAX_DIST && dist > 0.05) {
      tmp.divideScalar(dist); // r — единичный луч зрения камера→фича
      if (tmp.dot(dir) > COS_HALF) {
        isVis = true;
        // вес измерения: w = 1/R, R = R0·(1+(d/D0)²) — дальние фичи шумнее
        const w = 1 / (R0 * (1 + (dist / D0) ** 2));
        if (f.n) {
          // фича однородной стены: информация только вдоль нормали, Λ += w·n nᵀ
          addNormal(L, [f.n.x, f.n.y, f.n.z], w);
        } else {
          // bearing-фича: Λ += w·(I − r rᵀ) — вся плоскость поперёк луча
          addBearing(L, [tmp.x, tmp.y, tmp.z], w);
        }
        // луч к видимой фиче
        rayPos[vis * 6 + 0] = p.x; rayPos[vis * 6 + 1] = p.y; rayPos[vis * 6 + 2] = p.z;
        rayPos[vis * 6 + 3] = f.pos.x; rayPos[vis * 6 + 4] = f.pos.y; rayPos[vis * 6 + 5] = f.pos.z;
        vis++;
      }
    }
    // подсветка точки: видимая ярче, невидимая гаснет
    const c = isVis ? (f.n ? C_VIS_N : C_VIS) : C_DIM;
    cur.colAttr.setXYZ(k, c.r, c.g, c.b);
  }
  cur.colAttr.needsUpdate = true;
  rayGeo.setDrawRange(0, vis * 2);
  rayGeo.getAttribute('position').needsUpdate = true;

  const eig = jacobiEig3(L); // [{l, v}], λ1 ≥ λ2 ≥ λ3
  return { L, eig, vis };
}

// ---------- визуализация эллипсоида / стрелок ----------
const basisM = new THREE.Matrix4();
const c1 = new THREE.Vector3(), c2 = new THREE.Vector3(), c3 = new THREE.Vector3();

function axisLen(l) {
  // полуось ∝ 1/√(λ+eps); вырожденное направление упирается в потолок AXIS_CAP
  return Math.min(AXIS_CAP, ELL_SCALE / Math.sqrt(l + EPS_DARK));
}

function updateVisuals(p, { L, eig }) {
  const dark = eig[2].l < EPS_DARK;

  // эллипсоид: M = [v1·a1 | v2·a2 | v3·a3], позиция — телефон
  c1.fromArray(eig[0].v).multiplyScalar(axisLen(eig[0].l));
  c2.fromArray(eig[1].v).multiplyScalar(axisLen(eig[1].l));
  c3.fromArray(eig[2].v).multiplyScalar(axisLen(eig[2].l));
  basisM.makeBasis(c1, c2, c3);
  basisM.setPosition(p);
  ellipsoid.matrix.copy(basisM);
  ellMat.color.set(dark ? 0xff7a7a : 0x6fb7ff);

  // стрелки собственных векторов + подписи λᵢ
  for (let i = 0; i < 3; i++) {
    const e = eig[i];
    const isDark = e.l < EPS_DARK;
    const len = Math.max(0.35, axisLen(e.l));
    tmp.fromArray(e.v);
    arrows[i].position.copy(p);
    arrows[i].setDirection(tmp);
    arrows[i].setLength(len, 0.14, 0.07);
    arrows[i].setColor(isDark ? 0xff5555 : EIG_COLORS[i]);
    labels[i].spr.position.copy(p).addScaledVector(tmp, len + 0.25);
    labels[i].set(`λ${i + 1}=${fmt(e.l)}`, isDark ? '#ff7a7a' : '#d7dde8');
  }

  // пробное направление d из слайдеров (азимут/элевация)
  const az = THREE.MathUtils.degToRad(+azS.value);
  const el = THREE.MathUtils.degToRad(+elS.value);
  const d = [Math.cos(el) * Math.cos(az), Math.sin(el), Math.cos(el) * Math.sin(az)];
  tmp.fromArray(d);
  probeArrow.position.copy(p);
  probeArrow.setDirection(tmp);
  return quadForm(L, d); // dᵀΛd — информация вдоль пробы
}

// ---------- HUD ----------
function updateHUD({ L, eig }, q) {
  for (let i = 0; i < 3; i++)
    for (let j = 0; j < 3; j++)
      mcells[i * 3 + j].textContent = fmt(L[i][j]);
  for (let i = 0; i < 3; i++) elL[i].textContent = fmt(eig[i].l);

  const lmin = eig[2].l;
  elLmin.textContent = fmt(lmin);
  // цветовая шкала: зелёный при «здоровой» информации, красный к нулю
  const h = 120 * Math.min(1, Math.max(0, lmin / 1.5));
  elLmin.style.color = `hsl(${h}, 75%, 58%)`;
  elQ.textContent = fmt(q);

  const nDark = eig.filter((e) => e.l < EPS_DARK).length;
  if (nDark > 0) {
    const v = eig[2].v;
    darkBox.className = 'found';
    darkBox.innerHTML = nDark === 1
      ? `⚠ Тёмное направление: d ≈ (${v.map((x) => x.toFixed(2)).join(', ')}) — вдоль него измерения не дают информации.`
      : `⚠ Тёмных направлений: ${nDark} — наблюдается лишь ${3 - nDark}-мерное подпространство.`;
  } else {
    darkBox.className = '';
    darkBox.textContent = '✓ Тёмных направлений нет: все три направления наблюдаемы.';
  }
}

// ---------- график λ_min (canvas 2D внизу) ----------
const gCanvas = $('graph');
const gCtx = gCanvas.getContext('2d');
function sizeGraph() {
  const r = gCanvas.getBoundingClientRect();
  gCanvas.width = r.width * devicePixelRatio;
  gCanvas.height = r.height * devicePixelRatio;
  gCtx.setTransform(devicePixelRatio, 0, 0, devicePixelRatio, 0, 0);
}
sizeGraph();

function drawGraph() {
  const w = gCanvas.clientWidth, h = gCanvas.clientHeight;
  gCtx.clearRect(0, 0, w, h);
  // сетка
  gCtx.strokeStyle = 'rgba(255,255,255,0.07)';
  gCtx.lineWidth = 1;
  gCtx.beginPath();
  for (let i = 1; i < 4; i++) { gCtx.moveTo(0, h * i / 4); gCtx.lineTo(w, h * i / 4); }
  gCtx.stroke();
  // нулевая линия — красная: сюда проседает λ_min в коридоре
  gCtx.strokeStyle = 'rgba(255,107,107,0.5)';
  gCtx.beginPath(); gCtx.moveTo(0, h - 3); gCtx.lineTo(w, h - 3); gCtx.stroke();

  let maxL = 0.5;
  for (const s of samples) maxL = Math.max(maxL, s.l);
  const X = (t) => t * w;
  const Y = (l) => h - 3 - (l / maxL) * (h - 16);

  if (samples.length > 1) {
    gCtx.strokeStyle = '#5dd39e';
    gCtx.lineWidth = 2;
    gCtx.beginPath();
    gCtx.moveTo(X(samples[0].t), Y(samples[0].l));
    for (const s of samples) gCtx.lineTo(X(s.t), Y(s.l));
    gCtx.stroke();
  }
  // маркер текущей позиции на маршруте
  gCtx.strokeStyle = 'rgba(255,210,74,0.8)';
  gCtx.beginPath(); gCtx.moveTo(X(tParam), 0); gCtx.lineTo(X(tParam), h); gCtx.stroke();
  // подпись масштаба
  gCtx.fillStyle = 'rgba(215,221,232,0.55)';
  gCtx.font = '11px Consolas, monospace';
  gCtx.fillText('max ' + fmt(maxL), 6, 13);
}

// ---------- главный цикл ----------
setPreset('corridor');
let prevTime = performance.now();

function tick(now) {
  const dt = Math.min(0.05, (now - prevTime) / 1000);
  prevTime = now;

  if (running) {
    tParam += dt / RUN_TIME;
    if (tParam >= 1) {
      tParam = 1;
      running = false;
      runBtn.textContent = '▶ Пройти маршрут';
      routeSlider.disabled = false;
    }
    routeSlider.value = Math.round(tParam * 1000);
  }

  // позиция и взгляд телефона на маршруте
  const { pos, dir } = cur.route(tParam);
  phone.position.copy(pos);
  phone.lookAt(look.copy(pos).add(dir));
  phoneLight.position.copy(pos).y += 0.6;

  // Λ → собственное разложение → визуализация → HUD
  const res = recompute(pos, dir);
  const q = updateVisuals(pos, res);
  updateHUD(res, q);
  if (running) samples.push({ t: tParam, l: res.eig[2].l });
  drawGraph();

  // камера-наблюдатель мягко следует за телефоном
  controls.target.lerp(tgt.copy(pos), 0.06);
  controls.update();
  renderer.render(scene, camera);
  requestAnimationFrame(tick);
}
requestAnimationFrame(tick);
