# FS27 MCO (OFFENSIVE MIDFIELDERS) — CORRECTIONS V1

Every change made to `FS27_MCO_DATABASE_V1.md` before it is imported. The source file is never edited: these rows are applied by the importer (`AuthoringCorrections`, the same mechanism as the
other player databases) and each one only applies if the record still holds the "before" value. Classes: **Verified** (an outside source contradicts the file; the source is named),
**Inference** (concluded from the file itself), **Design** (an FS27 contract decision). No attribute, role affinity or Target OVR was changed.

| Player | Field | Before | After | Class | Source | Reason |
|---|---|---|---|---|---|---|
| mo-kevin-de-bruyne | club | Manchester City | Napoli | Verified | Napoli and ESPN profiles: joined Napoli on a free transfer in 2025 (web search) | Outdated club in the file |
| mo-kevin-de-bruyne | shirtNumber | 17 | 11 | Verified | Same profiles: wears number 11 at Napoli | The file gives his former number |
| mo-florian-wirtz | club | Bayer Leverkusen | Liverpool | Verified | Liverpool announcement and press: £116m move from Leverkusen (web search) | Outdated club in the file |
| mo-florian-wirtz | shirtNumber | 10 | 7 | Verified | Liverpool announcement: new number 7 (web search) | The file gives his former number |
| mo-bruno-fernandes | ageAsWritten | 31 | 32 | Inference | The file itself: date of birth 1994-09-08 | The stated age 31 contradicts the file's own date of birth on the reference date (2026-10-02): 32 |
| mo-bruno-fernandes | shortName | Bruno Fernandes | Fernandes | Design | FS27 contract: a short name has at most 12 characters | "Bruno Fernandes" has 15; without the correction the player cannot be imported |
| mo-gilberto-mora | shortName | Gilberto Mora | Mora | Design | FS27 contract: a short name has at most 12 characters | "Gilberto Mora" has 13; without the correction the player cannot be imported |
| mo-gilberto-mora | shirtNumber | 18 | 10 | Verified | Wikipedia and press: wears number 10 at Tijuana (web search) | The file says "18 (o dorsal formativo en Xolos)"; the reader keeps the leading number, which is not his number |
| mo-gilberto-mora | heightCm | 172 | 168 | Verified | Wikipedia: 1.68 m (web search) | The file gives an estimate ("Estimado formativo") |

## Findings that are NOT changed (documented, nothing is invented)

- **Gilberto Mora's weight (64 kg)** is marked "Estimado formativo" in the file and was not found in public profiles: kept, UNVERIFIED. He is 17 years old (born 2008-10-14).
- **Three Target OVRs are 6 below the calculated overall** (Isco 80 vs 86, De Bruyne 82 vs 88, Wirtz 81 vs 87): outside the tolerance of 5, so the importer warns (`TargetOvrDiffers`). The tolerance and the research are not touched; the game uses the calculated value.
- **Marco Reus**: the database table writes "Reus (Marco Reus)"; the reader uses the full name inside the parentheses. Club (LA Galaxy) and number (18) were verified.
- **Source inconsistencies reported by the reader** (the cards are used): the play-profile tables of Ødegaard, Musiala and Wirtz carry a malformed foot row ("PreferredFoot: Left | Left"); the database table writes the club without "FC" for Palmer and Ødegaard. A test pins exactly these.
- **Verified and unchanged:** Bellingham (Real Madrid, 5), Palmer (Chelsea, 20), Ødegaard (Arsenal, 8), Bruno Fernandes (Manchester United, 8), Musiala (Bayern, 42), Isco (Betis, 22), dates of birth.
- **Secondary zone** is Attack for all ten (as the source states); real positions SD (second striker), RW, LW, LM are authoring information and some are Wing positions beyond the one secondary zone (`PolyvalenceBeyondZone` ×6). Emergency cover (DM, CM, RW, LW) is authoring only: nobody is converted into a winger or a striker. Signature actions with no football behaviour yet are authoring tags.
