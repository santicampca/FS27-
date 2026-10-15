# FS27 MCD (DEFENSIVE MIDFIELDERS) — CORRECTIONS V1

Every change made to `FS27_MCD_DATABASE_V1.md` before it is imported. The source file is never edited: these rows are applied by the importer (`AuthoringCorrections`, the same mechanism as the
other player databases) and each one only applies if the record still holds the "before" value. Classes: **Verified** (an outside source contradicts the file; the source is named),
**Inference** (concluded from the file itself), **Design** (an FS27 contract decision). No attribute, role affinity or Target OVR was changed.

| Player | Field | Before | After | Class | Source | Reason |
|---|---|---|---|---|---|---|
| mc-bruno-guimaraes | shortName | Bruno Guimarães | Guimarães | Design | FS27 contract: a short name has at most 12 characters | "Bruno Guimarães" has 15; without the correction the player cannot be imported |
| mc-joao-neves | ageAsWritten | 21 | 22 | Inference | The file itself: date of birth 2004-09-27 | The stated age 21 contradicts the file's own date of birth on the reference date (2026-10-02): 22 |
| mc-joao-neves | bodyTypeLabel | Agile / Light | Light | Inference | The file itself: 174 cm, 67 kg; "Light" is one of the five official body types | Two labels in one cell; the official single label is used so the body type is not left to the default (same rule as the hybrid midfielders) |
| mc-elliot-anderson | nationality | Scotland / England | England | Verified | Manchester City and Wikipedia profiles: English, plays for the England national team (web search) | The file lists two nationalities (and the code "SCO / ENG"); one is required |
| mc-elliot-anderson | shirtNumber | 26 | 5 | Verified | Manchester City squad-number announcement for 2026/27 (web search) | The file gives a number from a previous club |
| mc-leandro-paredes | club | AS Roma / Boca Juniors | Boca Juniors | Verified | Boca Juniors profiles: plays for Boca, number 5 and captain (web search) | The file lists a former club too; the current one is Boca Juniors |

## Findings that are NOT changed (documented, nothing is invented)

- **Source inconsistencies reported by the reader** (the cards are used): the Elliot Anderson card writes the nationality code as "SCO / ENG", his play-profile row as "PreferredFoot: Left | Left" (a malformed row, the identity line says Left), and the database table says nation "England" while the card says "Scotland / England"; the database table says club "Liverpool" for Gravenberch (card: Liverpool FC) and "AS Roma / Boca" for Paredes (card: AS Roma / Boca Juniors). A test pins exactly these.
- **Stray citation labels** inside some Identity blocks ("The National News", "www.transfermarkt.us", "Major League Soccer", "FIFA") are kept in the source file and ignored by the reader.
- **Verified and unchanged:** Rodri (Manchester City, 16), Vitinha, De Jong, Joao Neves, Çalhanoğlu (Inter, contract to June 2027), Gravenberch, Bruno Guimarães, Casemiro (Inter Miami, 5, signed July 2026), dates of birth.
- **Emergency cover** (CB, AM, LM) and the functional versatility text are authoring information only: nobody is converted into a centre-back or a winger, and the one secondary zone is the one the source states (Defense, or Wing for Vitinha and Anderson).
- **Signature actions** with no football behaviour yet are authoring tags, never code. **Target OVR** is kept apart from the calculated overall; everyone is inside the tolerance of 5.
