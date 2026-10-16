/** The HUD, overlays and sheets every slot shares; the canvas mounts in #game. */
export const HUD = (title: string) => `
<div id="game"></div>

    <div id="loader"><div class="box">
      <div class="title">${title.toUpperCase()}</div>
      <div class="studio">SwiftBets Originals</div>
      <div class="bar"><i></i></div>
    </div></div>

    <div id="top"><span class="pill" id="clock" title="Session time">00:00</span><span class="pill" id="rtp">RTP 96.00%</span></div>
    <div id="feature"><span>FREE SPINS</span><b id="fs-left">10</b><span>left</span><span class="x2" id="fs-mult">×2</span><span>won <b id="fs-won">R 0.00</b></span></div>

    <div id="hud">
      <div class="row">
        <div class="left">
          <div class="stat"><small>Balance</small><b id="balance">…</b></div>
          <div class="stat win"><small>Win</small><b id="win">R 0.00</b></div>
        </div>
        <div class="right">
          <button class="icon" id="btn-sound" aria-label="Sound">🔊</button>
          <button class="icon" id="btn-history" aria-label="History">🕘</button>
          <button class="icon" id="btn-rules" aria-label="Rules and pays">ⓘ</button>
        </div>
      </div>
      <div class="row">
        <div class="mid">
          <div class="stake">
            <button id="stake-down" aria-label="Lower stake">−</button>
            <div class="v"><small>STAKE</small><b id="stake">R 1.00</b></div>
            <button id="stake-up" aria-label="Raise stake">+</button>
          </div>
          <button id="spin" aria-label="Spin">SPIN<span class="count" id="auto-count"></span></button>
          <div style="display:flex;gap:8px">
            <button class="icon" id="btn-turbo" aria-label="Spin speed">⚡<span class="lvl" id="speed-level"></span></button>
            <button class="icon" id="btn-auto" aria-label="Autoplay">↻</button>
          </div>
        </div>
      </div>
    </div>

    <div id="overlay"><div class="card">
      <div class="kicker" id="ov-kicker"></div>
      <div class="amount" id="ov-amount"></div>
      <div class="sub" id="ov-sub"></div>
      <button class="go" id="ov-go"></button>
    </div></div>
    <div id="toast"></div>

    <div class="sheet" id="sheet-rules"><div class="panel"><button class="close" data-close>✕</button><div id="rules-body"></div></div></div>
    <div class="sheet" id="sheet-history"><div class="panel"><button class="close" data-close>✕</button><h2>Your recent rounds</h2><div id="history-body"></div></div></div>
    <div class="sheet" id="sheet-auto"><div class="panel"><button class="close" data-close>✕</button>
      <h2>Autoplay</h2>
      <h3>Number of spins</h3><div class="choices" id="auto-spins"></div>
      <h3>Stop if total loss reaches</h3><div class="choices" id="auto-loss"></div>
      <p class="foot">Free spins play on automatically. Autoplay stops at the loss limit, on any error, or when you tap Stop. A loss limit is required.</p>
      <button class="primary" id="auto-start">START AUTOPLAY</button>
    </div></div>
`;
