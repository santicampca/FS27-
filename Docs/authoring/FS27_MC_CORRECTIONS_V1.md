# FS27 MC (CENTRAL MIDFIELDERS) — CORRECTIONS V1

Every change made to `FS27_MC_DATABASE_V1.md` before it is imported. The source file is never edited: these rows are applied by the importer (`AuthoringCorrections`, the same mechanism as the
other player databases) and each one only applies if the record still holds the "before" value. Classes: **Verified** (an outside source contradicts the file; the source is named),
**Inference** (concluded from the file itself), **Design** (an FS27 contract decision). No attribute, role affinity or Target OVR was changed.

| Player | Field | Before | After | Class | Source | Reason |
|---|---|---|---|---|---|---|
| mq-luka-modric | club | Real Madrid | AC Milan | Verified | AC Milan official statement of 23 July 2026 (contract extended to 30 June 2027) and press (web search) | Outdated club in the file |
| mq-luka-modric | shirtNumber | 10 | 14 | Verified | AC Milan profile and press: wears number 14 (web search) | The file gives his former Real Madrid number |
| mq-luka-modric | ageAsWritten | 40 | 41 | Inference | The file itself: date of birth 1985-09-09 | The stated age 40 contradicts the file's own date of birth on the reference date (2026-10-02): 41 |
| mq-scott-mctominay | shirtNumber | 39 | 8 | Verified | SSC Napoli shop and profiles: wears number 8 (web search) | The file gives his former Manchester United number |
| mq-enzo-fernandez | shortName | Enzo Fernández | Fernández | Design | FS27 contract: a short name has at most 12 characters | "Enzo Fernández" has 14; without the correction the player cannot be imported |

## Findings that are NOT changed (documented, nothing is invented)

- **Luka Modrić's Target OVR (81) is 6 below the calculated overall (87)**: outside the tolerance of 5, so the importer warns (`TargetOvrDiffers`). The tolerance and the research (attributes, roles, target) are not touched; the game uses the calculated 87.
- **Nicolò Barella has Stamina 95**, the only 95 of the base, kept as researched.
- **Source inconsistencies reported by the reader** (the cards are used): the database table writes the club without "FC"/"SSC" for Fernández, Rice, Mac Allister and McTominay. A test pins exactly these four.
- **Verified and unchanged:** Fabián Ruiz (PSG, 8), Pedri (Barcelona, 8), Enzo Fernández (Chelsea, 8), Rice (Arsenal, 41), Mac Allister (Liverpool, 10), Tonali (Newcastle, 8), Barella (Inter, 23), Mainoo (Manchester United, 37), dates of birth.
- **Secondary zones** are the one the source states (Attack for Fabián, Pedri, Enzo, Modrić, Barella, McTominay; Defense for Rice, Mac Allister, Tonali, Mainoo); emergency cover (AM, LW, CB, RM) is authoring information only. McTominay's first role is Finisher (91), as researched.
- **Signature actions** with no football behaviour yet are authoring tags; the tag "Late box arrival" maps to the existing late-box-arrival behaviour (five players), nothing else is forced onto a behaviour.
