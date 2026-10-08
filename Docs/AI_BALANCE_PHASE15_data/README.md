# Phase 15 data

One CSV per cell (`<Mode>_<A>_vs_<B>.csv`), one row per match with both sides (176 columns; see `SideColumns` in `AiPhase15Tools.cs`). Each seed appears twice: A at home and A away.

| Folder | What | Seeds | Matches per cell | Length |
|---|---|---|---|---|
| base120 | the matrix, 120-second slices (as Phase 14) | 1001–1075 | 150 | 120 s |
| basefull | the matrix, real length (Arena 720 s, Classic 960 s) | 1001–1075 | 150 | full |
| baseadj | Novato–Amateur and Élite–Experto | 1001–1075 | 150 | full |
| s2_base | Élite / Novato vs Profesional, fresh seeds (validation) | 2001–2075 | 150 | full |
| s2_expA | the same with RiskBias 1.5 (Experiment A) | 2001–2075 | 150 | full |
| s3_expA | Experiment A on a part of S1 | 1001–1020 | 40 | full |
| human | synthetic human-like profiles vs the levels (a SIMULATION) | 6001–6030 | 60 | full |

`summaries/` has the text tables made by `tools/` (python 3, standard library only, bootstrap seed fixed); `analysis.json` in each folder has every mean, sd, median, range and 95 % CI. The probes (`probe_*.csv`) are described in the report. Reproduce a cell with `tools/run15.sh MODE A B FIRST SEEDS SECONDS OUT` after `dotnet build Tests/Core.Tests`.
