# Referees system (V1: data only)

## Purpose
Prepare five famous referees as **data** for a future gameplay phase. Nothing in the game reads them today: no fouls, cards, penalties, advantage, VAR, animations, AI or in-match behaviour, and no influence on players, coaches or the GRL.

## Contract (`Assets/_Project/Scripts/Core/Referees/`)
| Type | Role |
|---|---|
| `RefereeDefinition` | `RefereeId`, `Name`, `FullName`, `ShortName`, `NationalityCode`, `StyleId`, `Profile`, `Description`, `Notes`. |
| `RefereeProfile` | Seven values, all 0–100: Strictness, Cards, Advantage, GameControl, PenaltySensitivity, Consistency, Communication. |
| `RefereeStyleDefinition` | A style as data (`StyleId`, `Name`, `Description`). A new style is a new entry in the data file, not a code change. |
| `RefereeDataRecord` | Flat authoring/JSON record (values nullable: "absent" is never 0). |
| `RefereeRecordImporter` | Validates records (id, names, nationality via the existing `NationalityCodes`, style exists, values present and within 0–100) and reports issues by code; invalid records are not added. |
| `RefereeDatabase` | Styles and referees, found by id; repeated ids refused. |
| `RefereeJson` | Strict reader/writer, schema `FS27.Referees.v1`. |

Referees have **no** grade, formations, CoachBoost or CompatibilityScore. No other Core type references the referee types (guarded by a test).

Data file: `Docs/authoring/FS27_REFEREES_V1.json`. Nationality codes reuse `NationalityCodes`; `POL` (Poland) and `ROU` (Romania) were added to that table because they did not exist.

## The five referees (profile: Strictness / Cards / Advantage / GameControl / PenaltySensitivity / Consistency / Communication)
| Id | Nationality | Style | Profile |
|---|---|---|---|
| `referee-szymon-marciniak` | POL | BalancedControl | 55 / 50 / 80 / 95 / 55 / 90 / 90 |
| `referee-michael-oliver` | ENG | PermissiveFlow | 45 / 45 / 90 / 88 / 50 / 88 / 85 |
| `referee-clement-turpin` | FRA | TechnicalBalanced | 55 / 55 / 75 / 92 / 55 / 92 / 88 |
| `referee-danny-makkelie` | NED | DisciplinaryControl | 70 / 70 / 60 / 92 / 55 / 88 / 85 |
| `referee-istvan-kovacs` | ROU | AuthorityIntensity | 68 / 65 / 60 / 92 / 60 / 88 / 85 |

## Styles
BalancedControl (balanced control and authority), PermissiveFlow (permissive, favours continuity), TechnicalBalanced (technical and balanced), DisciplinaryControl (disciplinary, interventionist), AuthorityIntensity (authority and intensity).

## Authoring values and verification
Each referee carries notes with a kind: `Verified` (checked against reliable sources: birth date/place, FIFA/UEFA status, major tournaments), `Unverified` (looked for, not found) or `AuthoringInference` / `Authoring` (style, profile values and descriptions are design values supplied by the project, not statistics).
Not verified: Oliver's and Kovács's 2026 World Cup appointments.

## What remains for gameplay (not done)
Everything behavioural: how the profile influences fouls, cards, advantage, penalties, consistency of decisions and communication; match assignment; any use of styles. Those need their own design and phase.
