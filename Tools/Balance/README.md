# Balance tools (Phase 17)

Three levels of validation. No single test run may last more than 1200 s (the scripts stop a run at 1100 s and record it as INCOMPLETE, never as passed).

1. **Level 1 – quick tests** (seconds to ~4 min): `dotnet test Tests/Core.Tests --no-build` (the whole suite) or `--filter "Name~GoalOrigin_|Name~Stamina_"` for the Phase 17 tests.
2. **Level 2 – reduced balance** (about 5 min): `Tools/Balance/fast_battery.sh OUTDIR [FS27_EXP]` plays 6 cells x 20 full-length matches with fixed seeds; `python3 -I Tools/Balance/compare_battery.py OUTDIR --reference Docs/AI_BALANCE_PHASE17_data/fast_reference.json` prints the summary and flags what left the bands. 20 matches per cell give wide bands: it is an alarm, not proof.
3. **Level 3 – extended validation** (8 min per cell): the explicit test `Phase15Battery_WriteTheCsv` with `BAT_MODE`, `BAT_A`, `BAT_B`, `BAT_FIRST`, `BAT_SEEDS`, `BAT_SECONDS=full`, `BAT_OUT` (and `FS27_EXP` for a one-variable experiment), cell by cell, so that no run goes over the limit. Writes the matches CSV plus `_shots.csv` and `_goals.csv`.

Always `dotnet build Tests/Core.Tests` before and do not rebuild while a battery is running.
