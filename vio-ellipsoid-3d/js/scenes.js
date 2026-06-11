// ============================================================
// Три сцены-пресета. Каждый build() возвращает:
//   group    — THREE.Group с геометрией окружения и светом,
//   features — [{ pos: Vector3, n: Vector3|null }]
//              n === null → «богатая» фича: bearing-вклад w·(I − r rᵀ)
//              n — нормаль однородной стены: вклад только w·n nᵀ
//   route(t) — позиция и направление взгляда телефона, t ∈ [0,1]
//   camPos   — стартовая позиция камеры-наблюдателя
// ============================================================

import * as THREE from 'three';

// Детерминированный ГПСЧ — расстановка фич воспроизводима между запусками
function mulberry32(seed) {
  return function () {
    seed |= 0; seed = (seed + 0x6D2B79F5) | 0;
    let t = Math.imul(seed ^ (seed >>> 15), 1 | seed);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

// Стены комнат — BackSide: видны только изнутри, поэтому сцену
// удобно облетать снаружи, ближняя стена не загораживает.
const inMat = (color, rough = 0.92) =>
  new THREE.MeshStandardMaterial({ color, roughness: rough, metalness: 0, side: THREE.BackSide });
const solidMat = (color, rough = 0.8) =>
  new THREE.MeshStandardMaterial({ color, roughness: rough, metalness: 0.05 });

function roomLight(x, y, z, intensity = 12) {
  const l = new THREE.PointLight(0xfff0dd, intensity, 0, 2);
  l.position.set(x, y, z);
  return l;
}

// ---------------- «Комната»: богатая текстура на всех стенах ----------------
function buildRoom() {
  const g = new THREE.Group();
  // комната 8×3×8 — инвертированный бокс (порядок материалов: ±x, ±y, ±z)
  const shell = new THREE.Mesh(new THREE.BoxGeometry(8, 3, 8), [
    inMat(0x5a6680), inMat(0x5a6680),
    inMat(0x49506a), inMat(0x363d4e),
    inMat(0x636f8c), inMat(0x636f8c),
  ]);
  shell.position.y = 1.5;
  g.add(shell);
  // немного «мебели», чтобы комната читалась
  const table = new THREE.Mesh(new THREE.BoxGeometry(1.6, 0.08, 0.9), solidMat(0x8a6f55, 0.6));
  table.position.set(0, 0.75, 0);
  g.add(table);
  const shelf = new THREE.Mesh(new THREE.BoxGeometry(0.3, 2.0, 1.4), solidMat(0x4a5468));
  shelf.position.set(-3.7, 1.0, 2.4);
  g.add(shelf);
  g.add(roomLight(0, 2.7, 0, 16));

  // Фичи: равномерно на всех четырёх стенах, все «богатые» (n = null) —
  // каждая даёт полноценный bearing-вклад. 4 × 36 = 144 фичи.
  const rnd = mulberry32(7);
  const features = [];
  const walls = [
    { fix: 'x', v: 3.93 }, { fix: 'x', v: -3.93 },
    { fix: 'z', v: 3.93 }, { fix: 'z', v: -3.93 },
  ];
  for (const w of walls) {
    for (let i = 0; i < 36; i++) {
      const u = (rnd() * 2 - 1) * 3.6;
      const y = 0.3 + rnd() * 2.4;
      const pos = w.fix === 'x'
        ? new THREE.Vector3(w.v, y, u)
        : new THREE.Vector3(u, y, w.v);
      features.push({ pos, n: null });
    }
  }

  // маршрут — круг внутри комнаты, взгляд по касательной
  const route = (t) => {
    const a = t * Math.PI * 2;
    return {
      pos: new THREE.Vector3(2.3 * Math.cos(a), 1.4, 2.3 * Math.sin(a)),
      dir: new THREE.Vector3(-Math.sin(a), 0, Math.cos(a)),
    };
  };
  return { group: g, features, route, camPos: new THREE.Vector3(9.5, 7.5, 9.5) };
}

// ------- «Коридор»: однородные боковые стены, почти ничего впереди -------
function buildCorridor() {
  const g = new THREE.Group();
  // коридор 2.6×2.8×26 вдоль оси z
  const shell = new THREE.Mesh(new THREE.BoxGeometry(2.6, 2.8, 26), [
    inMat(0x5f6a84), inMat(0x5f6a84),
    inMat(0x4a5168), inMat(0x3a4154),
    inMat(0x6a7591), inMat(0x6a7591),
  ]);
  shell.position.y = 1.4;
  g.add(shell);
  for (let z = -9; z <= 9; z += 6) g.add(roomLight(0, 2.6, z, 8));

  const features = [];
  // Редкие фичи на ОДНОРОДНЫХ боковых стенах: каждая даёт информацию
  // ТОЛЬКО вдоль нормали стены (поперёк коридора, ось x). Вдоль
  // коридора (ось z) от них информации ноль — в этом вся соль сцены.
  let k = 0;
  for (let z = -12; z <= 12; z += 2.4, k++) {
    const y = (k % 2) ? 0.9 : 1.9;
    features.push({ pos: new THREE.Vector3(-1.28, y, z), n: new THREE.Vector3(1, 0, 0) });
    features.push({ pos: new THREE.Vector3(1.28, 2.8 - y, z), n: new THREE.Vector3(-1, 0, 0) });
  }
  // Однородный пол: информация только по вертикали (нормаль +y)
  for (let z = -12; z <= 12; z += 3) {
    features.push({ pos: new THREE.Vector3(((z / 3) % 2) ? 0.5 : -0.5, 0.03, z), n: new THREE.Vector3(0, 1, 0) });
  }
  // Богатый «постер» на дальнем торце — единственный источник информации
  // вдоль оси коридора; виден только в самом конце маршрута
  const poster = new THREE.Mesh(new THREE.PlaneGeometry(1.4, 1.2),
    new THREE.MeshStandardMaterial({ color: 0xc0772f, roughness: 0.7, emissive: 0x402200, emissiveIntensity: 0.4 }));
  poster.position.set(0, 1.5, 12.94);
  poster.rotation.y = Math.PI;
  g.add(poster);
  for (const [x, y] of [[-0.5, 1.1], [0.5, 1.1], [-0.5, 1.9], [0.5, 1.9]]) {
    features.push({ pos: new THREE.Vector3(x, y, 12.9), n: null });
  }

  // маршрут — прямо по коридору, взгляд вперёд
  const route = (t) => ({
    pos: new THREE.Vector3(0, 1.4, -11.5 + t * 23),
    dir: new THREE.Vector3(0, 0, 1),
  });
  return { group: g, features, route, camPos: new THREE.Vector3(7.5, 6, -15) };
}

// ---------------- «Лестница»: фичи на ступенях ----------------
function buildStairs() {
  const g = new THREE.Group();
  const N = 12, RISE = 0.17, TREAD = 0.34, W = 2.4;

  // пол перед лестницей
  const floor = new THREE.Mesh(new THREE.BoxGeometry(5, 0.1, 5), solidMat(0x3a4154));
  floor.position.set(0, -0.05, -1.5);
  g.add(floor);

  const stepMat = solidMat(0x6a7591, 0.85);
  const features = [];
  for (let i = 0; i < N; i++) {
    const top = (i + 1) * RISE;
    const zFront = i * TREAD;
    const step = new THREE.Mesh(new THREE.BoxGeometry(W, top, TREAD), stepMat);
    step.position.set(0, top / 2, zFront + TREAD / 2);
    g.add(step);
    // фичи на ступенях: передняя кромка (контрастный край) + точки на проступи —
    // «богатые», дают полный bearing-вклад
    for (const x of [-0.9, -0.3, 0.3, 0.9]) {
      features.push({ pos: new THREE.Vector3(x, top + 0.02, zFront + 0.03), n: null });
    }
    for (const x of [-0.6, 0.6]) {
      features.push({ pos: new THREE.Vector3(x, top + 0.02, zFront + 0.25), n: null });
    }
  }
  // площадка наверху
  const landTop = N * RISE;
  const landing = new THREE.Mesh(new THREE.BoxGeometry(W, landTop, 2.0), solidMat(0x5a6680));
  landing.position.set(0, landTop / 2, N * TREAD + 1.0);
  g.add(landing);
  for (const x of [-0.8, 0, 0.8]) {
    features.push({ pos: new THREE.Vector3(x, landTop + 0.02, N * TREAD + 0.6), n: null });
  }
  // стена над верхней площадкой: в конце подъёма ступени уходят вниз
  // из поля зрения — без неё видимых фич не остаётся вовсе
  const topWall = new THREE.Mesh(new THREE.PlaneGeometry(2.4, 3.2), solidMat(0x636f8c, 0.9));
  topWall.position.set(0, landTop + 1.6, N * TREAD + 2.04);
  topWall.rotation.y = Math.PI;
  g.add(topWall);
  const rnd = mulberry32(11);
  for (let i = 0; i < 12; i++) {
    features.push({
      pos: new THREE.Vector3((rnd() * 2 - 1) * 1.0, landTop + 0.4 + rnd() * 2.6, N * TREAD + 2.0),
      n: null,
    });
  }
  g.add(roomLight(0, 4.5, 2, 14));

  // маршрут — подъём вдоль пролёта; взгляд по ходу движения,
  // слегка вниз, чтобы ступени попадали в конус обзора
  const A = new THREE.Vector3(0, 1.4, -2.4);
  const B = new THREE.Vector3(0, 1.4 + landTop, N * TREAD + 1.2);
  const travel = new THREE.Vector3().subVectors(B, A).normalize();
  const look = travel.clone().add(new THREE.Vector3(0, -0.45, 0)).normalize();
  const route = (t) => ({
    pos: new THREE.Vector3().lerpVectors(A, B, t),
    dir: look.clone(),
  });
  return { group: g, features, route, camPos: new THREE.Vector3(8, 5.5, -4) };
}

export const PRESETS = {
  room:     { label: 'Комната',  build: buildRoom },
  corridor: { label: 'Коридор',  build: buildCorridor },
  stairs:   { label: 'Лестница', build: buildStairs },
};
