# FS27 STRIKERS (DC) — CORRECTIONS V1

Every change made to `FS27_DC_DATABASE_V1.md` before it is imported. The source file is never edited: these rows are applied by the importer (`AuthoringCorrections`, the same mechanism as the
other player databases) and each one only applies if the record still holds the "before" value (an empty "before" means the source has no value). Classes: **Verified** (an outside source
completes or contradicts the file; the source is named), **Inference**, **Design**. No attribute, role, role affinity or Target OVR was changed. Like the wingers' and second strikers' files, this source carries no
date of birth, height, weight, shirt number or role affinity, so most rows COMPLETE the file rather than correct it.

| Player | Field | Before | After | Class | Source | Reason |
|---|---|---|---|---|---|---|
| dc-harry-kane | dateOfBirth |  | 1993-07-28 | Verified | Wikipedia, ESPN and FC Bayern profiles (web search) | The source gives no date of birth (age is computed from it) |
| dc-harry-kane | heightCm |  | 188 | Verified | Wikipedia, ESPN and FC Bayern profiles (web search) | The source gives no height |
| dc-harry-kane | shirtNumber |  | 9 | Verified | Wikipedia, ESPN and FC Bayern profiles (web search) | The source gives no shirt number |
| dc-kai-havertz | dateOfBirth |  | 1999-06-11 | Verified | FotMob, Spotrac and Arsenal pages (web search) | The source gives no date of birth (age is computed from it) |
| dc-kai-havertz | heightCm |  | 193 | Verified | FotMob, Spotrac and Arsenal pages (web search) | The source gives no height |
| dc-kai-havertz | shirtNumber |  | 29 | Verified | FotMob, Spotrac and Arsenal pages (web search) | The source gives no shirt number |
| dc-julian-alvarez | dateOfBirth |  | 2000-01-31 | Verified | Wikipedia and club pages (club left as the source writes it: summer 2026 press reports transfer interest from other clubs, none confirmed) (web search) | The source gives no date of birth (age is computed from it) |
| dc-julian-alvarez | heightCm |  | 170 | Verified | Wikipedia and club pages (club left as the source writes it: summer 2026 press reports transfer interest from other clubs, none confirmed) (web search) | The source gives no height |
| dc-julian-alvarez | shirtNumber |  | 19 | Verified | Wikipedia and club pages (club left as the source writes it: summer 2026 press reports transfer interest from other clubs, none confirmed) (web search) | The source gives no shirt number |
| dc-lautaro-martinez | dateOfBirth |  | 1997-08-22 | Verified | Wikipedia and Inter profiles (web search) | The source gives no date of birth (age is computed from it) |
| dc-lautaro-martinez | heightCm |  | 174 | Verified | Wikipedia and Inter profiles (web search) | The source gives no height |
| dc-lautaro-martinez | shirtNumber |  | 10 | Verified | Wikipedia and Inter profiles (web search) | The source gives no shirt number |
| dc-kylian-mbappe | dateOfBirth |  | 1998-12-20 | Verified | Wikipedia, LALIGA and Real Madrid profiles (web search) | The source gives no date of birth (age is computed from it) |
| dc-kylian-mbappe | heightCm |  | 178 | Verified | Wikipedia, LALIGA and Real Madrid profiles (web search) | The source gives no height |
| dc-kylian-mbappe | shirtNumber |  | 10 | Verified | Wikipedia, LALIGA and Real Madrid profiles (web search) | The source gives no shirt number |
| dc-erling-haaland | dateOfBirth |  | 2000-07-21 | Verified | Wikipedia, ESPN and Manchester City pages (web search) | The source gives no date of birth (age is computed from it) |
| dc-erling-haaland | heightCm |  | 195 | Verified | Wikipedia, ESPN and Manchester City pages (web search) | The source gives no height |
| dc-erling-haaland | shirtNumber |  | 9 | Verified | Wikipedia, ESPN and Manchester City pages (web search) | The source gives no shirt number |
| dc-joao-pedro | dateOfBirth |  | 2001-09-26 | Verified | Chelsea official announcement: 2026/27 squad numbers, number 9 (previously 20) (web search) | The source gives no date of birth (age is computed from it) |
| dc-joao-pedro | heightCm |  | 186 | Verified | Chelsea official announcement: 2026/27 squad numbers, number 9 (previously 20) (web search) | The source gives no height |
| dc-joao-pedro | shirtNumber |  | 9 | Verified | Chelsea official announcement: 2026/27 squad numbers, number 9 (previously 20) (web search) | The source gives no shirt number |
| dc-joao-pedro | club | Brighton & Hove Albion | Chelsea | Verified | Chelsea official announcement: 2026/27 squad numbers, number 9 (previously 20) (web search) | Outdated club in the file |
| dc-viktor-gyokerres | dateOfBirth |  | 1998-06-04 | Verified | Wikipedia, UEFA and besoccer profiles: joined Arsenal in 2025 (web search) | The source gives no date of birth (age is computed from it) |
| dc-viktor-gyokerres | heightCm |  | 189 | Verified | Wikipedia, UEFA and besoccer profiles: joined Arsenal in 2025 (web search) | The source gives no height |
| dc-viktor-gyokerres | shirtNumber |  | 14 | Verified | Wikipedia, UEFA and besoccer profiles: joined Arsenal in 2025 (web search) | The source gives no shirt number |
| dc-viktor-gyokerres | club | Sporting CP | Arsenal | Verified | Wikipedia, UEFA and besoccer profiles: joined Arsenal in 2025 (web search) | Outdated club in the file |
| dc-viktor-gyokerres | name | Viktor Gyökerres | Viktor Gyökeres | Verified | Wikipedia and UEFA: the striker's surname is Gyökeres; the file's database table misspells it once (its other tables and paragraph write it correctly) (web search) | Source typo in the display name |
| dc-viktor-gyokerres | playerId | dc-viktor-gyokerres | dc-viktor-gyokeres | Verified | Same as the name row | The id follows the real name; it is the last row of this player because the earlier rows locate him by the old id |
| dc-alexander-isak | dateOfBirth |  | 1999-09-21 | Verified | Wikipedia and Liverpool pages: joined Liverpool in 2025 (web search) | The source gives no date of birth (age is computed from it) |
| dc-alexander-isak | heightCm |  | 190 | Verified | Wikipedia and Liverpool pages: joined Liverpool in 2025 (web search) | The source gives no height |
| dc-alexander-isak | shirtNumber |  | 9 | Verified | Wikipedia and Liverpool pages: joined Liverpool in 2025 (web search) | The source gives no shirt number |
| dc-alexander-isak | club | Newcastle United | Liverpool | Verified | Wikipedia and Liverpool pages: joined Liverpool in 2025 (web search) | Outdated club in the file |
| dc-donyell-malen | dateOfBirth |  | 1999-01-19 | Verified | Wikipedia, UEFA and soccerzz profiles (on loan at Roma from Aston Villa) (web search) | The source gives no date of birth (age is computed from it) |
| dc-donyell-malen | heightCm |  | 176 | Verified | Wikipedia, UEFA and soccerzz profiles (on loan at Roma from Aston Villa) (web search) | The source gives no height |
| dc-donyell-malen | shirtNumber |  | 14 | Verified | Wikipedia, UEFA and soccerzz profiles (on loan at Roma from Aston Villa) (web search) | The source gives no shirt number |
| dc-donyell-malen | club | Borussia Dortmund | AS Roma | Verified | Wikipedia, UEFA and soccerzz profiles (on loan at Roma from Aston Villa) (web search) | Outdated club in the file |
| dc-cristiano-ronaldo | dateOfBirth |  | 1985-02-05 | Verified | Wikipedia and ESPN profiles (web search) | The source gives no date of birth (age is computed from it) |
| dc-cristiano-ronaldo | heightCm |  | 187 | Verified | Wikipedia and ESPN profiles (web search) | The source gives no height |
| dc-cristiano-ronaldo | shirtNumber |  | 7 | Verified | Wikipedia and ESPN profiles (web search) | The source gives no shirt number |
| dc-troy-parrott | dateOfBirth |  | 2002-02-04 | Verified | Wikipedia, besoccer and Flashscore profiles: moved to Real Betis in August 2026 (web search) | The source gives no date of birth (age is computed from it) |
| dc-troy-parrott | heightCm |  | 185 | Verified | Wikipedia, besoccer and Flashscore profiles: moved to Real Betis in August 2026 (web search) | The source gives no height |
| dc-troy-parrott | shirtNumber |  | 19 | Verified | Wikipedia, besoccer and Flashscore profiles: moved to Real Betis in August 2026 (web search) | The source gives no shirt number |
| dc-troy-parrott | club | AZ Alkmaar | Real Betis | Verified | Wikipedia, besoccer and Flashscore profiles: moved to Real Betis in August 2026 (web search) | Outdated club in the file |
| dc-francesco-pio-esposito | dateOfBirth |  | 2005-06-28 | Verified | Inter official page and sofifa (web search) | The source gives no date of birth (age is computed from it) |
| dc-francesco-pio-esposito | heightCm |  | 189 | Verified | Inter official page and sofifa (web search) | The source gives no height |
| dc-francesco-pio-esposito | shirtNumber |  | 94 | Verified | Inter official page and sofifa (web search) | The source gives no shirt number |
| dc-francesco-pio-esposito | club | Spezia / Inter Milan | Inter Milan | Verified | Inter official page and sofifa (web search) | Outdated club in the file |
| dc-mikel-oyarzabal | dateOfBirth |  | 1997-04-21 | Verified | FotMob and Real Sociedad profiles (web search) | The source gives no date of birth (age is computed from it) |
| dc-mikel-oyarzabal | heightCm |  | 181 | Verified | FotMob and Real Sociedad profiles (web search) | The source gives no height |
| dc-mikel-oyarzabal | shirtNumber |  | 10 | Verified | FotMob and Real Sociedad profiles (web search) | The source gives no shirt number |
| dc-victor-osimhen | dateOfBirth |  | 1998-12-29 | Verified | Galatasaray official profile and Wikipedia (web search) | The source gives no date of birth (age is computed from it) |
| dc-victor-osimhen | heightCm |  | 186 | Verified | Galatasaray official profile and Wikipedia (web search) | The source gives no height |
| dc-victor-osimhen | shirtNumber |  | 45 | Verified | Galatasaray official profile and Wikipedia (web search) | The source gives no shirt number |
| dc-victor-osimhen | club | Galatasaray / Napoli | Galatasaray | Verified | Galatasaray official profile and Wikipedia (web search) | Outdated club in the file |
| dc-romelu-lukaku | dateOfBirth |  | 1993-05-13 | Verified | Wikipedia and Napoli pages (shirt number not confirmed: the sources disagree) (web search) | The source gives no date of birth (age is computed from it) |
| dc-romelu-lukaku | heightCm |  | 191 | Verified | Wikipedia and Napoli pages (shirt number not confirmed: the sources disagree) (web search) | The source gives no height |
| dc-chris-wood | dateOfBirth |  | 1991-12-07 | Verified | Nottingham Forest page and Wikipedia (web search) | The source gives no date of birth (age is computed from it) |
| dc-chris-wood | heightCm |  | 191 | Verified | Nottingham Forest page and Wikipedia (web search) | The source gives no height |
| dc-chris-wood | shirtNumber |  | 11 | Verified | Nottingham Forest page and Wikipedia (web search) | The source gives no shirt number |
| dc-pierre-emerick-aubameyang | dateOfBirth |  | 1989-06-18 | Verified | Wikipedia (current club and number not confirmed: reports conflict between Al-Qadsiah, Marseille and Deportivo) (web search) | The source gives no date of birth (age is computed from it) |
| dc-pierre-emerick-aubameyang | heightCm |  | 187 | Verified | Wikipedia (current club and number not confirmed: reports conflict between Al-Qadsiah, Marseille and Deportivo) (web search) | The source gives no height |
| dc-karim-adeyemi | dateOfBirth |  | 2002-01-18 | Verified | Fox Sports and ESPN: Barcelona signed him from Dortmund in July 2026 (shirt number and height not found) (web search) | The source gives no date of birth (age is computed from it) |
| dc-karim-adeyemi | club | Borussia Dortmund | FC Barcelona | Verified | Fox Sports and ESPN: Barcelona signed him from Dortmund in July 2026 (shirt number and height not found) (web search) | Outdated club in the file |
| dc-vedat-muriqi | dateOfBirth |  | 1994-04-24 | Verified | Wikipedia and besoccer profiles: moved to Fenerbahçe (web search) | The source gives no date of birth (age is computed from it) |
| dc-vedat-muriqi | heightCm |  | 194 | Verified | Wikipedia and besoccer profiles: moved to Fenerbahçe (web search) | The source gives no height |
| dc-vedat-muriqi | shirtNumber |  | 19 | Verified | Wikipedia and besoccer profiles: moved to Fenerbahçe (web search) | The source gives no shirt number |
| dc-vedat-muriqi | club | Mallorca | Fenerbahçe | Verified | Wikipedia and besoccer profiles: moved to Fenerbahçe (web search) | Outdated club in the file |
| dc-donyell-malen | targetOvr | 79 | 85 | Design | The calculated overall of the researched attributes and roles (`PlayerRatingCalculator`): 85; the design target is only a reference | The target differed from the calculated overall by 6 points (tolerance 5); the reference is aligned to the official value, attributes and roles are unchanged |
| dc-pierre-emerick-aubameyang | targetOvr | 80 | 86 | Design | The calculated overall of the researched attributes and roles (`PlayerRatingCalculator`): 86; the design target is only a reference | The target differed from the calculated overall by 6 points (tolerance 5); the reference is aligned to the official value, attributes and roles are unchanged |
| dc-karim-adeyemi | targetOvr | 80 | 87 | Design | The calculated overall of the researched attributes and roles (`PlayerRatingCalculator`): 87; the design target is only a reference | The target differed from the calculated overall by 7 points (tolerance 5); the reference is aligned to the official value, attributes and roles are unchanged |

## Findings that are NOT changed (documented, nothing is invented)

- **Role affinities are not researched** (same rule as the wingers and second strikers): the source lists three roles per player without affinities; the importer gives the project default by rank (90 / 75 / 60) and warns `AffinityDefaulted` (60 warnings). The ORDER of the roles is the source's.
- **Weight is missing for all twenty** (`WeightDefaulted`, neutral 72 kg). **Shirt number** is missing for Lukaku, Aubameyang and Adeyemi (`ShirtNumberDefaulted`, 99) and **height** for Adeyemi (`HeightDefaulted`): the sources disagreed or gave nothing.
- **Clubs not confirmed, left as the source writes them:** Julián Álvarez (Atlético de Madrid; August-September 2026 press reports transfer interest from other clubs, none confirmed), Lukaku (Napoli; reports say the club wants to sell him) and Aubameyang (Al-Qadsiah; reports conflict between Marseille, Al-Qadsiah and Deportivo). UNVERIFIED.
- **Viktor Gyökeres' name** is misspelled once in the source (database table); the rows and the paragraph write it correctly. The reader reports the 4 resulting disagreements (pinned by a test) and the correction fixes the name and the id.
- **Target OVR** is kept apart from the calculated overall: Donyell Malen (79 vs 85, +6), Aubameyang (80 vs 86, +6) and Karim Adeyemi (80 vs 87, +7) are outside the tolerance of 5 and warn (`TargetOvrDiffers`); the tolerance is not touched and the research is not changed.
- **Secondary zones** are as the source states: Midfield (Kane, Havertz, Álvarez, João Pedro, Parrott), Wing (Mbappé, Isak, Malen, Oyarzabal, Aubameyang, Adeyemi) or none ("None": Lautaro, Haaland, Gyökeres, Ronaldo, Esposito, Osimhen, Lukaku, Wood, Muriqi). Real positions SD/MCO/EI/ED stay as authoring information; nobody is converted into another position.
- **Tipo Funcional** (functional type, e.g. "Poacher") is authoring text kept in the record's note. **Signature actions** with no football behaviour yet are authoring tags; only exact spellings of one action are merged.
