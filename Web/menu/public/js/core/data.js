// Carga y consulta de los datos exportados por el Core (public/data/*.json). Nada se inventa en la web: si falta un dato se dice.
const cache = new Map();
const base = new URL('../../data/', import.meta.url);

async function getJson(path) {
  if (!cache.has(path)) cache.set(path, fetch(new URL(path, base)).then((r) => { if (!r.ok) throw new Error(path + ': HTTP ' + r.status); return r.json(); }).catch((e) => { cache.delete(path); throw e; }));
  return cache.get(path);
}

export const loadData = () => getJson('fs27-data.json').then(index);
export const loadLab = () => getJson('fs27-penalty-lab.json');
export const loadScenarios = () => getJson('fs27-var-scenarios.json');
export const loadTraceIndex = () => getJson('traces/index.json');
export const loadTrace = (file) => getJson(file);

function index(d) {
  if (d.__indexed) return d;
  d.playersById = new Map(d.players.map((p) => [p.id, p]));
  d.teamsById = new Map(d.teams.map((t) => [t.id, t]));
  d.rolesById = new Map(d.roles.map((r) => [r.id, r]));
  d.teamOfPlayer = new Map();
  for (const t of d.teams) for (const id of [...t.active, ...t.bench]) d.teamOfPlayer.set(id, t.id);
  d.__indexed = true;
  return d;
}

export const ATTRIBUTES = [
  ['Speed', 'Velocidad'], ['Acceleration', 'Aceleración'], ['Stamina', 'Resistencia'], ['Control', 'Control'], ['Passing', 'Pase'], ['Shooting', 'Disparo'],
  ['Finishing', 'Definición'], ['Dribbling', 'Regate'], ['Technique', 'Técnica'], ['Defense', 'Defensa'], ['Strength', 'Fuerza'], ['Agility', 'Agilidad'],
];
export const KEEPER_CAPS = [
  ['Reflexes', 'Reflejos'], ['Handling', 'Manos'], ['Positioning', 'Colocación'], ['Diving', 'Estirada'], ['Kicking', 'Saque largo'], ['Distribution', 'Distribución'], ['Command', 'Mando'], ['Recovery', 'Recuperación'],
];
export const LINE_ES = { Goalkeeper: 'Portero', Defender: 'Defensa', Midfielder: 'Medio', Forward: 'Delantero' };
export const GROUP_ES = { Defense: 'Defensa', Creation: 'Construcción', Mobility: 'Energía y ataque', Attack: 'Finalización' };
export const KEEPER_STYLE_ES = { ShotStopper: 'Parador', Distributor: 'Distribuidor', Sweeper: 'Líbero', Commander: 'Comandante' };
export const LEVEL_ES = { Elite: 'Élite', Strong: 'Fuerte', Balanced: 'Equilibrado', Modest: 'Modesto' };

export const roleName = (data, id) => (data.rolesById.get(id) || { name: id }).name;
export const teamOf = (data, playerId) => data.teamsById.get(data.teamOfPlayer.get(playerId)) || null;
