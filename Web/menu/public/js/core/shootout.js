// Cuenta de una tanda de penales: copia de PenaltyShootoutState del Core (Assets/_Project/Scripts/Core/Match/PenaltyShootout.cs).
// Solo cuenta (turnos, goles, final anticipado, muerte súbita). Las pruebas la comparan con secuencias generadas por el Core.
export class Shootout {
  constructor(kicksPerTeam, suddenDeath, firstTeam) {
    this.kicksPerTeam = Math.max(1, kicksPerTeam); this.suddenDeath = !!suddenDeath; this.firstTeam = firstTeam;
    this.kicks = []; this.taken = { Home: 0, Away: 0 }; this.goals = { Home: 0, Away: 0 };
    this.isDecided = false; this.hasWinner = false; this.winner = '';
  }
  static other(t) { return t === 'Home' ? 'Away' : 'Home'; }
  get nextTeam() { return this.taken[this.firstTeam] <= this.taken[Shootout.other(this.firstTeam)] ? this.firstTeam : Shootout.other(this.firstTeam); }
  get round() { return Math.min(this.taken.Home, this.taken.Away); }
  get inSuddenDeath() { return this.taken.Home >= this.kicksPerTeam && this.taken.Away >= this.kicksPerTeam && this.goals.Home === this.goals.Away; }
  misses(t) { return this.taken[t] - this.goals[t]; }

  recordKick(scored) {
    if (this.isDecided) return false;
    const team = this.nextTeam;
    this.kicks.push({ round: this.round, team, scored: !!scored });
    this.taken[team]++; if (scored) this.goals[team]++;
    this.#evaluate();
    return true;
  }

  #decide(w) { this.isDecided = true; this.hasWinner = true; this.winner = w; }

  #evaluate() {
    const k = this.kicksPerTeam, remH = Math.max(0, k - this.taken.Home), remA = Math.max(0, k - this.taken.Away);
    if (this.taken.Home <= k && this.taken.Away <= k && (remH > 0 || remA > 0)) {
      if (this.goals.Home > this.goals.Away + remA) { this.#decide('Home'); return; }
      if (this.goals.Away > this.goals.Home + remH) { this.#decide('Away'); return; }
      return;
    }
    if (this.taken.Home === this.taken.Away && this.taken.Home >= k) {
      if (this.goals.Home !== this.goals.Away) { this.#decide(this.goals.Home > this.goals.Away ? 'Home' : 'Away'); return; }
      if (!this.suddenDeath) { this.isDecided = true; this.hasWinner = false; }
    }
  }
}
