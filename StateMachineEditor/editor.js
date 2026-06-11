'use strict';

/* ===================== Модель ===================== */

const STORAGE_KEY = 'fsm-node-editor';

let machine = emptyMachine();
let pan = { x: 0, y: 0 };
let scale = 1;
let selection = null; // { type: 'state' | 'transition', id }
let drag = null;      // { kind: 'pan' | 'node' | 'connect', ... }

const nodeEls = new Map(); // stateId -> DOM element

function emptyMachine() {
  return { name: 'NewStateMachine', initialStateId: null, states: [], transitions: [] };
}

function uid() {
  return 'n' + Date.now().toString(36) + Math.random().toString(36).slice(2, 7);
}

function findState(id) { return machine.states.find(s => s.id === id); }
function findTransition(id) { return machine.transitions.find(t => t.id === id); }

function uniqueName(base, excludeId) {
  base = (base || '').trim() || 'State';
  let name = base, i = 2;
  while (machine.states.some(s => s.name === name && s.id !== excludeId)) {
    name = base + '_' + i++;
  }
  return name;
}

function addState(x, y) {
  const s = {
    id: uid(),
    name: uniqueName('State'),
    x: Math.round(x),
    y: Math.round(y),
    onEnter: [],
    onExit: [],
    isFinal: false,
  };
  machine.states.push(s);
  if (!machine.initialStateId) machine.initialStateId = s.id;
  selection = { type: 'state', id: s.id };
  render();
  return s;
}

function deleteState(id) {
  machine.transitions = machine.transitions.filter(t => t.from !== id && t.to !== id);
  machine.states = machine.states.filter(s => s.id !== id);
  if (machine.initialStateId === id) {
    machine.initialStateId = machine.states.length ? machine.states[0].id : null;
  }
  if (selection && selection.id === id) selection = null;
  render();
}

function addTransition(fromId, toId) {
  const t = { id: uid(), from: fromId, to: toId, event: '', condition: '', priority: 0 };
  machine.transitions.push(t);
  selection = { type: 'transition', id: t.id };
  render();
  return t;
}

function deleteTransition(id) {
  machine.transitions = machine.transitions.filter(t => t.id !== id);
  if (selection && selection.id === id) selection = null;
  render();
}

/* ===================== Сериализация ===================== */

function cleanActions(arr) {
  return (arr || []).map(a => String(a).trim()).filter(Boolean);
}

function serialize() {
  const nameOf = id => {
    const s = findState(id);
    return s ? s.name : null;
  };
  return {
    name: machine.name,
    initialState: nameOf(machine.initialStateId),
    states: machine.states.map(s => ({
      name: s.name,
      isFinal: !!s.isFinal,
      onEnter: cleanActions(s.onEnter),
      onExit: cleanActions(s.onExit),
      editor: { x: Math.round(s.x), y: Math.round(s.y) },
    })),
    transitions: machine.transitions.map(t => ({
      from: nameOf(t.from),
      to: nameOf(t.to),
      event: (t.event || '').trim(),
      condition: (t.condition || '').trim(),
      priority: t.priority | 0,
    })),
  };
}

function deserialize(data) {
  if (!data || typeof data !== 'object') throw new Error('Некорректный JSON');
  if (!Array.isArray(data.states)) throw new Error('Отсутствует массив "states"');

  const next = emptyMachine();
  next.name = typeof data.name === 'string' && data.name ? data.name : 'ImportedStateMachine';

  const idByName = new Map();
  data.states.forEach((st, i) => {
    const name = typeof st.name === 'string' && st.name ? st.name : 'State_' + (i + 1);
    const id = uid();
    idByName.set(name, id);
    next.states.push({
      id,
      name,
      x: st.editor && Number.isFinite(st.editor.x) ? st.editor.x : 80 + (i % 4) * 220,
      y: st.editor && Number.isFinite(st.editor.y) ? st.editor.y : 80 + Math.floor(i / 4) * 140,
      onEnter: cleanActions(st.onEnter),
      onExit: cleanActions(st.onExit),
      isFinal: !!st.isFinal,
    });
  });

  (Array.isArray(data.transitions) ? data.transitions : []).forEach(tr => {
    const from = idByName.get(tr.from);
    const to = idByName.get(tr.to);
    if (!from || !to) return; // переход на несуществующее состояние пропускаем
    next.transitions.push({
      id: uid(),
      from,
      to,
      event: typeof tr.event === 'string' ? tr.event : '',
      condition: typeof tr.condition === 'string' ? tr.condition : '',
      priority: Number.isFinite(tr.priority) ? tr.priority : 0,
    });
  });

  next.initialStateId =
    idByName.get(data.initialState) || (next.states.length ? next.states[0].id : null);

  machine = next;
  selection = null;
}

/* ===================== DOM ===================== */

const canvasEl = document.getElementById('canvas');
const worldEl = document.getElementById('world');
const svgEl = document.getElementById('edges');
const propsEl = document.getElementById('props-body');
const jsonEl = document.getElementById('json-preview');
const nameInput = document.getElementById('machine-name');
const fileInput = document.getElementById('file-input');

const SVG_DEFS = `
<defs>
  <marker id="arrow" viewBox="0 0 10 10" refX="9" refY="5"
          markerWidth="7" markerHeight="7" orient="auto-start-reverse">
    <path d="M0,0 L10,5 L0,10 z" fill="#8aa0b8"/>
  </marker>
  <marker id="arrow-sel" viewBox="0 0 10 10" refX="9" refY="5"
          markerWidth="7" markerHeight="7" orient="auto-start-reverse">
    <path d="M0,0 L10,5 L0,10 z" fill="#4da3ff"/>
  </marker>
</defs>`;

function escapeXml(str) {
  return String(str)
    .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;').replace(/'/g, '&apos;');
}

function toWorld(clientX, clientY) {
  const r = canvasEl.getBoundingClientRect();
  return {
    x: (clientX - r.left - pan.x) / scale,
    y: (clientY - r.top - pan.y) / scale,
  };
}

function updateWorldTransform() {
  worldEl.style.transform = `translate(${pan.x}px, ${pan.y}px) scale(${scale})`;
  canvasEl.style.backgroundSize = `${24 * scale}px ${24 * scale}px`;
  canvasEl.style.backgroundPosition = `${pan.x}px ${pan.y}px`;
}

/* ---------- узлы ---------- */

function renderNodes() {
  for (const el of nodeEls.values()) el.remove();
  nodeEls.clear();

  for (const s of machine.states) {
    const el = document.createElement('div');
    el.className = 'node';
    el.dataset.id = s.id;
    if (selection && selection.type === 'state' && selection.id === s.id) el.classList.add('selected');
    if (machine.initialStateId === s.id) el.classList.add('initial');
    el.style.left = s.x + 'px';
    el.style.top = s.y + 'px';

    const head = document.createElement('div');
    head.className = 'node-head';
    const nameSpan = document.createElement('span');
    nameSpan.className = 'node-name';
    nameSpan.textContent = s.name;
    head.appendChild(nameSpan);
    if (machine.initialStateId === s.id) {
      const b = document.createElement('span');
      b.className = 'badge initial';
      b.title = 'Начальное состояние';
      b.textContent = '▶';
      head.appendChild(b);
    }
    if (s.isFinal) {
      const b = document.createElement('span');
      b.className = 'badge final';
      b.title = 'Конечное состояние';
      b.textContent = '⏹';
      head.appendChild(b);
    }
    el.appendChild(head);

    const body = document.createElement('div');
    body.className = 'node-body';
    const enter = cleanActions(s.onEnter);
    const exit = cleanActions(s.onExit);
    for (const a of enter.slice(0, 2)) {
      const line = document.createElement('div');
      line.className = 'action-line';
      line.textContent = '↓ ' + a;
      body.appendChild(line);
    }
    for (const a of exit.slice(0, 2)) {
      const line = document.createElement('div');
      line.className = 'action-line';
      line.textContent = '↑ ' + a;
      body.appendChild(line);
    }
    if (!enter.length && !exit.length) body.textContent = '—';
    el.appendChild(body);

    const port = document.createElement('div');
    port.className = 'port';
    port.title = 'Перетащите на другой узел, чтобы создать переход';
    el.appendChild(port);

    worldEl.appendChild(el);
    nodeEls.set(s.id, el);
  }
}

/* ---------- рёбра ---------- */

function nodeRect(s) {
  const el = nodeEls.get(s.id);
  const w = el ? el.offsetWidth : 150;
  const h = el ? el.offsetHeight : 56;
  return { x: s.x, y: s.y, w, h, cx: s.x + w / 2, cy: s.y + h / 2 };
}

// Точка на границе прямоугольника по лучу из центра в сторону target
function borderPoint(rect, target) {
  const dx = target.x - rect.cx;
  const dy = target.y - rect.cy;
  if (dx === 0 && dy === 0) return { x: rect.cx, y: rect.cy - rect.h / 2 };
  const tx = dx !== 0 ? (rect.w / 2) / Math.abs(dx) : Infinity;
  const ty = dy !== 0 ? (rect.h / 2) / Math.abs(dy) : Infinity;
  const t = Math.min(tx, ty);
  return { x: rect.cx + dx * t, y: rect.cy + dy * t };
}

function edgeGeometry(t, groupIndex, groupSize) {
  const a = findState(t.from);
  const b = findState(t.to);
  const ra = nodeRect(a);

  if (t.from === t.to) {
    // петля над узлом
    const lift = 58 + groupIndex * 34;
    const x1 = ra.cx - 24, x2 = ra.cx + 24;
    const y = ra.y;
    return {
      d: `M ${x1} ${y} C ${x1 - 36} ${y - lift} ${x2 + 36} ${y - lift} ${x2} ${y}`,
      label: { x: ra.cx, y: y - lift * 0.78 },
    };
  }

  const rb = nodeRect(b);
  // нормаль в канонической ориентации пары, чтобы параллельные рёбра расходились
  const canonical = t.from < t.to;
  let dx = rb.cx - ra.cx, dy = rb.cy - ra.cy;
  if (!canonical) { dx = -dx; dy = -dy; }
  const len = Math.hypot(dx, dy) || 1;
  const nx = -dy / len, ny = dx / len;

  const off = (groupIndex - (groupSize - 1) / 2) * 56;
  const mid = {
    x: (ra.cx + rb.cx) / 2 + nx * off,
    y: (ra.cy + rb.cy) / 2 + ny * off,
  };

  const p0 = borderPoint(ra, mid);
  const p2 = borderPoint(rb, mid);
  // квадратичная кривая, проходящая через mid при t = 0.5
  const c = { x: 2 * mid.x - (p0.x + p2.x) / 2, y: 2 * mid.y - (p0.y + p2.y) / 2 };

  return {
    d: `M ${p0.x.toFixed(1)} ${p0.y.toFixed(1)} Q ${c.x.toFixed(1)} ${c.y.toFixed(1)} ${p2.x.toFixed(1)} ${p2.y.toFixed(1)}`,
    label: mid,
  };
}

function drawEdges(tempLine) {
  // группировка по неупорядоченной паре узлов
  const groups = new Map();
  for (const t of machine.transitions) {
    const key = t.from < t.to ? t.from + '|' + t.to : t.to + '|' + t.from;
    if (!groups.has(key)) groups.set(key, []);
    groups.get(key).push(t);
  }

  let html = SVG_DEFS;

  for (const list of groups.values()) {
    list.forEach((t, idx) => {
      const sel = selection && selection.type === 'transition' && selection.id === t.id;
      const geo = edgeGeometry(t, idx, list.length);
      const color = sel ? '#4da3ff' : '#8aa0b8';
      const marker = sel ? 'arrow-sel' : 'arrow';

      html += `<path class="hit" data-id="${t.id}" d="${geo.d}" fill="none" stroke="transparent" stroke-width="14"/>`;
      html += `<path d="${geo.d}" fill="none" stroke="${color}" stroke-width="${sel ? 2.2 : 1.6}" marker-end="url(#${marker})"/>`;

      const text = t.event ? t.event : '…';
      const w = Math.max(text.length * 6.6 + 12, 22);
      html += `<g class="edge-label" data-id="${t.id}">
        <rect x="${geo.label.x - w / 2}" y="${geo.label.y - 10}" width="${w}" height="18" rx="9"
              fill="#1c222c" stroke="${color}" stroke-width="1"/>
        <text x="${geo.label.x}" y="${geo.label.y + 3.5}" text-anchor="middle"
              font-size="11" fill="${sel ? '#cfe6ff' : '#aebfd2'}">${escapeXml(text)}</text>
      </g>`;
    });
  }

  if (tempLine) {
    html += `<path d="M ${tempLine.x1} ${tempLine.y1} L ${tempLine.x2} ${tempLine.y2}"
             fill="none" stroke="#4da3ff" stroke-width="1.6" stroke-dasharray="5 4" marker-end="url(#arrow-sel)"/>`;
  }

  svgEl.innerHTML = html;
}

/* ---------- панель свойств ---------- */

function field(labelText, inputEl) {
  const wrap = document.createElement('div');
  wrap.className = 'field';
  const label = document.createElement('label');
  label.textContent = labelText;
  wrap.appendChild(label);
  wrap.appendChild(inputEl);
  return wrap;
}

function checkboxRow(labelText, checked, onChange) {
  const wrap = document.createElement('div');
  wrap.className = 'checkbox-row';
  const cb = document.createElement('input');
  cb.type = 'checkbox';
  cb.checked = checked;
  cb.addEventListener('change', () => onChange(cb.checked));
  const label = document.createElement('label');
  label.textContent = labelText;
  label.addEventListener('click', () => cb.click());
  wrap.appendChild(cb);
  wrap.appendChild(label);
  return wrap;
}

function renderPanel() {
  propsEl.innerHTML = '';

  if (!selection) {
    const p = document.createElement('p');
    p.className = 'muted';
    p.textContent = machine.states.length
      ? 'Выберите состояние или переход, чтобы изменить его свойства.'
      : 'Добавьте первое состояние двойным кликом по холсту или кнопкой «＋ Состояние».';
    propsEl.appendChild(p);
    return;
  }

  if (selection.type === 'state') {
    const s = findState(selection.id);
    if (!s) { selection = null; renderPanel(); return; }

    const nameEl = document.createElement('input');
    nameEl.type = 'text';
    nameEl.value = s.name;
    nameEl.addEventListener('input', () => {
      s.name = nameEl.value;
      const el = nodeEls.get(s.id);
      if (el) el.querySelector('.node-name').textContent = s.name;
      updateJsonPreview();
    });
    nameEl.addEventListener('change', () => {
      s.name = uniqueName(nameEl.value, s.id);
      nameEl.value = s.name;
      const el = nodeEls.get(s.id);
      if (el) el.querySelector('.node-name').textContent = s.name;
      updateJsonPreview();
    });
    propsEl.appendChild(field('Имя состояния', nameEl));

    propsEl.appendChild(checkboxRow('Начальное состояние', machine.initialStateId === s.id, v => {
      machine.initialStateId = v ? s.id : null;
      render();
    }));
    propsEl.appendChild(checkboxRow('Конечное состояние', !!s.isFinal, v => {
      s.isFinal = v;
      render();
    }));

    const enterEl = document.createElement('textarea');
    enterEl.placeholder = 'одно действие на строку';
    enterEl.value = (s.onEnter || []).join('\n');
    enterEl.addEventListener('input', () => {
      s.onEnter = enterEl.value.split('\n');
      updateJsonPreview();
    });
    enterEl.addEventListener('change', () => { renderNodes(); drawEdges(); });
    propsEl.appendChild(field('Действия onEnter', enterEl));

    const exitEl = document.createElement('textarea');
    exitEl.placeholder = 'одно действие на строку';
    exitEl.value = (s.onExit || []).join('\n');
    exitEl.addEventListener('input', () => {
      s.onExit = exitEl.value.split('\n');
      updateJsonPreview();
    });
    exitEl.addEventListener('change', () => { renderNodes(); drawEdges(); });
    propsEl.appendChild(field('Действия onExit', exitEl));

    const btnRow = document.createElement('div');
    btnRow.className = 'btn-row';
    const del = document.createElement('button');
    del.className = 'danger';
    del.textContent = 'Удалить состояние';
    del.addEventListener('click', () => deleteState(s.id));
    btnRow.appendChild(del);
    propsEl.appendChild(btnRow);
    return;
  }

  // transition
  const t = findTransition(selection.id);
  if (!t) { selection = null; renderPanel(); return; }

  const route = document.createElement('div');
  route.className = 'route';
  route.textContent = `${findState(t.from).name} → ${findState(t.to).name}`;
  propsEl.appendChild(route);

  const stateSelect = (value, onChange) => {
    const sel = document.createElement('select');
    for (const s of machine.states) {
      const opt = document.createElement('option');
      opt.value = s.id;
      opt.textContent = s.name;
      if (s.id === value) opt.selected = true;
      sel.appendChild(opt);
    }
    sel.addEventListener('change', () => onChange(sel.value));
    return sel;
  };

  propsEl.appendChild(field('Из состояния', stateSelect(t.from, v => { t.from = v; render(); })));
  propsEl.appendChild(field('В состояние', stateSelect(t.to, v => { t.to = v; render(); })));

  const eventEl = document.createElement('input');
  eventEl.type = 'text';
  eventEl.placeholder = 'например: start, damage, timeout';
  eventEl.value = t.event;
  eventEl.addEventListener('input', () => {
    t.event = eventEl.value;
    drawEdges();
    updateJsonPreview();
  });
  propsEl.appendChild(field('Событие (триггер)', eventEl));

  const condEl = document.createElement('input');
  condEl.type = 'text';
  condEl.placeholder = 'например: hp > 0';
  condEl.value = t.condition;
  condEl.addEventListener('input', () => {
    t.condition = condEl.value;
    updateJsonPreview();
  });
  propsEl.appendChild(field('Условие (guard)', condEl));

  const prioEl = document.createElement('input');
  prioEl.type = 'number';
  prioEl.value = t.priority;
  prioEl.addEventListener('input', () => {
    t.priority = parseInt(prioEl.value, 10) || 0;
    updateJsonPreview();
  });
  propsEl.appendChild(field('Приоритет', prioEl));

  const btnRow = document.createElement('div');
  btnRow.className = 'btn-row';
  const swap = document.createElement('button');
  swap.textContent = '⇄ Развернуть';
  swap.addEventListener('click', () => {
    const tmp = t.from; t.from = t.to; t.to = tmp;
    render();
  });
  const del = document.createElement('button');
  del.className = 'danger';
  del.textContent = 'Удалить переход';
  del.addEventListener('click', () => deleteTransition(t.id));
  btnRow.appendChild(swap);
  btnRow.appendChild(del);
  propsEl.appendChild(btnRow);
}

/* ---------- JSON / persist ---------- */

function updateJsonPreview() {
  jsonEl.textContent = JSON.stringify(serialize(), null, 2);
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify({ machine, pan, scale }));
  } catch (e) { /* приватный режим — игнорируем */ }
}

function render() {
  nameInput.value = machine.name;
  renderNodes();
  drawEdges();
  renderPanel();
  updateJsonPreview();
}

/* ===================== Взаимодействие ===================== */

canvasEl.addEventListener('mousedown', e => {
  if (e.button !== 0 && e.button !== 1) return;

  const nodeEl = e.target.closest('.node');
  if (nodeEl && e.button === 0) {
    const id = nodeEl.dataset.id;
    if (e.target.classList.contains('port')) {
      drag = { kind: 'connect', fromId: id, x: 0, y: 0 };
    } else {
      const s = findState(id);
      const w = toWorld(e.clientX, e.clientY);
      drag = { kind: 'node', id, dx: w.x - s.x, dy: w.y - s.y, moved: false };
    }
    if (!(selection && selection.type === 'state' && selection.id === id)) {
      selection = { type: 'state', id };
      render();
    }
    e.preventDefault();
    return;
  }

  if (e.target === canvasEl || e.target === worldEl || e.button === 1) {
    drag = { kind: 'pan', startX: e.clientX - pan.x, startY: e.clientY - pan.y, moved: false };
    canvasEl.classList.add('panning');
    e.preventDefault();
  }
});

document.addEventListener('mousemove', e => {
  if (!drag) return;

  if (drag.kind === 'pan') {
    pan.x = e.clientX - drag.startX;
    pan.y = e.clientY - drag.startY;
    drag.moved = true;
    updateWorldTransform();
  } else if (drag.kind === 'node') {
    const s = findState(drag.id);
    if (!s) { drag = null; return; }
    const w = toWorld(e.clientX, e.clientY);
    s.x = Math.round(w.x - drag.dx);
    s.y = Math.round(w.y - drag.dy);
    drag.moved = true;
    const el = nodeEls.get(s.id);
    el.style.left = s.x + 'px';
    el.style.top = s.y + 'px';
    drawEdges();
  } else if (drag.kind === 'connect') {
    const from = findState(drag.fromId);
    const r = nodeRect(from);
    const w = toWorld(e.clientX, e.clientY);
    drawEdges({ x1: r.x + r.w, y1: r.cy, x2: w.x, y2: w.y });
    document.querySelectorAll('.node.connect-target').forEach(n => n.classList.remove('connect-target'));
    const over = e.target.closest && e.target.closest('.node');
    if (over) over.classList.add('connect-target');
  }
});

document.addEventListener('mouseup', e => {
  if (!drag) return;
  const d = drag;
  drag = null;
  canvasEl.classList.remove('panning');

  if (d.kind === 'connect') {
    document.querySelectorAll('.node.connect-target').forEach(n => n.classList.remove('connect-target'));
    const over = e.target.closest && e.target.closest('.node');
    if (over) {
      addTransition(d.fromId, over.dataset.id);
    } else {
      drawEdges();
    }
  } else if (d.kind === 'node' && d.moved) {
    updateJsonPreview();
  } else if (d.kind === 'pan') {
    updateJsonPreview();
    if (!d.moved && (e.target === canvasEl || e.target === worldEl)) {
      selection = null;
      render();
    }
  }
});

canvasEl.addEventListener('dblclick', e => {
  if (e.target !== canvasEl && e.target !== worldEl) return;
  const w = toWorld(e.clientX, e.clientY);
  addState(w.x - 75, w.y - 28);
});

canvasEl.addEventListener('wheel', e => {
  e.preventDefault();
  const factor = e.deltaY < 0 ? 1.1 : 1 / 1.1;
  const next = Math.min(2.5, Math.max(0.25, scale * factor));
  const r = canvasEl.getBoundingClientRect();
  const mx = e.clientX - r.left, my = e.clientY - r.top;
  // точка под курсором остаётся на месте
  pan.x = mx - ((mx - pan.x) / scale) * next;
  pan.y = my - ((my - pan.y) / scale) * next;
  scale = next;
  updateWorldTransform();
}, { passive: false });

svgEl.parentElement.addEventListener('click', e => {
  const target = e.target.closest ? e.target.closest('[data-id]') : null;
  if (target && (target.classList.contains('hit') || target.classList.contains('edge-label'))) {
    selection = { type: 'transition', id: target.dataset.id };
    render();
  }
});

document.addEventListener('keydown', e => {
  const tag = document.activeElement && document.activeElement.tagName;
  if (tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT') return;

  if ((e.key === 'Delete' || e.key === 'Backspace') && selection) {
    if (selection.type === 'state') deleteState(selection.id);
    else deleteTransition(selection.id);
    e.preventDefault();
  } else if (e.key === 'Escape') {
    selection = null;
    render();
  }
});

/* ---------- toolbar ---------- */

nameInput.addEventListener('input', () => {
  machine.name = nameInput.value;
  updateJsonPreview();
});

document.getElementById('btn-add-state').addEventListener('click', () => {
  const r = canvasEl.getBoundingClientRect();
  const w = toWorld(r.left + r.width / 2, r.top + r.height / 2);
  addState(w.x - 75 + (Math.random() * 60 - 30), w.y - 28 + (Math.random() * 60 - 30));
});

document.getElementById('btn-export').addEventListener('click', () => {
  const data = serialize();
  if (!data.initialState && data.states.length) {
    if (!confirm('Не задано начальное состояние. Экспортировать всё равно?')) return;
  }
  const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
  const a = document.createElement('a');
  a.href = URL.createObjectURL(blob);
  a.download = (machine.name || 'state-machine').replace(/[^\w.-]+/g, '_') + '.json';
  a.click();
  URL.revokeObjectURL(a.href);
});

document.getElementById('btn-copy').addEventListener('click', async e => {
  const text = JSON.stringify(serialize(), null, 2);
  try {
    await navigator.clipboard.writeText(text);
    e.target.textContent = '✓ Скопировано';
    setTimeout(() => { e.target.textContent = 'Копировать'; }, 1200);
  } catch (err) {
    window.prompt('Скопируйте JSON вручную:', text);
  }
});

document.getElementById('btn-import').addEventListener('click', () => fileInput.click());

fileInput.addEventListener('change', () => {
  const file = fileInput.files[0];
  fileInput.value = '';
  if (!file) return;
  const reader = new FileReader();
  reader.onload = () => {
    try {
      deserialize(JSON.parse(reader.result));
      pan = { x: 40, y: 40 };
      scale = 1;
      updateWorldTransform();
      render();
    } catch (err) {
      alert('Ошибка импорта: ' + err.message);
    }
  };
  reader.readAsText(file);
});

document.getElementById('btn-clear').addEventListener('click', () => {
  if (!confirm('Удалить все состояния и переходы?')) return;
  machine = emptyMachine();
  selection = null;
  render();
});

/* ===================== Старт ===================== */

function seedDemo() {
  const idle = addState(120, 180);
  idle.name = 'Idle';
  idle.onEnter = ['PlayAnimation(idle)'];
  const run = addState(460, 180);
  run.name = 'Running';
  run.onEnter = ['PlayAnimation(run)'];
  machine.initialStateId = idle.id;
  const t1 = addTransition(idle.id, run.id);
  t1.event = 'move';
  const t2 = addTransition(run.id, idle.id);
  t2.event = 'stop';
  selection = null;
}

(function init() {
  let restored = false;
  try {
    const saved = localStorage.getItem(STORAGE_KEY);
    if (saved) {
      const data = JSON.parse(saved);
      if (data.machine && Array.isArray(data.machine.states)) {
        machine = data.machine;
        pan = data.pan || pan;
        scale = data.scale || 1;
        restored = true;
      }
    }
  } catch (e) { /* битые данные — начинаем заново */ }

  if (!restored) seedDemo();
  updateWorldTransform();
  render();
})();
