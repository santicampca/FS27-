# FS27 SECOND STRIKERS (SD) — CORRECTIONS V1

Every change made to `FS27_SD_DATABASE_V1.md` before it is imported. The source file is never edited: these rows are applied by the importer (`AuthoringCorrections`, the same mechanism as the
other player databases) and each one only applies if the record still holds the "before" value (an empty "before" means the source has no value). Classes: **Verified** (an outside source
completes or contradicts the file; the source is named), **Inference**, **Design**. No attribute, role, role affinity or Target OVR was changed. Like the wingers' file, this source carries no
date of birth, height, weight, shirt number or role affinity, so most rows COMPLETE the file rather than correct it.

| Player | Field | Before | After | Class | Source | Reason |
|---|---|---|---|---|---|---|
| sd-paulo-dybala | dateOfBirth |  | 1993-11-15 | Verified | Wikipedia, ESPN and Roma profiles (web search) | The source gives no date of birth (age is computed from it) |
| sd-paulo-dybala | heightCm |  | 177 | Verified | Wikipedia, ESPN and Roma profiles (web search) | The source gives no height |
| sd-paulo-dybala | shirtNumber |  | 21 | Verified | Wikipedia, ESPN and Roma profiles (web search) | The source gives no shirt number |
| sd-lionel-messi | dateOfBirth |  | 1987-06-24 | Verified | Wikipedia and Inter Miami profiles (web search) | The source gives no date of birth (age is computed from it) |
| sd-lionel-messi | heightCm |  | 170 | Verified | Wikipedia and Inter Miami profiles (web search) | The source gives no height |
| sd-lionel-messi | shirtNumber |  | 10 | Verified | Wikipedia and Inter Miami profiles (web search) | The source gives no shirt number |
| sd-michael-olise | dateOfBirth |  | 2001-12-12 | Verified | Bundesliga and FC Bayern pages (web search) | The source gives no date of birth (age is computed from it) |
| sd-michael-olise | shirtNumber |  | 17 | Verified | Bundesliga and FC Bayern pages (web search) | The source gives no shirt number |
| sd-lamine-yamal | dateOfBirth |  | 2007-07-13 | Verified | Wikipedia and FC Barcelona profile (web search) | The source gives no date of birth (age is computed from it) |
| sd-lamine-yamal | heightCm |  | 178 | Verified | Wikipedia and FC Barcelona profile (web search) | The source gives no height |
| sd-lamine-yamal | shirtNumber |  | 10 | Verified | Wikipedia and FC Barcelona profile (web search) | The source gives no shirt number |
| sd-morgan-rogers | dateOfBirth |  | 2002-07-26 | Verified | Chelsea official announcement and Al Jazeera: record British transfer from Aston Villa in July 2026, number 17 (web search) | The source gives no date of birth (age is computed from it) |
| sd-morgan-rogers | heightCm |  | 187 | Verified | Chelsea official announcement and Al Jazeera: record British transfer from Aston Villa in July 2026, number 17 (web search) | The source gives no height |
| sd-morgan-rogers | shirtNumber |  | 17 | Verified | Chelsea official announcement and Al Jazeera: record British transfer from Aston Villa in July 2026, number 17 (web search) | The source gives no shirt number |
| sd-morgan-rogers | club | Aston Villa | Chelsea | Verified | Chelsea official announcement and Al Jazeera: record British transfer from Aston Villa in July 2026, number 17 (web search) | Outdated club in the file |
| sd-fermin-lopez | dateOfBirth |  | 2003-05-11 | Verified | Wikipedia and LALIGA profiles (web search) | The source gives no date of birth (age is computed from it) |
| sd-fermin-lopez | heightCm |  | 176 | Verified | Wikipedia and LALIGA profiles (web search) | The source gives no height |
| sd-fermin-lopez | shirtNumber |  | 7 | Verified | Wikipedia and LALIGA profiles (web search) | The source gives no shirt number |
| sd-raphinha | dateOfBirth |  | 1996-12-14 | Verified | Wikipedia and FC Barcelona profile (web search) | The source gives no date of birth (age is computed from it) |
| sd-raphinha | heightCm |  | 176 | Verified | Wikipedia and FC Barcelona profile (web search) | The source gives no height |
| sd-raphinha | shirtNumber |  | 11 | Verified | Wikipedia and FC Barcelona profile (web search) | The source gives no shirt number |
| sd-neymar-jr | dateOfBirth |  | 1992-02-05 | Verified | Wikipedia, besoccer and playmakerstats profiles: plays for Santos (captain) (web search) | The source gives no date of birth (age is computed from it) |
| sd-neymar-jr | heightCm |  | 175 | Verified | Wikipedia, besoccer and playmakerstats profiles: plays for Santos (captain) (web search) | The source gives no height |
| sd-neymar-jr | shirtNumber |  | 10 | Verified | Wikipedia, besoccer and playmakerstats profiles: plays for Santos (captain) (web search) | The source gives no shirt number |
| sd-neymar-jr | club | Al-Hilal | Santos | Verified | Wikipedia, besoccer and playmakerstats profiles: plays for Santos (captain) (web search) | Outdated club in the file |
| sd-khvicha-kvaratskhelia | dateOfBirth |  | 2001-02-12 | Verified | PSG and ESPN profiles (6 ft = 183 cm); the file lists a former club too (web search) | The source gives no date of birth (age is computed from it) |
| sd-khvicha-kvaratskhelia | heightCm |  | 183 | Verified | PSG and ESPN profiles (6 ft = 183 cm); the file lists a former club too (web search) | The source gives no height |
| sd-khvicha-kvaratskhelia | shirtNumber |  | 7 | Verified | PSG and ESPN profiles (6 ft = 183 cm); the file lists a former club too (web search) | The source gives no shirt number |
| sd-khvicha-kvaratskhelia | club | Paris Saint-Germain / Napoli | Paris Saint-Germain | Verified | PSG and ESPN profiles (6 ft = 183 cm); the file lists a former club too (web search) | Outdated club in the file |
| sd-ousmane-dembele | dateOfBirth |  | 1997-05-15 | Verified | PSG and Wikipedia profiles (web search) | The source gives no date of birth (age is computed from it) |
| sd-ousmane-dembele | heightCm |  | 178 | Verified | PSG and Wikipedia profiles (web search) | The source gives no height |
| sd-ousmane-dembele | shirtNumber |  | 10 | Verified | PSG and Wikipedia profiles (web search) | The source gives no shirt number |
| sd-lionel-messi | targetOvr | 83 | 90 | Design | The calculated overall of the researched attributes and roles (`PlayerRatingCalculator`): 90; the design target is only a reference | The target differed from the calculated overall by 7 points (tolerance 5); the reference is aligned to the official value, attributes and roles are unchanged |
| sd-neymar-jr | targetOvr | 80 | 89 | Design | The calculated overall of the researched attributes and roles (`PlayerRatingCalculator`): 89; the design target is only a reference | The target differed from the calculated overall by 9 points (tolerance 5); the reference is aligned to the official value, attributes and roles are unchanged |

## Findings that are NOT changed (documented, nothing is invented)

- **Role affinities are not researched** (same situation and same rule as the wingers): the source lists three roles per player without affinities; the importer gives the project default by rank (90 / 75 / 60) and warns `AffinityDefaulted` (30 warnings). The ORDER of the roles is the source's.
- **Weight is missing for all ten** (`WeightDefaulted`, neutral 72 kg) and **height for Michael Olise** (`HeightDefaulted`, neutral 178 cm): public profiles did not give them. Ages come from the dates of birth above.
- **Ousmane Dembélé's foot** is written "Both" in the file: `PreferredFootAmbiguous` (the default Right is kept and it is never turned into Left, the project rule).
- **Target OVR** is kept apart from the calculated overall: **Lionel Messi** (target 83, calculated 90, +7) and **Neymar** (target 80, calculated 89, +9) are outside the tolerance of 5 and warn (`TargetOvrDiffers`); the tolerance is not touched and the research is not changed.
- **Secondary zones** are Midfield (Dybala, Messi, Rogers, Fermín López) or Wing (Olise, Yamal, Raphinha, Neymar, Kvaratskhelia, Dembélé), as the source states. Real positions ED/EI are Wing and MCO/MC are Midfield: those beyond the one secondary zone stay as authoring information (`PolyvalenceBeyondZone` ×4); nobody is converted into a winger or a midfielder.
- **Signature actions** with no football behaviour yet are authoring tags; only exact spellings of one action are merged (ProgressivePassing = ProgressivePass, StrikingFromDistance = LongRangeStrike...); none is forced onto an existing behaviour.
- **Identity checks**: all ten names, nations and clubs agree between the tables and the paragraphs (no reader problem).
