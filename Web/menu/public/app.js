/* FS27 — menú web. Sin dependencias. Solo interfaz: no hay conexión con el Core ni con ningún servidor. */
(() => {
  "use strict";
  const $ = (s, r = document) => r.querySelector(s);
  const $$ = (s, r = document) => Array.from(r.querySelectorAll(s));
  const root = document.documentElement;
  const body = document.body;
  const screenEl = $("#screen");
  const main = $("#main");

  const store = {
    get(k, d) { try { const v = localStorage.getItem("fs27." + k); return v === null ? d : JSON.parse(v); } catch { return d; } },
    set(k, v) { try { localStorage.setItem("fs27." + k, JSON.stringify(v)); } catch { /* sin almacenamiento: se ignora */ } },
  };

  /* Plantilla de ejemplo con nombres ficticios. */
  const SQUAD = [
    { n: 1, name: "Dario Vance", pos: "POR", r: 78, x: 8, y: 50, gk: true },
    { n: 4, name: "Iker Solano", pos: "DEF", r: 74, x: 28, y: 22 },
    { n: 5, name: "Tomás Brecha", pos: "DEF", r: 76, x: 28, y: 78 },
    { n: 8, name: "Lucho Arrieta", pos: "MED", r: 80, x: 52, y: 50 },
    { n: 10, name: "Nico Valdivia", pos: "DEL", r: 84, x: 74, y: 28 },
    { n: 9, name: "Kai Montero", pos: "DEL", r: 82, x: 74, y: 72 },
  ];
  const BENCH = [
    { n: 12, name: "Rafa Ledesma", pos: "POR", r: 70 },
    { n: 14, name: "Bruno Cairo", pos: "MED", r: 73 },
    { n: 17, name: "Teo Palmieri", pos: "DEL", r: 75 },
  ];

  const esc = (s) => String(s).replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" }[c]));
  const back = `<button type="button" class="back" data-back><i></i> VOLVER</button>`;
  const head = (crumb, title, pill, lead) => `
    <div class="screen__bar">${back}<span class="screen__crumb">Menú · ${esc(crumb)}</span></div>
    <div><span class="pill">${esc(pill)}</span></div>
    <h2 id="screen-title" tabindex="-1">${esc(title)}</h2>
    <p class="screen__lead">${lead}</p>`;

  const miniField = (players) => `
    <div class="field" role="img" aria-label="Formación de ejemplo 2-1-2 más portero">
      <svg viewBox="0 0 400 250" preserveAspectRatio="none" aria-hidden="true">
        <g fill="none" stroke="rgba(255,255,255,.55)" stroke-width="2">
          <rect x="8" y="8" width="384" height="234"/><path d="M200 8v234"/><circle cx="200" cy="125" r="32"/>
          <rect x="8" y="75" width="48" height="100"/><rect x="344" y="75" width="48" height="100"/>
        </g>
      </svg>
      ${players.map((p) => `<span class="player${p.gk ? " gk" : ""}" style="left:${p.x}%;top:${p.y}%">${p.n}<small>${esc(p.name.split(" ")[1])}</small></span>`).join("")}
    </div>`;

  const SCREENS = {
    arena() {
      return `${head("Arena", "ARENA", "MODO ARCADE", "Partidos rápidos y directos. Los muros mantienen el balón vivo, no hay faltas ni árbitro y todo se decide con regates, pases y disparos.")}
      <div class="split">
        ${miniField(SQUAD)}
        <div class="grid">
          <article class="card"><h3>Reglas</h3><ul><li>Sin faltas, tarjetas ni VAR</li><li>Balón siempre en juego gracias a los muros</li><li>Si hay empate en una competición con ganador, se define por penales arcade</li></ul></article>
          <article class="card"><h3>Penales arcade</h3><p>Eliges zona, potencia y altura; el portero elige hacia dónde lanzarse. Los atributos cuentan, no solo la suerte.</p></article>
          <button type="button" class="cta" disabled aria-disabled="true">JUGAR · NO DISPONIBLE EN LA WEB</button>
        </div>
      </div>
      <p class="note"><b>Presentación.</b> Esta pantalla solo describe el modo. Los partidos se ejecutan en el juego, no en esta web.</p>`;
    },
    classic() {
      return `${head("Classic", "CLASSIC", "MODO PRINCIPAL", "El fútbol 6 contra 6 con reglas: árbitro con criterio propio, faltas, tarjetas, tiros libres, penales y revisión por VAR.")}
      <div class="grid">
        <article class="card"><h3>Árbitro</h3><p>Cada árbitro tiene un estilo: más permisivo o más estricto. Las faltas y tarjetas salen de lo que ocurre en el campo.</p></article>
        <article class="card"><h3>Tiros libres y penales</h3><p>Eliges quién ejecuta. En los penales decides dirección, potencia y altura mientras el portero intenta anticiparse.</p></article>
        <article class="card"><h3>VAR</h3><p>Puedes pedir la revisión de un gol, una roja o un penal pagando Zenit Points. Si la decisión no cambia, el coste se cobra igual.</p></article>
        <article class="card"><h3>Definición por penales</h3><p>Orden de tiradores configurable antes de empezar. Tandas de cinco con final anticipado y muerte súbita.</p></article>
        <article class="card card--wide"><button type="button" class="cta" disabled aria-disabled="true">JUGAR · NO DISPONIBLE EN LA WEB</button></article>
      </div>
      <p class="note"><b>Aviso.</b> La mecánica de penales es una interpretación propia, no una copia de los controles de ningún otro juego. Esta web no ejecuta el Core del juego.</p>`;
    },
    equipo() {
      const row = (p) => `<li><span class="num">${p.n}</span><span class="nm">${esc(p.name)}<small>${esc(p.pos)}</small></span><span class="rating">${p.r}</span></li>`;
      return `${head("Equipo", "EQUIPO", "DATOS DE EJEMPLO", "Plantilla y formación de demostración. Los nombres son ficticios y nada de lo que ves se guarda ni afecta al juego.")}
      <div class="split">
        ${miniField(SQUAD)}
        <div>
          <ul class="roster" aria-label="Titulares de ejemplo">${SQUAD.map(row).join("")}</ul>
          <h3 style="margin:16px 0 8px;letter-spacing:.18em;font-size:13px;color:var(--muted)">SUPLENTES</h3>
          <ul class="roster" aria-label="Suplentes de ejemplo">${BENCH.map(row).join("")}</ul>
        </div>
      </div>`;
    },
    entrenamiento() {
      return `${head("Entrenamiento", "ENTRENAMIENTO", "EN DESARROLLO", "Esta sección todavía no existe. Aquí irán las sesiones para mejorar atributos de tus jugadores.")}
      <div class="grid">
        <article class="card"><h3>Estado</h3><p>En desarrollo. No hay sesiones, progresión ni recompensas todavía.</p><div class="bar" style="margin-top:12px" aria-hidden="true"><i style="--w:12%"></i></div></article>
        <article class="card"><h3>Idea</h3><p>Sesiones cortas de regate, pase y definición pensadas para jugar en el móvil.</p></article>
      </div>`;
    },
    tienda() {
      const items = [["Camiseta", "CAM"], ["Balón", "BAL"], ["Estadio", "EST"], ["Celebración", "CEL"]];
      return `${head("Tienda", "TIENDA", "VISTA PREVIA", "Escaparate provisional. No se puede comprar nada ni se procesan pagos: los artículos son solo maquetas.")}
      <div class="shop">${items.map(([n, a]) => `
        <article class="item"><div class="item__art" aria-hidden="true">${a}</div><h3>${n}</h3><span class="price">Próximamente</span>
        <button type="button" class="cta" disabled aria-disabled="true">NO DISPONIBLE</button></article>`).join("")}</div>
      <p class="note"><b>Sin pagos.</b> Esta web no tiene dinero real, cuentas ni saldo de Zenit Points conectado. El contador ZP de la cabecera es un dato de ejemplo.</p>`;
    },
    configuracion() {
      const rm = store.get("reduceMotion", false);
      const quality = store.get("quality", "alta");
      const row = (id, t, sub, on) => `<label class="setting" for="${id}"><span><b>${t}</b><small>${sub}</small></span><input class="switch" type="checkbox" id="${id}" ${on ? "checked" : ""} role="switch"></label>`;
      return `${head("Configuración", "CONFIGURACIÓN", "AJUSTES INICIALES", "Los ajustes marcados como demostración se guardan solo en este navegador y no cambian el juego.")}
      <div class="settings">
        ${row("set-motion", "Reducir animaciones", "Funciona en esta web", rm)}
        ${row("set-sound", "Sonido (demostración)", "No afecta al juego", store.get("sound", true))}
        ${row("set-vibr", "Vibración (demostración)", "No afecta al juego", store.get("vibration", true))}
        <div class="setting"><span><b>Calidad gráfica (demostración)</b><small>No afecta al juego</small></span>
          <span class="segment" role="group" aria-label="Calidad gráfica">${["baja", "media", "alta"].map((q) => `<button type="button" data-q="${q}" aria-pressed="${q === quality}">${q.toUpperCase()}</button>`).join("")}</span></div>
      </div>
      <p class="note"><b>Aviso.</b> Solo «Reducir animaciones» tiene efecto, y solo aquí. El resto de ajustes son una maqueta.</p>`;
    },
  };

  function applyMotion() { root.dataset.motion = store.get("reduceMotion", false) ? "off" : "on"; }
  applyMotion();

  function wireScreen(route) {
    $$("[data-back]", screenEl).forEach((b) => b.addEventListener("click", goHome));
    if (route !== "configuracion") return;
    $("#set-motion").addEventListener("change", (e) => { store.set("reduceMotion", e.target.checked); applyMotion(); });
    $("#set-sound").addEventListener("change", (e) => store.set("sound", e.target.checked));
    $("#set-vibr").addEventListener("change", (e) => store.set("vibration", e.target.checked));
    $$("[data-q]", screenEl).forEach((b) => b.addEventListener("click", () => {
      store.set("quality", b.dataset.q);
      $$("[data-q]", screenEl).forEach((o) => o.setAttribute("aria-pressed", String(o === b)));
    }));
  }

  let lastRoute = "";
  function goHome() { if (location.hash && location.hash !== "#/") location.hash = "#/"; else render(); }

  function render() {
    const m = /^#\/([a-z]+)/.exec(location.hash);
    const route = m && SCREENS[m[1]] ? m[1] : "";
    if (!route) {
      body.dataset.view = "menu"; screenEl.hidden = true; screenEl.innerHTML = ""; delete screenEl.dataset.theme;
      document.title = "FS27 — Menú principal";
      const tile = $(`[data-route="${lastRoute}"]`) || $(".tile");
      if (lastRoute !== "") tile.focus({ preventScroll: true }); else main.focus({ preventScroll: true });
      if (tile) selectTile(tile);
      lastRoute = "";
      return;
    }
    lastRoute = route;
    body.dataset.view = "screen";
    screenEl.hidden = false;
    screenEl.dataset.theme = route;
    screenEl.innerHTML = SCREENS[route]();
    document.title = "FS27 — " + route.charAt(0).toUpperCase() + route.slice(1);
    wireScreen(route);
    window.scrollTo(0, 0);
    $("#screen-title").focus({ preventScroll: true });
  }

  /* Navegación con teclado en el menú: flechas por posición geométrica. */
  const tiles = $$(".tile");
  function selectTile(t) { tiles.forEach((x) => x.classList.toggle("is-selected", x === t && document.activeElement === t)); }
  tiles.forEach((t) => { t.addEventListener("focus", () => selectTile(t)); t.addEventListener("blur", () => t.classList.remove("is-selected")); });

  function neighbour(from, dir) {
    const a = from.getBoundingClientRect(); const ac = [a.left + a.width / 2, a.top + a.height / 2];
    let best = null, bestScore = Infinity;
    for (const t of tiles) {
      if (t === from) continue;
      const b = t.getBoundingClientRect(); const dx = b.left + b.width / 2 - ac[0], dy = b.top + b.height / 2 - ac[1];
      const along = dir === "ArrowRight" ? dx : dir === "ArrowLeft" ? -dx : dir === "ArrowDown" ? dy : -dy;
      if (along <= 4) continue;
      const across = dir === "ArrowLeft" || dir === "ArrowRight" ? Math.abs(dy) : Math.abs(dx);
      const score = along + across * 2;
      if (score < bestScore) { bestScore = score; best = t; }
    }
    return best;
  }

  document.addEventListener("keydown", (e) => {
    if (e.defaultPrevented || e.altKey || e.ctrlKey || e.metaKey) return;
    if (body.dataset.view === "screen") {
      const tag = (document.activeElement && document.activeElement.tagName) || "";
      if (e.key === "Escape" || (e.key === "Backspace" && !/INPUT|TEXTAREA/.test(tag))) { e.preventDefault(); goHome(); }
      return;
    }
    if (!e.key.startsWith("Arrow")) return;
    const cur = tiles.includes(document.activeElement) ? document.activeElement : null;
    if (!cur) { e.preventDefault(); tiles[0].focus(); return; }
    const next = neighbour(cur, e.key);
    if (next) { e.preventDefault(); next.focus(); }
  });

  window.addEventListener("hashchange", render);
  render();
})();
