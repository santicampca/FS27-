# FS27 HYBRID MIDFIELDERS (MDI / MDD) — CORRECTIONS V1

Every change made to `FS27_HYBRID_MIDFIELDERS_DATABASE_V1.md` before it is imported. The source file is never edited: these rows are applied by the importer (`AuthoringCorrections`,
the same mechanism as the other player databases) and each one only applies if the record still holds the "before" value. Classes: **Verified** (an outside source contradicts the
file; the source is named), **Inference** (concluded from the file itself), **Design** (an FS27 contract decision). No attribute, role affinity or Target OVR was changed.

| Player | Field | Before | After | Class | Source | Reason |
|---|---|---|---|---|---|---|
| md-joshua-kimmich | dateOfBirth | 1995-01-18 | 1995-02-08 | Verified | Wikipedia, UEFA and Bayern profiles: born 8 February 1995 (web search) | Wrong day and month in the file; the stated age (31) is unchanged |
| md-valentin-barco | club | Brighton & Hove Albion (en préstamo/seguimiento) | Chelsea | Verified | Chelsea official announcement: signed from Strasbourg in August 2026 (web search) | Outdated club and not a single club in the file |
| md-valentin-barco | shirtNumber | 19 | 4 | Verified | Chelsea official profile: wears number 4 (web search) | The file gives a number from a previous club |
| md-valentin-barco | bodyTypeLabel | Agile / Light | Light | Inference | The file itself: 172 cm, 68 kg, "Constitución ligera" style description; "Light" is one of the five official body types | Two labels in one cell; the official single label is used so the body type is not left to the default |
| md-oleksandr-zinchenko | club | Arsenal FC | Ajax | Verified | Arsenal and Ajax announcements: permanent transfer to Ajax on 1 February 2026 (web search; the Ajax contract ran to 30 June 2026, the current club after that was not found) | Outdated club in the file. His shirt number at Ajax was not confirmed, so the source value 35 (his Arsenal number) is kept and flagged as unverified |
| md-myles-lewis-skelly | ageAsWritten | 19 | 20 | Verified | Arsenal profile: born 26 September 2006 (web search); the file itself gives that date of birth | The stated age 19 contradicts the file's own date of birth on the reference date (2026-10-02): 20 |
| md-nico-o-reilly | shirtNumber | 75 | 33 | Verified | Manchester City announcement: number 33 from the 2025/26 season (web search) | The file gives his former number |
| md-nathan-brown | name | Nathan Brown | Nathaniel Brown | Verified | FC Bayern München first-team profile and UEFA player profile: the player is Nathaniel Brown (web search) | The card names him "Nathan Brown"; the name shown in the game is his real name |
| md-nathan-brown | fullName | Nathan Brown | Nathaniel Christopher Brown | Verified | UEFA player profile: Nathaniel Christopher "Nene" Brown (web search) | Full name completed from the official profile |
| md-nathan-brown | nationality | England | Germany | Verified | FC Bayern profile (German/American) and UEFA profile: Germany international, 10 caps (web search) | The card says England; he plays for Germany |
| md-nathan-brown | dateOfBirth | 2004-05-12 | 2003-06-16 | Verified | FC Bayern profile and UEFA profile: born 16 June 2003 (web search) | Wrong date in the card |
| md-nathan-brown | ageAsWritten | 22 | 23 | Verified | Same profiles; 23 on the reference date (2026-10-02) | Age follows the verified date of birth |
| md-nathan-brown | heightCm | 180 | 176 | Verified | FC Bayern first-team profile: 1.76 m (web search) | Height in the card does not match the official profile |
| md-nathan-brown | club | Club de desarrollo / EFL | FC Bayern München | Verified | FC Bayern announcement and profile: joined from Eintracht Frankfurt on 3 July 2026, contract to 2031 (web search) | Placeholder text in the card; real current club |
| md-nathan-brown | playerId | md-nathan-brown | md-nathaniel-brown | Verified | Same as the name row | The id follows the real name; it is the last row of this player because the earlier rows locate him by the old id |

## Findings that are NOT changed (documented, nothing is invented)

- **"Nathan Brown" is Nathaniel Brown (FC Bayern München).** The project owner identified him; FC Bayern and UEFA confirm: German international, born 16 June 2003, 1.76 m, number 11
  (the card's number was already right), joined Bayern on 3 July 2026. His identity corrections are in the table above. He stays one of the 12 hybrids (MDI) and nothing else changes:
  attributes, roles, Target OVR, zones and the card's natural position (LM / CM) are the research's. His weight (73 kg) was not found in the official profiles and stays UNVERIFIED.
  Bayern lists him as a defender (left-back) who also plays left midfield; the research's LM / CM natural position is kept as authored, not rewritten.
- **Barco, summary table**: the summary says natural position "LB / LWB" while his card and polyvalence say LB, with LM / LWB as secondary real positions. The card is used; the reader
  reports the table disagreement and the test pins it.
- **Real positions beyond the one secondary zone** (LWB, RWB, RB are Wing positions; Zinchenko, Hall, James, Kimmich and Caicedo have Defense as their secondary zone) stay in the
  authoring record as `PolyvalenceBeyondZone` warnings; no second zone is invented and nobody is converted into a full-back or a winger.
- **Signature actions** with no football behaviour yet (Inside Movement, Support Run, Pressing...) are authoring tags (warning `SignatureActionsPending`), never code.
- **Target OVR** is kept apart from the calculated overall; every player is inside the tolerance of 5.
