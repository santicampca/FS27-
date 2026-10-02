# FS27 DEFENDERS — CORRECTIONS V1

Every change made to `FS27_DEFENDERS_DATABASE_V1.md` before it is imported. The source file is never edited: these rows are applied by the importer
(`AuthoringCorrections`) and each one only applies if the record still holds the "before" value. Classes: **Verified** (an outside source contradicts the
file; the source is named), **Inference** (concluded from evidence inside the file and from the game's own role ratings), **Design** (an FS27 contract
decision). Nothing here changes a Target OVR, and no attribute was changed.

| Player | Field | Before | After | Class | Source | Reason |
|---|---|---|---|---|---|---|
| cb-kalidou-koulibaly | heightCm | 196 | 186 | Verified | Wikipedia lists 1.86 m (seen in a web-search result). The Saudi Pro League site lists 1.95 m, so outside sources disagree; 1.86 is used because it is the widely published figure and fits the file's weight and build | The file says 1.96 m, taller than Van Dijk (1.95); the outside sources and the rest of the file (89 kg, "Robust / Tank") fit 1.86 m |
| cb-kalidou-koulibaly | shirtNumber | 26 | 3 | Verified | Search result for his profile: plays for Al-Hilal wearing number 3 | The file gives the number he wore at his earlier club |
| cb-marquinhos | secondaryZone | Midfield / Wing | Midfield | Design | FS27 contract: a player has ONE secondary zone | His first listed secondary position is CDM (Midfield); the RB (Wing) extension stays in the authoring record as real positions |
| cb-jonathan-tah | role.Guardian | 84 | 78 | Inference | File text + official role ratings (Anchor 80, Guardian 78) | Guardian is the agility/speed role (Agility 64, Speed 72) and the file's own weakness is "agilidad y giro lentos"; Anchor (Defense, Strength, Passing, Control) fits him better |
| cb-jonathan-tah | role.Anchor | 78 | 84 | Inference | File text + official role ratings | Same reason: Anchor and Guardian swap places (Wall 90 stays primary) |
| cb-matthijs-de-ligt | role.Guardian | 80 | 76 | Inference | File text + official role ratings (Anchor 80, Guardian 78) | The file's weakness is "agilidad y velocidad de giro limitadas" (Agility 68): Guardian cannot rank above Anchor |
| cb-matthijs-de-ligt | role.Anchor | 76 | 80 | Inference | File text + official role ratings | Swap with Guardian |
| cb-kalidou-koulibaly | role.Guardian | 82 | 78 | Inference | File text + official role ratings (Anchor 81, Guardian 78) | Weakness "velocidad punta reducida... agilidad limitada en giros cortos" (Speed 74, Agility 67): Guardian below Anchor |
| cb-kalidou-koulibaly | role.Anchor | 78 | 82 | Inference | File text + official role ratings | Swap with Guardian |
| cb-marc-pubill | role.Guardian | 84 | 82 | Inference | File text + official role ratings (Wall 80, Anchor 79, Guardian 78) | His strengths are reach, aerial play and strength (Strength 85, 86 kg) and the weakness is agility (72): Wall is the primary role, not Guardian |
| cb-marc-pubill | role.Wall | 82 | 84 | Inference | File text + official role ratings | Swap with Guardian |
| cb-ruben-dias | role.Guardian | 86 | 82 | Inference | File text + official role ratings (Wall 84, Guardian 81) | Strength 89 and a "velocidad punta justa" weakness (Speed 75, Agility 72): Wall ranks above Guardian (Anchor 91 stays primary) |
| cb-ruben-dias | role.Wall | 82 | 86 | Inference | File text + official role ratings | Swap with Guardian |
| cb-pau-cubarsi | role.Guardian | 92 | 85 | Inference | File text + official role ratings (Anchor 84, Guardian 82) | His strengths are passing, vision and technique (Passing 92, Technique 90) with no pace claim (Speed 76): that is an Anchor, not a Guardian |
| cb-pau-cubarsi | role.Anchor | 85 | 92 | Inference | File text + official role ratings | Swap with Guardian |
