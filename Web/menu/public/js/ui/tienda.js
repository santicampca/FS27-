// Tienda: cosméticos de la web que se pagan con ZP de la cartera local. Sin dinero real, sin pasarelas y sin ventajas deportivas.
// Cada compra es un movimiento idempotente de la cartera ("shop:<artículo>") y deja el artículo en el inventario local; el equipado cambia de verdad algo en esta web.
import { $, $$, esc, confirmDialog, toast } from './dom.js';
import { head, note } from './screen.js';
import { ledger, commitLedger, stampNow, inventory, saveInventory, onLedger } from '../app/state.js';
import { fx } from './fx.js';

export const CATALOG = [
  { id: 'ball-neon', slot: 'ball', name: 'Balón neón', price: 3, text: 'Un balón verde fosforescente en el visor del campo.', swatch: '#9dff2c' },
  { id: 'ball-gold', slot: 'ball', name: 'Balón dorado', price: 5, text: 'Balón dorado en el visor del campo.', swatch: '#ffd23d' },
  { id: 'trail', slot: 'trail', name: 'Estela del balón', price: 8, text: 'Una estela luminosa detrás del balón en el visor.', swatch: 'linear-gradient(90deg,transparent,#fff)' },
  { id: 'card-gold', slot: 'card', name: 'Marco dorado de tarjetas', price: 4, text: 'Marco dorado en todas las tarjetas de jugador.', swatch: '#ffc83d' },
  { id: 'card-neon', slot: 'card', name: 'Marco neón de tarjetas', price: 4, text: 'Marco neón cian en todas las tarjetas de jugador.', swatch: '#2bd9ff' },
  { id: 'pitch-night', slot: 'pitch', name: 'Césped nocturno', price: 6, text: 'Césped en tonos oscuros para el visor del campo.', swatch: '#0a3d2c' },
  { id: 'pitch-diag', slot: 'pitch', name: 'Césped en diagonal', price: 6, text: 'Franjas diagonales en el césped del visor.', swatch: 'repeating-linear-gradient(60deg,#0d6b3a 0 8px,#127f47 8px 16px)' },
];
const SLOT_ES = { ball: 'Balón', trail: 'Efecto', card: 'Tarjetas', pitch: 'Césped' };

export function render(el) {
  const draw = () => {
    el.innerHTML = head('Tienda', 'TIENDA', 'COSMÉTICOS · SIN DINERO REAL', 'Compra con Zenit Points objetos puramente estéticos. No hay pagos reales, ni suscripciones, ni ventajas deportivas.') +
      `<div class="actions"><span class="chip chip--zp"><span class="gem" aria-hidden="true"></span> <b>${ledger.balance}</b> <small>ZP</small></span><a class="btn btn--ghost" href="#/cartera">Ver mi cartera</a></div>
       <div class="shop">${CATALOG.map((it) => {
         const owned = inventory.owned.includes(it.id), equipped = inventory.equipped[it.slot] === it.id;
         return `<article class="item${owned ? ' is-owned' : ''}"><div class="item__art" style="background:${esc(it.swatch)}" aria-hidden="true"><span>${esc(SLOT_ES[it.slot])}</span></div><h3>${esc(it.name)}</h3><p>${esc(it.text)}</p>
           <span class="price">${owned ? (equipped ? 'Equipado' : 'Comprado') : it.price + ' ZP'}</span>
           ${owned ? `<button class="btn ${equipped ? 'btn--ghost' : 'btn--primary'}" data-equip="${esc(it.id)}">${equipped ? 'Quitar' : 'Equipar'}</button>` : `<button class="btn btn--primary" data-buy="${esc(it.id)}" ${ledger.balance < it.price ? 'aria-describedby="poor"' : ''}>Comprar</button>`}</article>`;
       }).join('')}</div><p id="poor" class="field-note">Si no te alcanzan los ZP, la compra se rechaza sin cobrar nada. Ganas ZP completando sesiones de <a href="#/entrenamiento">Entrenamiento</a>.</p>
       ${note('<b>Alcance:</b> los artículos y tu inventario existen solo en esta web (en este navegador). Equipados, cambian el visor del campo y las tarjetas de jugador. El juego todavía no lee tu inventario.')}`;
    $$('[data-buy]', el).forEach((b) => b.addEventListener('click', () => buy(b.dataset.buy, b)));
    $$('[data-equip]', el).forEach((b) => b.addEventListener('click', () => { const it = CATALOG.find((i) => i.id === b.dataset.equip); if (inventory.equipped[it.slot] === it.id) delete inventory.equipped[it.slot]; else inventory.equipped[it.slot] = it.id; saveInventory(); fx.click(); draw(); }));
  };
  async function buy(id, btn) {
    const it = CATALOG.find((i) => i.id === id); if (!it || inventory.owned.includes(id)) return;
    const ok = await confirmDialog({ title: 'Comprar ' + it.name, body: `<p>Cuesta <b>${it.price} ZP</b>. Tu saldo es <b>${ledger.balance} ZP</b>.</p><p>Es un objeto estético: no cambia el rendimiento de nadie.</p>`, ok: `Comprar (${it.price} ZP)` });
    if (!ok || inventory.owned.includes(id)) return;
    const r = ledger.trySpend(it.price, 'Shop', it.id, 'shop:' + it.id, stampNow());
    if (!r.ok) { toast(r.failure === 'InsufficientBalance' ? `Saldo insuficiente: te faltan ${it.price - ledger.balance} ZP. No se cobró nada.` : 'No se pudo comprar', 'bad'); return; }
    commitLedger(); inventory.owned.push(id); inventory.equipped[it.slot] = id; saveInventory(); fx.coin();
    toast(`${it.name} comprado y equipado. Saldo ${r.balanceAfter} ZP`); draw();
  }
  draw();
  return onLedger(() => { /* el saldo se vuelve a leer al redibujar */ });
}
