# Goalkeeper AI — Stage 1 (Phase 29)

Scope: what the keeper does with the ball in his hands in open play (`Core/Goalkeeper/GoalkeeperDistribution.cs`).
Wait for a lane / play short / play long / clear, using the dynamic `PassRisk` chance discounted by the keeper's own
Kicking/Distribution, a hurry radius for rivals on top of him, and a patience that grows with his decision making
(difficulty `GoalkeeperParameters.DecisionMaking` through Command). Numbers: `GoalkeeperAiDefaults.Distribution*` (+ tuning twins);
switch `DistributionEnabled` (off = the Phase 28 behaviour, play at once to the best receiver).
Not in stage 1: goal kicks (still `TakeSetPiece`), 1v1, crosses/aerial, styles, recovery capability, fictional profiles.
Baseline comparison: `*.Keeper.DistributionEnabled=0` vs default, `Tools/Balance/fast_battery.sh`, seeds 5001–5020.
