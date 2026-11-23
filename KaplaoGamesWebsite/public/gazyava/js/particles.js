/* ==========================================================================
   ХОЛСТ ЧАСТИЦ (ПУЗЫРЬКИ CO2, КРОШКИ ХРУМКИ, ИСКРЫ ВОЗНЕСЕНИЯ, ЛЁД)
   ========================================================================== */
let fxCtx = null;
let cw = 0;
let ch = 0;
const bgBubbles = [];
const burstParticles = [];
export const svgBubbleNodes = [];
let lastTs = 0;
let canvasElement = null;
let svgContainerElement = null;

export function initCanvas(canvasEl) {
  canvasElement = canvasEl || document.getElementById('fxCanvas');
  if (!canvasElement) return;
  fxCtx = canvasElement.getContext('2d');
  resizeCanvas(canvasElement);
  bgBubbles.length = 0;
  for (let i = 0; i < 38; i++) {
    bgBubbles.push({
      x: Math.random() * cw,
      y: Math.random() * ch,
      r: 1.4 + Math.random() * 3.8,
      vy: 15 + Math.random() * 32,
      wob: Math.random() * Math.PI * 2,
      alpha: 0.14 + Math.random() * 0.34
    });
  }
}

export function resizeCanvas(canvasEl) {
  const el = canvasEl || canvasElement || document.getElementById('fxCanvas');
  if (!el) return;
  canvasElement = el;
  const dpr = Math.min(window.devicePixelRatio || 1, 2);
  cw = window.innerWidth;
  ch = window.innerHeight;
  el.width = Math.floor(cw * dpr);
  el.height = Math.floor(ch * dpr);
  if (fxCtx) fxCtx.setTransform(dpr, 0, 0, dpr, 0, 0);
}

export function spawnParticles(type, x, y, count) {
  const palettes = {
    crumb: ['#f59e0b', '#fbbf24', '#d97706', '#fde68a', '#92400e'],
    gold: ['#fde047', '#fffbeb', '#fbbf24', '#fb923c'],
    ice: ['#e0f2fe', '#7dd3fc', '#38f2ff', '#ffffff']
  };
  const colors = palettes[type] || palettes.gold;

  for (let i = 0; i < count; i++) {
    const ang = Math.random() * Math.PI * 2;
    const spd = (type === 'crumb' ? 40 : 60) + Math.random() * (type === 'crumb' ? 120 : 180);
    burstParticles.push({
      x,
      y,
      vx: Math.cos(ang) * spd,
      vy: Math.sin(ang) * spd - (type === 'crumb' ? 20 : 40),
      size: (type === 'crumb' ? 2.5 : 2.8) + Math.random() * 3.6,
      rot: Math.random() * Math.PI * 2,
      vRot: (Math.random() - 0.5) * 12,
      alpha: 1,
      color: colors[Math.floor(Math.random() * colors.length)],
      life: 0,
      maxLife: 0.6 + Math.random() * 0.45,
      type
    });
  }
}

export function drawCanvas(dt, speedMult = 1.0) {
  if (!fxCtx || !canvasElement) return;
  fxCtx.clearRect(0, 0, cw, ch);

  // Фоновые поднимающиеся пузырьки
  const rgb = '56, 242, 255';
  for (let i = bgBubbles.length - 1; i >= 0; i--) {
    const b = bgBubbles[i];
    b.y -= b.vy * speedMult * dt;
    b.wob += dt * 2.2;
    b.x += Math.sin(b.wob) * 0.45;
    if (b.y < -10) {
      b.y = ch + 10;
      b.x = Math.random() * cw;
    }
    fxCtx.beginPath();
    fxCtx.arc(b.x, b.y, b.r, 0, Math.PI * 2);
    fxCtx.fillStyle = `rgba(${rgb}, ${b.alpha})`;
    fxCtx.fill();
  }

  // Частицы от комбо, хрумки и льда
  for (let i = burstParticles.length - 1; i >= 0; i--) {
    const p = burstParticles[i];
    p.life += dt;
    if (p.life >= p.maxLife) {
      burstParticles.splice(i, 1);
      continue;
    }
    p.x += p.vx * dt;
    p.y += p.vy * dt;
    p.vy += (p.type === 'crumb' ? 260 : 180) * dt;
    p.rot += p.vRot * dt;
    p.alpha = Math.max(0, 1 - p.life / p.maxLife);

    fxCtx.save();
    fxCtx.translate(p.x, p.y);
    fxCtx.rotate(p.rot);
    fxCtx.globalAlpha = p.alpha;
    fxCtx.fillStyle = p.color;

    if (p.type === 'crumb') {
      fxCtx.fillRect(-p.size / 2, -p.size / 2, p.size, p.size * 0.75);
    } else if (p.type === 'ice') {
      fxCtx.beginPath();
      fxCtx.moveTo(0, -p.size);
      fxCtx.lineTo(p.size * 0.7, p.size * 0.6);
      fxCtx.lineTo(-p.size * 0.7, p.size * 0.5);
      fxCtx.closePath();
      fxCtx.fill();
    } else {
      fxCtx.beginPath();
      fxCtx.arc(0, 0, p.size * 0.65, 0, Math.PI * 2);
      fxCtx.fill();
    }
    fxCtx.restore();
  }
}

export function initSvgBubbles(svgContainer) {
  const container = svgContainer || document.getElementById('svgBubbles');
  if (!container) return;
  svgContainerElement = container;
  container.innerHTML = '';
  svgBubbleNodes.length = 0;
  const ns = 'http://www.w3.org/2000/svg';
  for (let i = 0; i < 16; i++) {
    const c = document.createElementNS(ns, 'circle');
    c.setAttribute('r', (1.5 + Math.random() * 2.5).toFixed(1));
    c.setAttribute('fill', 'rgba(255,255,255,0.56)');
    container.appendChild(c);
    svgBubbleNodes.push({
      el: c,
      x: 38 + Math.random() * 64,
      y: 105 + Math.random() * 140,
      spd: 28 + Math.random() * 48,
      ph: Math.random() * Math.PI * 2
    });
  }
}
