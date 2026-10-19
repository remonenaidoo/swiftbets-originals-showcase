import { Container, Graphics, Sprite, Text, type Ticker } from 'pixi.js';
import { rand, sound, Tweens, ease } from '@originals/slot-kit';
import { bootInstant, call, describe, key, times, floats } from '@originals/instant-kit';

interface WheelView { roundId: string; multiplierBp: number; win: number; detail: { segments: number; risk: string; segment: number }; balance: number | null }
interface WheelInfo { rules: { segments: number[]; risks: string[]; wheels: Record<string, number[]> } }

/** One colour per multiplier, so the legend reads at a glance: grey for nothing, then cool to hot. */
const colourFor = (bp: number) => (bp === 0 ? 0x2a2440 : bp <= 12000 ? 0x3d7bff : bp <= 15000 ? 0x22c3a6 : bp <= 20000 ? 0x9b5cff : bp <= 32000 ? 0xff8a3d : 0xffcc33);

const boot = async () => {
  const { shell, app, tex, refresh, state } = await bootInstant<WheelInfo>({
    gameId: 'sunburst-wheel', title: 'Sunburst Wheel', accent: '#ffb547', accent2: '#8f4dff', background: '#120a1e', art: ['hub.webp', 'pointer.webp'],
    verify: async (s, c, n) => { const [f] = await floats(s, c, n, 1); return `the pointer stopped on segment ${Math.floor(f * segments) + 1} of ${segments} (counting clockwise from the top), for the wheel set now.`; },
  });
  const wheels = state.info.rules.wheels;
  let segments = 20, risk = 'medium', busy = false, angle = 0;
  const table = () => wheels[`${segments}-${risk}`];

  shell.controls.innerHTML = `
    <div class="row"><div class="label">Risk</div><div class="chips" id="risks">${['low', 'medium', 'high'].map((r) => `<button class="chip${r === risk ? ' on' : ''}" data-v="${r}">${r[0].toUpperCase() + r.slice(1)}</button>`).join('')}</div></div>
    <div class="row"><div class="label">Segments</div><div class="chips" id="segs">${[10, 20, 30].map((n) => `<button class="chip${n === segments ? ' on' : ''}" data-v="${n}">${n}</button>`).join('')}</div></div>
    <div class="row" id="legend" style="font-size:13px;color:var(--muted);flex-wrap:wrap;gap:6px 12px"></div>`;
  const chips = (id: string, set: (v: string) => void) => {
    for (const b of document.querySelectorAll<HTMLElement>(`#${id} .chip`)) b.onclick = () => { if (busy) return; set(b.dataset.v!); for (const x of document.querySelectorAll(`#${id} .chip`)) x.classList.toggle('on', x === b); draw(); legend(); rules(); };
  };
  chips('risks', (v) => { risk = v; });
  chips('segs', (v) => { segments = Number(v); });
  const legend = () => {
    const counts = new Map<number, number>();
    for (const bp of table()) counts.set(bp, (counts.get(bp) ?? 0) + 1);
    document.getElementById('legend')!.innerHTML = [...counts].sort((a, b) => a[0] - b[0]).map(([bp, n]) =>
      `<span><i style="display:inline-block;width:10px;height:10px;border-radius:3px;background:#${colourFor(bp).toString(16).padStart(6, '0')};margin-right:5px"></i><b style="color:var(--ink)">${times(bp)}</b> × ${n}</span>`).join('');
  };

  const rules = () => shell.rules(`
    <h2>Sunburst Wheel</h2>
    <p>Choose a risk and how many segments the wheel has, then spin. The segment under the pointer pays its multiplier. Higher risk puts more on fewer segments.</p>
    <div class="facts"><div><b>96.00%</b><small>Return to player</small></div><div><b>${times(Math.max(...Object.values(wheels).flat()))}</b><small>Top segment</small></div><div><b>9</b><small>Wheels</small></div></div>
    <h3>This wheel: ${segments} segments, ${risk} risk</h3>
    <p>${table().map(times).join(' · ')}</p>
    <p>Every segment is equally likely, so every wheel returns exactly 96%: the average of its segments is 0.96×.</p>
    <h3>Fairness</h3>
    <p>The segment is the first float of HMAC-SHA256(server seed, "client seed:nonce:0") × segments, floored. In case of a malfunction, all pays and plays are void.</p>
    <p class="foot">18+ · Play responsibly · SwiftBets demo balance</p>`);

  // ---- Stage ----
  const tweens = new Tweens(app.ticker);
  const shade = new Graphics(), wheel = new Container(), glow = new Graphics(), labels = new Container();
  const hub = new Sprite(tex('hub.webp')), pointer = new Sprite(tex('pointer.webp'));
  hub.anchor.set(0.5); pointer.anchor.set(0.5, 0.15);
  const result = new Text({ text: '', style: { fontFamily: 'Trebuchet MS, system-ui', fontWeight: '900', fontSize: 40, fill: 0xffffff, stroke: { color: 0x1a0b2e, width: 5 }, dropShadow: { color: 0xffb547, alpha: 0.6, blur: 14, distance: 0 }, padding: 24 } });
  result.anchor.set(0.5);
  const disc = new Graphics();
  wheel.addChild(disc, labels);
  app.stage.addChild(shade, glow, wheel, hub, pointer, result);
  let R = 100, CX = 0, CY = 0, lit = -1;

  const draw = () => {
    const W = app.screen.width, H = app.screen.height;
    shade.clear().rect(0, 0, W, H).fill({ color: 0x07030f, alpha: 0.35 });
    R = Math.min(W * 0.42, (H - 140) * 0.44);
    CX = W / 2; CY = 48 + R + 8;
    wheel.position.set(CX, CY);
    const t = table(), th = (Math.PI * 2) / t.length;
    disc.clear();
    disc.circle(0, 0, R + 10).fill(0x1a1030).circle(0, 0, R + 10).stroke({ width: 4, color: 0xffcf6a });
    t.forEach((bp, i) => {
      const a0 = -Math.PI / 2 + i * th, a1 = a0 + th;
      disc.moveTo(0, 0).arc(0, 0, R, a0, a1).lineTo(0, 0).fill(i === lit ? 0xffffff : colourFor(bp));
      disc.moveTo(0, 0).lineTo(Math.cos(a0) * R, Math.sin(a0) * R).stroke({ width: 2, color: 0x120a1e, alpha: 0.9 });
    });
    for (let i = 0; i < 24; i++) { const a = (i / 24) * Math.PI * 2; disc.circle(Math.cos(a) * (R + 5), Math.sin(a) * (R + 5), 2.2).fill(0xfff1c2); }
    labels.removeChildren().forEach((c) => c.destroy());
    if (t.length <= 20) t.forEach((bp, i) => {
      if (!bp) return;
      const a = -Math.PI / 2 + (i + 0.5) * th;
      const l = new Text({ text: times(bp), style: { fontFamily: 'Trebuchet MS, system-ui', fontWeight: '900', fontSize: Math.max(10, R * (t.length <= 10 ? 0.11 : 0.075)), fill: 0xffffff, stroke: { color: 0x120a1e, width: 3 } } });
      l.anchor.set(0.5);
      l.position.set(Math.cos(a) * R * 0.72, Math.sin(a) * R * 0.72);
      l.rotation = a + Math.PI / 2;
      labels.addChild(l);
    });
    wheel.rotation = angle;
    hub.width = hub.height = R * 0.42; hub.position.set(CX, CY);
    pointer.width = R * 0.24; pointer.scale.y = pointer.scale.x; pointer.position.set(CX, CY - R - 18);
    result.position.set(CX, Math.min(app.screen.height - 26, CY + R + 34));
    result.style.fontSize = Math.round(Math.max(22, R * 0.2));
  };
  app.renderer.on('resize', draw);
  draw(); legend(); rules();

  let pulse = 0;
  app.ticker.add((t: Ticker) => { pulse += t.deltaTime * 0.05; glow.clear().circle(CX, CY, R + 26).fill({ color: 0xffb547, alpha: 0.06 + Math.sin(pulse) * 0.03 }); });

  shell.action.textContent = 'SPIN';
  shell.action.onclick = async () => {
    if (busy) return;
    busy = true;
    shell.action.disabled = true;
    shell.lock(true);
    lit = -1; result.text = '';
    sound.spin();
    try {
      const v = await call<WheelView>('instant/sunburst-wheel/play', { stake: shell.stake, choices: { segments, risk }, key: key() });
      const th = (Math.PI * 2) / segments;
      // Land the segment's middle (give or take a little) under the pointer, after several full turns.
      const target = -(v.detail.segment + 0.5 + (Math.random() - 0.5) * 0.6) * th;
      const from = angle, turns = Math.PI * 2 * 5;
      const to = from - turns - ((((from - target) % (Math.PI * 2)) + Math.PI * 2) % (Math.PI * 2));
      let last = -1;
      await tweens.to(0, 1, 3600, (k) => {
        angle = from + (to - from) * k;
        wheel.rotation = angle;
        const under = Math.floor(((((-angle) % (Math.PI * 2)) + Math.PI * 2) % (Math.PI * 2)) / th);
        if (under !== last) { last = under; pointer.rotation = 0.18; }
        pointer.rotation *= 0.8;
      }, ease.outCubic);
      angle = ((to % (Math.PI * 2)) + Math.PI * 2) % (Math.PI * 2);
      lit = v.detail.segment; draw();
      result.text = v.multiplierBp ? `${times(v.multiplierBp)} · ${rand(v.win)}` : 'No win';
      result.style.fill = v.multiplierBp ? 0xffe08a : 0xb9b3d6;
      shell.setBalance(v.balance);
      shell.setWin(v.win);
      if (v.multiplierBp) { sound.win(v.multiplierBp >= 50000 ? 3 : 1); if (v.multiplierBp >= 100000) shell.bigWin(v.win, v.multiplierBp); } else sound.stop(0);
      setTimeout(() => { lit = -1; draw(); }, 1400);
      void refresh();
    } catch (e) {
      shell.toast(describe(e));
    } finally {
      busy = false;
      shell.action.disabled = false;
      shell.lock(false);
    }
  };
};

boot().catch((e) => { document.body.innerHTML = `<p style="color:#eee;font-family:system-ui;padding:24px">${describe(e)}</p>`; });
