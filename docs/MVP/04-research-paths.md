# 04 — Research Paths

> Maps of how the 509 projects connect: the keystone spine, the weave between themes, full maps of the first three eras, keystone-critical maps for every later era, the method ladders across eras, and example strategic paths.
> Every diagram here is **generated from the catalog tables** by the validator and then pasted in. When the catalog changes, regenerate them; see [06-next-steps.md](06-next-steps.md) §4.

**Legend:** amber = Craft · green = Kin · blue = Reach · dark hexagon = Keystone. Solid arrow = required; dotted arrow = one-of alternative. **◂** = also requires a project from an earlier era (not drawn).

---

## 1. The keystone spine (the shape of a whole match)

```mermaid
flowchart LR
    classDef key fill:#3a3a3a,stroke:#000,color:#fff
    classDef craft fill:#f6d7a7,stroke:#b07a2a,color:#3a2a10
    classDef kin fill:#cfe8c6,stroke:#4f8a3c,color:#1d3317
    classDef reach fill:#c9dcf2,stroke:#3d6fa8,color:#13263d
    S((Start: band of 12)) --> K1
    K1{{KEY-1 Symbolic Mind}}:::key --> K2{{KEY-2 Sedentism}}:::key --> K3{{KEY-3 Age of Metal}}:::key --> K4{{KEY-4 Iron and Law}}:::key --> K5{{KEY-5 Feudal Order}}:::key
    K5 --> K6{{KEY-6 Printing Revolution}}:::key --> K7{{KEY-7 Industrial Revolution}}:::key --> K8{{KEY-8 Electrification}}:::key --> K9{{KEY-9 Digital Revolution}}:::key --> K10{{KEY-10 Planetary Network APEX}}:::key
    a1[Hafting]:::craft & a2[Fire Making]:::craft & a3[Kin Bonds]:::kin & a4[Spoken Language]:::reach --> K1
    b1[Pit House]:::craft & b2[Wild Cereal Harvest]:::kin & b3[Storage Pits]:::kin & b4[Seasonal Round]:::reach --> K2
    c1[Copper Smelting]:::craft & c2[Chiefdom]:::kin & c3[Exchange Networks]:::reach & c4[Clay Tokens]:::reach --> K3
    d1[Bloomery Iron]:::craft & d2[Code of Law]:::kin & d3[Writing]:::reach & d4[Market Towns]:::reach --> K4
    e1[Heavy Plough]:::kin & e2[Feudal Contract]:::kin & e3[Organized Faith]:::kin & e4[Castle Building]:::reach --> K5
    f1[Universities]:::kin & f2[Movable Type]:::reach & f3[Double-Entry Bookkeeping]:::reach & f4[Magnetic Compass]:::reach --> K6
    g1[Steam Engine]:::craft & g2[Factory System]:::craft & g3[Joint-Stock Company]:::reach & g4[Scientific Method]:::reach --> K7
    h1[Electric Generator]:::craft & h2[Germ Theory]:::kin & h3[Telegraph]:::reach & h4[Railways]:::reach --> K8
    i1[Electronic Computer]:::craft & i2[Mass Education]:::kin & i3[Transistor]:::reach & i4[Global Trade Order]:::reach --> K9
    j1[Sustainable Grid]:::craft & j2[Internet]:::reach & j3[Satellite Constellation]:::reach & j4[Global Governance Forum]:::reach --> K10
```

## 2. Critical paths and breadth

"Closure" is every project that *must* be completed to unlock the keystone (choosing the cheaper side of each one-of). "In-era" is the part of that closure inside the current era. **Breadth N** is the number of current-era projects the keystone demands, so **N − in-era** is the number of free choices each era guarantees.

| Keystone | Transition | Closure (all eras) | In-era critical | Breadth N | Free picks |
|----------|------------|-------------------:|----------------:|----------:|-----------:|
| KEY-1 Symbolic Mind | E0 → E1 | 11 | 11 | 13 | 2 |
| KEY-2 Sedentism | E1 → E2 | 28 | 11 | 14 | 3 |
| KEY-3 Age of Metal | E2 → E3 | 70 | 17 | 20 | 3 |
| KEY-4 Iron & Law | E3 → E4 | 108 | 18 | 21 | 3 |
| KEY-5 Feudal Order | E4 → E5 | 153 | 17 | 21 | 4 |
| KEY-6 Printing Revolution | E5 → E6 | 174 | 12 | 16 | 4 |
| KEY-7 Industrial Revolution | E6 → E7 | 210 | 10 | 14 | 4 |
| KEY-8 Electrification | E7 → E8 | 227 | 10 | 14 | 4 |
| KEY-9 Digital Revolution | E8 → E9 | 278 | 14 | 18 | 4 |
| KEY-10 Planetary Network | E9 → Apex | 338 | 13 | 17 | 4 |

**Balance pass ([07](07-balance-findings.md) F9):** KEY-4's breadth N drops from 22 to 21, so E3 completions land inside the 22–25 band in [02 §8.3](02-research-structure.md#83-cost-formula). The balance model's catalog parser finds shorter paths than this table from KEY-4 on: KEY-4's in-era path is 16 rather than 18 (5 free picks rather than 3), and every closure from KEY-4 to KEY-10 comes out 3–4 smaller (e.g. 104 rather than 108). The offset probably comes from a different pick on one any-of. The catalog validator (WS-M) should settle the tie-break rule; until then this table keeps the hand-derived paths.

Of the 509 projects, **338** are on the Apex closure; the other **171 are entirely optional**. They are pure strategy and geography: wonders, warfare beyond the basics, luxury goods, most of the medicine and culture lines.

## 3. The weave: how the themes feed each other

Prerequisite edges by theme (row = provider → column = consumer):

| from ↓ / to → | Craft | Kin | Reach |
|---|---:|---:|---:|
| **Craft** | 274 | 52 | 66 |
| **Kin** | 26 | 206 | 41 |
| **Reach** | 38 | 55 | 238 |

About **28%** of all prerequisite links cross themes. **Craft is the biggest exporter** (tools and materials enable everything), **Reach is the biggest importer of Craft**, and Kin and Reach feed each other almost symmetrically (society needs communication; claims need cohesion).

### 3.1 Lineage bridges
Which lineages each lineage *draws from* (its inputs) and *feeds* (its outputs), counting cross-lineage prerequisite links:

| Lineage | Theme | Draws from | Feeds |
|---------|-------|------------|-------|
| STN | Craft | FIB, HNT, FOR, MEC, BEL, MIN, MET, SOC, FIR | FIR×3, WOD×3, HNT×3, FIB×2, BLD×2, MIN×2, FOR×2, AGR×2, ART×2, MET, CHM, TRN, DEF |
| FIR | Craft | STN×3, WOD×2, CHM×2, FOR, CER, FIB, MIN, TRN, SOC, NRG, INQ | CER×4, CHM×4, MET×3, FOD×3, STN, FIB, MIN, AGR, COM |
| WOD | Craft | STN×3, MET×3, NRG×3, ART, FOR, MEC, FOD, NUM, CHM, INQ | SEA×4, HNT×3, FIR×2, BLD×2, MEC×2, FOR×2, DEF×2, WAR |
| FIB | Craft | CHM×3, MEC×3, STN×2, HUS×2, TRD×2, FOR, FIR, AGR, SOC, NRG | MEC×3, BLD×2, HNT×2, TRN×2, SEA×2, COM×2, STN, FIR, NRG, CHM, FOR, HUS, MED |
| CER | Craft | FIR×4, MEC×2, CHM×2, INQ×2, ART, FOD, TRD, NRG, COM | INQ×3, MET×2, MEC×2, FIR, BLD, CHM, HUS, FOD, MED, ART, COM, NUM |
| BLD | Craft | WOD×2, FIB×2, STN×2, NUM×2, CHM×2, MET×2, NRG×2, SOC, BEL, CER, TRD, INQ | MED×2, TRN×2, SEA×2, NRG, CHM, FOD, SOC, BEL, ART, TER, DEF |
| MIN | Craft | CHM×3, STN×2, NRG×2, INQ×2, MET, SOC, FIR, FOD, MEC, NUM, WAY | NRG×3, CHM×2, STN, FIR, FOD, DEF, INQ |
| MET | Craft | INQ×5, FIR×3, NRG×3, CER×2, TRD×2, STN, ART, CHM | WAR×4, WOD×3, NRG×3, TRN×3, BLD×2, STN, MIN, MEC, FOR, AGR, FOD, MED, SEA, TRD, COM, DEF |
| MEC | Craft | NUM×4, FIB×3, WOD×2, NRG×2, CER×2, SOC, MET, INQ | NRG×4, FIB×3, CER×2, AGR×2, WAY×2, TRN×2, COM×2, DEF×2, WAR×2, STN, WOD, MIN, MED, SOC, ART, LRN, TRD, NUM, INQ |
| NRG | Craft | MEC×4, INQ×4, MIN×3, MET×3, HUS×2, FIB, BLD | TRN×4, WOD×3, MET×3, AGR×3, BLD×2, MIN×2, MEC×2, COM×2, FIR, FIB, CER, CHM, FOD, ART, SEA, WAR, INQ |
| CHM | Craft | FIR×4, INQ×4, MIN×2, STN, FIB, FOD, CER, BLD, NRG, AGR | FIB×3, MIN×3, INQ×3, FIR×2, CER×2, BLD×2, WOD, MET, AGR, MED, ART, WAR |
| FOR | Kin | WOD×2, STN×2, INQ×2, FIB, WAY, AGR, MET, COM, SEA | AGR×3, STN, FIR, WOD, FIB, FOD, MED, WAY, INQ |
| HNT | Kin | WOD×3, STN×3, SEA×3, FIB×2, INQ×2, COM, GOV | WAR×2, STN, HUS, INQ |
| AGR | Kin | FOR×3, NRG×3, STN×2, HUS×2, MEC×2, FIR, NUM, MET, TER, CHM, WAY, INQ | HUS×3, FIB, CHM, FOR, FOD, SOC |
| HUS | Kin | AGR×3, HNT, WAY, CER, FIB, TER, MED, FOD, INQ | TRN×3, FIB×2, NRG×2, AGR×2, TRD, WAR |
| FOD | Kin | FIR×3, FOR, CER, AGR, BLD, MIN, GOV, SEA, MET, NRG, TRN, TRD | WOD, CER, MIN, CHM, HUS, SOC, GOV |
| MED | Kin | COM×2, SOC×2, BLD×2, LRN×2, FOR, FIB, MET, BEL, GOV, CER, CHM, INQ, MEC | HUS, BEL |
| SOC | Kin | GOV×3, TRD×3, COM×2, BEL, FOD, BLD, AGR, MEC | GOV×4, BEL×3, MED×2, ART×2, LRN×2, TRD×2, TER×2, DIP×2, STN, FIR, FIB, BLD, MIN, MEC, WAR |
| GOV | Kin | SOC×4, COM×4, LRN×3, FOD, NUM, WAR, BEL, TRD | DIP×4, SOC×3, TER×3, LRN×2, WAR×2, HNT, FOD, MED, BEL, TRN, TRD, NUM |
| BEL | Kin | SOC×3, ART×2, COM×2, MED, BLD, GOV, TRD, LRN, TRN, INQ, DIP | LRN×2, STN, BLD, MED, SOC, GOV, ART, NUM, TER, DIP, INQ |
| ART | Kin | COM×4, SOC×2, STN×2, CHM, WAY, BLD, BEL, TRD, CER, NRG, MEC | COM×3, BEL×2, WOD, CER, MET, LRN, WAY, TRD |
| LRN | Kin | COM×5, SOC×2, BEL×2, GOV×2, ART, INQ, MEC, NUM | GOV×3, MED×2, BEL, WAY, NUM, INQ |
| WAY | Reach | INQ×4, NUM×4, MEC×2, FOR, ART, COM, LRN, TRN | TER×3, SEA×2, MIN, FOR, AGR, HUS, ART, TRD, DEF, WAR, INQ |
| TRN | Reach | NRG×4, HUS×3, MET×3, FIB×2, MEC×2, BLD×2, STN, GOV, SEA | WAR×3, TRD×2, FIR, FOD, BEL, WAY, COM, TER |
| SEA | Reach | WOD×4, FIB×2, WAY×2, BLD×2, WAR×2, NRG, MET, TRD | HNT×3, TRD×2, TER×2, FOR, FOD, TRN, DEF |
| TRD | Reach | COM×3, SOC×2, TRN×2, SEA×2, NUM×2, ART, WAY, MET, HUS, MEC, GOV, DIP | SOC×3, FIB×2, MET×2, CER, BLD, FOD, GOV, BEL, ART, SEA, NUM |
| COM | Reach | ART×3, FIB×2, MEC×2, INQ×2, NRG×2, FIR, NUM, TRN, MET, CER | LRN×5, DIP×5, GOV×4, ART×4, TRD×3, NUM×3, MED×2, SOC×2, BEL×2, DEF×2, CER, FOR, HNT, WAY, TER, INQ |
| NUM | Reach | COM×3, INQ, CER, TRD, BEL, LRN, GOV, MEC | MEC×4, WAY×4, INQ×4, BLD×2, TRD×2, WOD, MIN, AGR, GOV, LRN, COM, DEF |
| TER | Reach | WAY×3, GOV×3, SOC×2, SEA×2, BEL, BLD, TRN, DIP, COM | AGR, HUS, DEF |
| DEF | Reach | WOD×2, COM×2, MEC×2, WAR×2, MIN, STN, TER, BLD, NUM, SEA, MET, WAY | — |
| WAR | Reach | MET×4, TRN×3, HNT×2, GOV×2, MEC×2, WOD, SOC, HUS, CHM, NRG, INQ, WAY, DIP | SEA×2, DEF×2, DIP×2, GOV |
| DIP | Reach | COM×5, GOV×4, SOC×2, WAR×2, BEL, INQ | BEL, TRD, TER, WAR |
| INQ | Reach | NUM×4, CER×3, CHM×3, HNT, COM, MIN, BEL, MEC, LRN, NRG, WAY, FOR | MET×5, NRG×4, CHM×4, WAY×4, CER×2, MIN×2, FOR×2, HNT×2, COM×2, FIR, WOD, BLD, MEC, AGR, HUS, MED, BEL, LRN, NUM, WAR, DIP |

## 4. Era maps (vertical-slice eras, every project)

### 4.1 E0 — Dawn (34 projects)
Seven starters (no prerequisites) radiate into tools, fire, food and social bonds. KEY-1 needs 4 named projects, 11 critical in total, and 13 for breadth.

```mermaid
flowchart LR
    classDef craft fill:#f6d7a7,stroke:#b07a2a,color:#3a2a10
    classDef kin fill:#cfe8c6,stroke:#4f8a3c,color:#1d3317
    classDef reach fill:#c9dcf2,stroke:#3d6fa8,color:#13263d
    classDef key fill:#3a3a3a,stroke:#000,color:#fff
    STN_01["STN-01<br/>Pebble Choppers"]:::craft
    STN_02["STN-02<br/>Hand Axes"]:::craft
    STN_03["STN-03<br/>Prepared Cores"]:::craft
    STN_04["STN-04<br/>Hafting"]:::craft
    FIR_01["FIR-01<br/>Fire Keeping"]:::craft
    FIR_02["FIR-02<br/>Fire Making"]:::craft
    FIR_03["FIR-03<br/>Hearths"]:::craft
    FIR_04["FIR-04<br/>Fire-Hardened Points"]:::craft
    WOD_01["WOD-01<br/>Digging Sticks"]:::craft
    FIB_01["FIB-01<br/>Cordage"]:::craft
    FIB_02["FIB-02<br/>Hide Scraping"]:::craft
    BLD_01["BLD-01<br/>Windbreaks and Lean-tos"]:::craft
    FOR_01["FOR-01<br/>Plant Lore"]:::kin
    FOR_02["FOR-02<br/>Roots and Tubers"]:::kin
    HNT_01["HNT-01<br/>Power Scavenging"]:::kin
    HNT_02["HNT-02<br/>Wooden Thrusting Spears"]:::kin
    HNT_03["HNT-03<br/>Stone-Tipped Spears"]:::kin
    FOD_01["FOD-01<br/>Cooking"]:::kin
    SOC_01["SOC-01<br/>Kin Bonds"]:::kin
    SOC_02["SOC-02<br/>Food Sharing"]:::kin
    BEL_01["BEL-01<br/>Mourning and Burial"]:::kin
    ART_01["ART-01<br/>Beads and Body Ornament"]:::kin
    ART_02["ART-02<br/>Storytelling"]:::kin
    LRN_01["LRN-01<br/>Imitation and Demonstration"]:::kin
    WAY_01["WAY-01<br/>Landmarks and Trails"]:::reach
    TRN_01["TRN-01<br/>Tumplines and Carry Frames"]:::reach
    SEA_01["SEA-01<br/>Floats and Rafts"]:::reach
    TRD_01["TRD-01<br/>Gift Exchange"]:::reach
    COM_01["COM-01<br/>Calls and Gestures"]:::reach
    COM_02["COM-02<br/>Spoken Language"]:::reach
    TER_01["TER-01<br/>Home Range"]:::reach
    DEF_01["DEF-01<br/>Thorn Barriers"]:::reach
    WAR_01["WAR-01<br/>Clubs and Thrown Stones"]:::reach
    DIP_01["DIP-01<br/>Stranger Greetings"]:::reach
    KEY_1{{"KEY-1<br/>Symbolic Mind"}}:::key
    STN_01 --> STN_02
    STN_02 --> STN_03
    STN_03 --> STN_04
    FIB_01 --> STN_04
    FIR_01 --> FIR_02
    STN_02 --> FIR_02
    FIR_02 --> FIR_03
    FIR_01 --> FIR_04
    WOD_01 --> FIR_04
    STN_01 --> WOD_01
    FOR_01 --> FIB_01
    STN_02 --> FIB_02
    WOD_01 --> BLD_01
    FOR_01 --> FOR_02
    WOD_01 --> FOR_02
    HNT_01 --> HNT_02
    WOD_01 --> HNT_02
    HNT_02 --> HNT_03
    STN_04 --> HNT_03
    FIR_01 --> FOD_01
    SOC_01 --> SOC_02
    SOC_01 --> BEL_01
    SOC_01 --> ART_01
    ART_01 --> ART_02
    COM_02 --> ART_02
    SOC_01 --> LRN_01
    FIB_01 --> TRN_01
    WOD_01 --> SEA_01
    SOC_02 --> TRD_01
    COM_01 --> COM_02
    WAY_01 --> TER_01
    WOD_01 --> DEF_01
    WOD_01 --> WAR_01
    COM_01 --> DIP_01
    COM_02 --> KEY_1
    FIR_02 --> KEY_1
    STN_04 --> KEY_1
    SOC_01 --> KEY_1
```

### 4.2 E1 — Hunters (59 projects)
The Upper Paleolithic explosion: projectile weapons, needles and tailored clothing, art, shamans, dogs, fishing, storage. Kin dominates this era (25 of 59).

```mermaid
flowchart LR
    classDef craft fill:#f6d7a7,stroke:#b07a2a,color:#3a2a10
    classDef kin fill:#cfe8c6,stroke:#4f8a3c,color:#1d3317
    classDef reach fill:#c9dcf2,stroke:#3d6fa8,color:#13263d
    classDef key fill:#3a3a3a,stroke:#000,color:#fff
    STN_05["STN-05<br/>Blade Industry ◂"]:::craft
    STN_06["STN-06<br/>Microliths"]:::craft
    STN_07["STN-07<br/>Bone and Antler Tools ◂"]:::craft
    FIR_05["FIR-05<br/>Landscape Burning ◂"]:::craft
    FIR_06["FIR-06<br/>Heat-Treating Stone ◂"]:::craft
    WOD_02["WOD-02<br/>Woodcarving ◂"]:::craft
    FIB_03["FIB-03<br/>Tanning and Leather ◂"]:::craft
    FIB_04["FIB-04<br/>Tailored Clothing"]:::craft
    FIB_05["FIB-05<br/>Basketry and Netting ◂"]:::craft
    CER_01["CER-01<br/>Fired Clay Figurines ◂"]:::craft
    CER_02["CER-02<br/>Pottery ◂"]:::craft
    BLD_02["BLD-02<br/>Hide Tents ◂"]:::craft
    BLD_03["BLD-03<br/>Pit House"]:::craft
    MIN_01["MIN-01<br/>Flint and Ochre Seeking"]:::craft
    MEC_01["MEC-01<br/>Lever and Wedge"]:::craft
    CHM_01["CHM-01<br/>Ochre Pigments ◂"]:::craft
    CHM_02["CHM-02<br/>Birch-Tar Adhesive ◂"]:::craft
    FOR_03["FOR-03<br/>Nut and Seed Gathering ◂"]:::kin
    FOR_04["FOR-04<br/>Wild Cereal Harvest"]:::kin
    FOR_05["FOR-05<br/>Seed Grinding"]:::kin
    FOR_06["FOR-06<br/>Tending Wild Stands"]:::kin
    HNT_04["HNT-04<br/>Cooperative Drives ◂"]:::kin
    HNT_05["HNT-05<br/>Spear-Thrower"]:::kin
    HNT_06["HNT-06<br/>Bow and Arrow ◂"]:::kin
    HNT_07["HNT-07<br/>Harpoons and Fish Spears ◂"]:::kin
    HNT_08["HNT-08<br/>Hooks, Lines and Nets"]:::kin
    HUS_01["HUS-01<br/>Wolf Companions"]:::kin
    HUS_02["HUS-02<br/>Herd Following"]:::kin
    FOD_02["FOD-02<br/>Smoking and Drying ◂"]:::kin
    FOD_03["FOD-03<br/>Storage Pits"]:::kin
    FOD_04["FOD-04<br/>Boiling and Stews ◂"]:::kin
    MED_01["MED-01<br/>Herbal Remedies ◂"]:::kin
    MED_02["MED-02<br/>Bone Setting and Wound Care ◂"]:::kin
    SOC_03["SOC-03<br/>Band Alliances ◂"]:::kin
    SOC_04["SOC-04<br/>Clans and Totems"]:::kin
    GOV_01["GOV-01<br/>Elders' Council ◂"]:::kin
    BEL_02["BEL-02<br/>Animism and Totems ◂"]:::kin
    BEL_03["BEL-03<br/>Shamanism"]:::kin
    ART_03["ART-03<br/>Cave Painting ◂"]:::kin
    ART_04["ART-04<br/>Music and Dance ◂"]:::kin
    ART_05["ART-05<br/>Songlines ◂"]:::kin
    LRN_02["LRN-02<br/>Oral Tradition ◂"]:::kin
    WAY_02["WAY-02<br/>Seasonal Round ◂"]:::reach
    WAY_03["WAY-03<br/>Star Paths"]:::reach
    TRN_02["TRN-02<br/>Travois and Sledges ◂"]:::reach
    TRN_03["TRN-03<br/>Snowshoes and Skis"]:::reach
    SEA_02["SEA-02<br/>Bark and Hide Canoes ◂"]:::reach
    TRD_02["TRD-02<br/>Barter ◂"]:::reach
    COM_03["COM-03<br/>Symbols and Marks ◂"]:::reach
    NUM_01["NUM-01<br/>Tally Marks ◂"]:::reach
    NUM_02["NUM-02<br/>Lunar Calendar"]:::reach
    TER_02["TER-02<br/>Camp Circuits ◂"]:::reach
    TER_03["TER-03<br/>Territorial Markers"]:::reach
    DEF_02["DEF-02<br/>Lookouts ◂"]:::reach
    WAR_02["WAR-02<br/>Raiding ◂"]:::reach
    WAR_03["WAR-03<br/>Archery Warfare"]:::reach
    DIP_02["DIP-02<br/>Hospitality Customs ◂"]:::reach
    INQ_01["INQ-01<br/>Tracking and Animal Lore ◂"]:::reach
    INQ_02["INQ-02<br/>Sky Watching ◂"]:::reach
    KEY_2{{"KEY-2<br/>Sedentism"}}:::key
    STN_05 --> STN_06
    STN_05 --> STN_07
    STN_05 --> FIR_06
    STN_05 --> WOD_02
    FIB_03 --> FIB_04
    STN_07 --> FIB_04
    CER_01 --> CER_02
    BLD_02 --> BLD_03
    WOD_02 --> BLD_03
    STN_05 --> MIN_01
    WOD_02 --> MEC_01
    FIB_05 --> FOR_03
    FOR_03 --> FOR_04
    STN_06 --> FOR_04
    FOR_04 --> FOR_05
    STN_05 --> FOR_05
    FOR_04 --> FOR_06
    WAY_02 --> FOR_06
    HNT_04 --> HNT_05
    WOD_02 --> HNT_05
    HNT_05 --> HNT_06
    STN_06 --> HNT_06
    STN_07 --> HNT_07
    HNT_07 --> HNT_08
    FIB_05 --> HNT_08
    HNT_04 --> HUS_01
    HUS_01 --> HUS_02
    WAY_02 --> HUS_02
    FOD_02 --> FOD_03
    FOR_03 --> FOD_03
    CER_02 --> FOD_04
    MED_01 --> MED_02
    SOC_03 --> SOC_04
    BEL_02 --> SOC_04
    BEL_02 --> BEL_03
    MED_01 --> BEL_03
    CHM_01 --> ART_03
    STN_07 --> ART_04
    WAY_02 --> ART_05
    WAY_02 --> WAY_03
    INQ_02 --> WAY_03
    HUS_01 --> TRN_02
    TRN_02 --> TRN_03
    FIB_03 --> TRN_03
    FIB_03 --> SEA_02
    NUM_01 --> NUM_02
    INQ_02 --> NUM_02
    WAY_02 --> TER_02
    TER_02 --> TER_03
    BEL_02 --> TER_03
    HNT_04 --> WAR_02
    WAR_02 --> WAR_03
    HNT_06 --> WAR_03
    SOC_03 --> DIP_02
    FOR_04 --> KEY_2
    FOD_03 --> KEY_2
    BLD_03 --> KEY_2
    WAY_02 --> KEY_2
```

### 4.3 E2 — Roots (52 projects)
The Neolithic: cultivation, herding, villages, pottery storage, polished stone, the first metal, chiefdoms and exchange networks.

```mermaid
flowchart LR
    classDef craft fill:#f6d7a7,stroke:#b07a2a,color:#3a2a10
    classDef kin fill:#cfe8c6,stroke:#4f8a3c,color:#1d3317
    classDef reach fill:#c9dcf2,stroke:#3d6fa8,color:#13263d
    classDef key fill:#3a3a3a,stroke:#000,color:#fff
    STN_08["STN-08<br/>Ground and Polished Stone ◂"]:::craft
    STN_09["STN-09<br/>Querns and Grindstones ◂"]:::craft
    STN_10["STN-10<br/>Megalith Raising"]:::craft
    FIR_07["FIR-07<br/>Kilns ◂"]:::craft
    FIR_08["FIR-08<br/>Charcoal Burning"]:::craft
    WOD_03["WOD-03<br/>Adzes and Gouges ◂"]:::craft
    WOD_04["WOD-04<br/>Coppicing ◂"]:::craft
    FIB_06["FIB-06<br/>Spinning ◂"]:::craft
    FIB_07["FIB-07<br/>Loom Weaving"]:::craft
    CER_03["CER-03<br/>Storage Jars ◂"]:::craft
    BLD_04["BLD-04<br/>Wattle and Daub ◂"]:::craft
    BLD_05["BLD-05<br/>Mudbrick"]:::craft
    BLD_06["BLD-06<br/>Longhouses"]:::craft
    MIN_02["MIN-02<br/>Flint Mines ◂"]:::craft
    MIN_03["MIN-03<br/>Quarrying"]:::craft
    MET_01["MET-01<br/>Native Copper"]:::craft
    MET_02["MET-02<br/>Copper Smelting"]:::craft
    MEC_02["MEC-02<br/>Rollers and Sledges ◂"]:::craft
    NRG_01["NRG-01<br/>Draft Animals"]:::craft
    CHM_03["CHM-03<br/>Lime Burning"]:::craft
    FOR_07["FOR-07<br/>Orchards"]:::kin
    HNT_09["HNT-09<br/>Weirs and Fish Traps ◂"]:::kin
    AGR_01["AGR-01<br/>Cultivation ◂"]:::kin
    AGR_02["AGR-02<br/>Domesticated Staples"]:::kin
    AGR_03["AGR-03<br/>Legume Pairing"]:::kin
    AGR_04["AGR-04<br/>Slash-and-Burn ◂"]:::kin
    HUS_03["HUS-03<br/>Goats and Sheep ◂"]:::kin
    HUS_04["HUS-04<br/>Cattle and Pigs"]:::kin
    FOD_05["FOD-05<br/>Fermentation ◂"]:::kin
    FOD_06["FOD-06<br/>Granaries ◂"]:::kin
    MED_03["MED-03<br/>Midwifery ◂"]:::kin
    SOC_05["SOC-05<br/>Feasting ◂"]:::kin
    SOC_06["SOC-06<br/>Village Life"]:::kin
    SOC_07["SOC-07<br/>Chiefdom"]:::kin
    GOV_02["GOV-02<br/>Chieftainship ◂"]:::kin
    GOV_03["GOV-03<br/>Customary Law ◂"]:::kin
    BEL_04["BEL-04<br/>Ancestor Veneration ◂"]:::kin
    BEL_05["BEL-05<br/>Sacred Places ◂"]:::kin
    LRN_03["LRN-03<br/>Apprenticeship ◂"]:::kin
    WAY_04["WAY-04<br/>Sketch Maps ◂"]:::reach
    TRN_04["TRN-04<br/>Pack Animals ◂"]:::reach
    SEA_03["SEA-03<br/>Dugout Canoes ◂"]:::reach
    SEA_04["SEA-04<br/>Outriggers ◂"]:::reach
    TRD_03["TRD-03<br/>Exchange Networks ◂"]:::reach
    COM_04["COM-04<br/>Signal Fires and Drums ◂"]:::reach
    NUM_03["NUM-03<br/>Clay Tokens ◂"]:::reach
    TER_04["TER-04<br/>Permanent Villages ◂"]:::reach
    TER_05["TER-05<br/>Daughter Villages"]:::reach
    DEF_03["DEF-03<br/>Palisades and Ditches ◂"]:::reach
    WAR_04["WAR-04<br/>Warrior Bands ◂"]:::reach
    DIP_03["DIP-03<br/>Marriage Alliances ◂"]:::reach
    INQ_03["INQ-03<br/>Stone and Ore Lore ◂"]:::reach
    KEY_3{{"KEY-3<br/>Age of Metal"}}:::key
    STN_08 --> STN_09
    STN_08 --> STN_10
    MEC_02 --> STN_10
    BEL_05 --> STN_10
    FIR_07 --> FIR_08
    WOD_03 --> FIR_08
    STN_08 --> WOD_03
    WOD_03 --> WOD_04
    AGR_02 -.-> FIB_06
    HUS_03 -.-> FIB_06
    FIB_06 --> FIB_07
    BLD_04 --> BLD_05
    BLD_04 --> BLD_06
    SOC_05 --> BLD_06
    STN_08 --> MIN_02
    MIN_02 --> MIN_03
    STN_08 --> MET_01
    MET_01 --> MET_02
    FIR_07 --> MET_02
    INQ_03 --> MET_02
    HUS_04 --> NRG_01
    FIR_07 --> CHM_03
    MIN_03 --> CHM_03
    AGR_01 --> FOR_07
    WOD_03 --> FOR_07
    WOD_03 --> HNT_09
    AGR_01 --> AGR_02
    AGR_02 --> AGR_03
    AGR_01 --> AGR_04
    STN_08 --> AGR_04
    HUS_03 --> HUS_04
    AGR_01 --> HUS_04
    AGR_02 --> FOD_05
    BLD_05 --> FOD_06
    FOD_05 --> SOC_05
    SOC_05 --> SOC_06
    BLD_06 --> SOC_06
    SOC_06 --> SOC_07
    GOV_02 --> SOC_07
    SOC_05 --> GOV_02
    GOV_02 --> GOV_03
    BEL_04 --> BEL_05
    SOC_05 --> LRN_03
    HUS_03 --> TRN_04
    WOD_03 --> SEA_03
    SEA_03 --> SEA_04
    WAY_04 --> TRD_03
    BLD_04 --> TER_04
    TER_04 --> TER_05
    SOC_06 --> TER_05
    WOD_03 --> DEF_03
    SOC_05 --> WAR_04
    MET_02 --> KEY_3
    SOC_07 --> KEY_3
    TRD_03 --> KEY_3
    NUM_03 --> KEY_3
```

## 5. Keystone-critical maps for later eras
Only the in-era projects on each keystone's critical path are drawn. Everything else in that era is a free pick.

### 5.1 E3 Bronze → KEY-4 Iron & Law
```mermaid
flowchart LR
    classDef craft fill:#f6d7a7,stroke:#b07a2a,color:#3a2a10
    classDef kin fill:#cfe8c6,stroke:#4f8a3c,color:#1d3317
    classDef reach fill:#c9dcf2,stroke:#3d6fa8,color:#13263d
    classDef key fill:#3a3a3a,stroke:#000,color:#fff
    FIR_09["FIR-09<br/>Bellows and Tuyères ◂"]:::craft
    WOD_05["WOD-05<br/>Joinery ◂"]:::craft
    MET_03["MET-03<br/>Casting and Moulds ◂"]:::craft
    MET_04["MET-04<br/>Arsenical Bronze"]:::craft
    MET_08["MET-08<br/>Bloomery Iron"]:::craft
    MEC_03["MEC-03<br/>The Wheel ◂"]:::craft
    AGR_05["AGR-05<br/>Ard Plough ◂"]:::kin
    AGR_06["AGR-06<br/>Irrigation Canals"]:::kin
    SOC_08["SOC-08<br/>Craft Specialization ◂"]:::kin
    SOC_09["SOC-09<br/>Urbanism"]:::kin
    GOV_04["GOV-04<br/>Tribute and Redistribution ◂"]:::kin
    GOV_05["GOV-05<br/>Kingship and Palace"]:::kin
    GOV_06["GOV-06<br/>Code of Law"]:::kin
    TRN_05["TRN-05<br/>Wheeled Carts ◂"]:::reach
    TRD_04["TRD-04<br/>Marketplaces ◂"]:::reach
    TRD_05["TRD-05<br/>Market Towns"]:::reach
    COM_05["COM-05<br/>Writing ◂"]:::reach
    NUM_04["NUM-04<br/>Standard Weights and Measures ◂"]:::reach
    KEY_4{{"KEY-4<br/>Iron and Law"}}:::key
    MET_03 --> WOD_05
    MET_03 --> MET_04
    FIR_09 --> MET_08
    MET_04 -.-> MET_08
    WOD_05 --> MEC_03
    AGR_05 --> AGR_06
    NUM_04 --> AGR_06
    TRD_04 --> SOC_08
    SOC_08 --> SOC_09
    AGR_06 --> SOC_09
    GOV_04 --> GOV_05
    SOC_09 --> GOV_05
    GOV_05 --> GOV_06
    COM_05 --> GOV_06
    MEC_03 --> TRN_05
    TRD_04 --> TRD_05
    TRN_05 --> TRD_05
    TRD_04 --> NUM_04
    MET_08 --> KEY_4
    COM_05 --> KEY_4
    GOV_06 --> KEY_4
    TRD_05 --> KEY_4
```

### 5.2 E4 Iron → KEY-5 Feudal Order
```mermaid
flowchart LR
    classDef craft fill:#f6d7a7,stroke:#b07a2a,color:#3a2a10
    classDef kin fill:#cfe8c6,stroke:#4f8a3c,color:#1d3317
    classDef reach fill:#c9dcf2,stroke:#3d6fa8,color:#13263d
    classDef key fill:#3a3a3a,stroke:#000,color:#fff
    CER_08["CER-08<br/>Fired Brick and Tile ◂"]:::craft
    BLD_09["BLD-09<br/>True Arch and Vault ◂"]:::craft
    MET_09["MET-09<br/>Iron Tools ◂"]:::craft
    MEC_04["MEC-04<br/>Pulley and Windlass ◂"]:::craft
    AGR_09["AGR-09<br/>Iron Sickle and Scythe ◂"]:::kin
    AGR_10["AGR-10<br/>Heavy Plough ◂"]:::kin
    HUS_09["HUS-09<br/>Camels and Caravan Animals ◂"]:::kin
    GOV_07["GOV-07<br/>Bureaucracy and Census ◂"]:::kin
    GOV_09["GOV-09<br/>Feudal Contract"]:::kin
    BEL_08["BEL-08<br/>Philosophy and Ethics ◂"]:::kin
    BEL_09["BEL-09<br/>Organized Faith"]:::kin
    TRD_06["TRD-06<br/>Coinage ◂"]:::reach
    TRD_07["TRD-07<br/>Long-Distance Trade Routes"]:::reach
    COM_07["COM-07<br/>Alphabet ◂"]:::reach
    DEF_06["DEF-06<br/>Towers and Gatehouses ◂"]:::reach
    DEF_08["DEF-08<br/>Castle Building"]:::reach
    WAR_07["WAR-07<br/>Professional Armies ◂"]:::reach
    KEY_5{{"KEY-5<br/>Feudal Order"}}:::key
    CER_08 --> BLD_09
    MET_09 --> AGR_09
    AGR_09 --> AGR_10
    GOV_07 --> GOV_09
    WAR_07 --> GOV_09
    COM_07 --> BEL_08
    BEL_08 --> BEL_09
    TRD_07 --> BEL_09
    TRD_06 --> TRD_07
    HUS_09 -.-> TRD_07
    MEC_04 --> DEF_06
    DEF_06 --> DEF_08
    BLD_09 --> DEF_08
    GOV_07 --> WAR_07
    AGR_10 --> KEY_5
    GOV_09 --> KEY_5
    BEL_09 --> KEY_5
    DEF_08 --> KEY_5
```

### 5.3 E5 Faith & Fief → KEY-6 Printing Revolution
```mermaid
flowchart LR
    classDef craft fill:#f6d7a7,stroke:#b07a2a,color:#3a2a10
    classDef kin fill:#cfe8c6,stroke:#4f8a3c,color:#1d3317
    classDef reach fill:#c9dcf2,stroke:#3d6fa8,color:#13263d
    classDef key fill:#3a3a3a,stroke:#000,color:#fff
    GOV_10["GOV-10<br/>Canon and Common Law ◂"]:::kin
    BEL_10["BEL-10<br/>Monasticism ◂"]:::kin
    LRN_07["LRN-07<br/>Monastery and Cathedral Schools ◂"]:::kin
    LRN_08["LRN-08<br/>Universities"]:::kin
    WAY_08["WAY-08<br/>Magnetic Compass ◂"]:::reach
    TRD_08["TRD-08<br/>Banking ◂"]:::reach
    TRD_09["TRD-09<br/>Double-Entry Bookkeeping"]:::reach
    COM_09["COM-09<br/>Paper ◂"]:::reach
    COM_10["COM-10<br/>Movable Type ◂"]:::reach
    NUM_08["NUM-08<br/>Zero and Positional Numerals ◂"]:::reach
    NUM_09["NUM-09<br/>Algebra"]:::reach
    INQ_06["INQ-06<br/>Optics and Magnetism ◂"]:::reach
    KEY_6{{"KEY-6<br/>Printing Revolution"}}:::key
    BEL_10 --> LRN_07
    LRN_07 --> LRN_08
    GOV_10 --> LRN_08
    INQ_06 --> WAY_08
    NUM_08 --> TRD_08
    TRD_08 --> TRD_09
    NUM_09 --> TRD_09
    COM_09 --> COM_10
    NUM_08 --> NUM_09
    COM_10 --> KEY_6
    TRD_09 --> KEY_6
    WAY_08 --> KEY_6
    LRN_08 --> KEY_6
```

### 5.4 E6 Discovery → KEY-7 Industrial Revolution
```mermaid
flowchart LR
    classDef craft fill:#f6d7a7,stroke:#b07a2a,color:#3a2a10
    classDef kin fill:#cfe8c6,stroke:#4f8a3c,color:#1d3317
    classDef reach fill:#c9dcf2,stroke:#3d6fa8,color:#13263d
    classDef key fill:#3a3a3a,stroke:#000,color:#fff
    FIB_12["FIB-12<br/>Flying Shuttle and Spinning Jenny ◂"]:::craft
    MIN_09["MIN-09<br/>Coal Mining ◂"]:::craft
    MEC_09["MEC-09<br/>Precision Instruments ◂"]:::craft
    MEC_10["MEC-10<br/>Factory System"]:::craft
    NRG_06["NRG-06<br/>Steam Engine ◂"]:::craft
    SOC_12["SOC-12<br/>Middle Class ◂"]:::kin
    SEA_12["SEA-12<br/>Caravels and Ocean Voyages ◂"]:::reach
    TRD_10["TRD-10<br/>Joint-Stock Company ◂"]:::reach
    INQ_07["INQ-07<br/>Telescope and Heliocentrism ◂"]:::reach
    INQ_08["INQ-08<br/>Scientific Method ◂"]:::reach
    KEY_7{{"KEY-7<br/>Industrial Revolution"}}:::key
    MEC_09 --> FIB_12
    TRD_10 --> FIB_12
    MEC_09 --> MEC_10
    FIB_12 --> MEC_10
    SOC_12 --> MEC_10
    MIN_09 --> NRG_06
    SEA_12 --> TRD_10
    INQ_07 --> INQ_08
    NRG_06 --> KEY_7
    MEC_10 --> KEY_7
    TRD_10 --> KEY_7
    INQ_08 --> KEY_7
```

### 5.5 E7 Industry → KEY-8 Electrification
```mermaid
flowchart LR
    classDef craft fill:#f6d7a7,stroke:#b07a2a,color:#3a2a10
    classDef kin fill:#cfe8c6,stroke:#4f8a3c,color:#1d3317
    classDef reach fill:#c9dcf2,stroke:#3d6fa8,color:#13263d
    classDef key fill:#3a3a3a,stroke:#000,color:#fff
    CER_12["CER-12<br/>Optical Glass and Microscopy ◂"]:::craft
    MET_14["MET-14<br/>Puddling and Rolling Mills ◂"]:::craft
    NRG_07["NRG-07<br/>Watt Engine ◂"]:::craft
    NRG_08["NRG-08<br/>Electric Generator"]:::craft
    MED_10["MED-10<br/>Inoculation and Vaccination ◂"]:::kin
    MED_11["MED-11<br/>Germ Theory"]:::kin
    TRN_12["TRN-12<br/>Railways ◂"]:::reach
    COM_12["COM-12<br/>Telegraph ◂"]:::reach
    INQ_10["INQ-10<br/>Modern Chemistry ◂"]:::reach
    INQ_11["INQ-11<br/>Electromagnetism"]:::reach
    KEY_8{{"KEY-8<br/>Electrification"}}:::key
    NRG_07 --> MET_14
    NRG_07 --> NRG_08
    INQ_11 --> NRG_08
    MED_10 --> MED_11
    CER_12 --> MED_11
    NRG_07 --> TRN_12
    MET_14 --> TRN_12
    INQ_11 --> COM_12
    INQ_10 --> INQ_11
    NRG_08 --> KEY_8
    COM_12 --> KEY_8
    TRN_12 --> KEY_8
    MED_11 --> KEY_8
```

### 5.6 E8 Modern → KEY-9 Digital Revolution
```mermaid
flowchart LR
    classDef craft fill:#f6d7a7,stroke:#b07a2a,color:#3a2a10
    classDef kin fill:#cfe8c6,stroke:#4f8a3c,color:#1d3317
    classDef reach fill:#c9dcf2,stroke:#3d6fa8,color:#13263d
    classDef key fill:#3a3a3a,stroke:#000,color:#fff
    CER_13["CER-13<br/>Electrical Insulators ◂"]:::craft
    CER_14["CER-14<br/>Silicon Crystal Growing"]:::craft
    MEC_12["MEC-12<br/>Assembly Line ◂"]:::craft
    MEC_13["MEC-13<br/>Electronic Computer"]:::craft
    NRG_09["NRG-09<br/>Electric Grid ◂"]:::craft
    LRN_11["LRN-11<br/>Mass Education ◂"]:::kin
    TRD_13["TRD-13<br/>Central Banking ◂"]:::reach
    TRD_14["TRD-14<br/>Global Trade Order"]:::reach
    COM_13["COM-13<br/>Telephone ◂"]:::reach
    COM_14["COM-14<br/>Radio"]:::reach
    NUM_13["NUM-13<br/>Boolean Logic and Computation ◂"]:::reach
    DIP_12["DIP-12<br/>International Organizations ◂"]:::reach
    INQ_15["INQ-15<br/>Relativity and Quantum Physics ◂"]:::reach
    INQ_18["INQ-18<br/>Transistor"]:::reach
    KEY_9{{"KEY-9<br/>Digital Revolution"}}:::key
    NRG_09 --> CER_13
    CER_13 --> CER_14
    INQ_15 --> CER_14
    NRG_09 --> MEC_12
    MEC_12 --> MEC_13
    NUM_13 --> MEC_13
    COM_14 --> LRN_11
    TRD_13 --> TRD_14
    DIP_12 --> TRD_14
    NRG_09 --> COM_13
    COM_13 --> COM_14
    INQ_15 --> COM_14
    COM_14 --> DIP_12
    INQ_15 --> INQ_18
    CER_14 --> INQ_18
    INQ_18 --> KEY_9
    MEC_13 --> KEY_9
    LRN_11 --> KEY_9
    TRD_14 --> KEY_9
```

### 5.7 E9 Information → KEY-10 Planetary Network (Apex)
```mermaid
flowchart LR
    classDef craft fill:#f6d7a7,stroke:#b07a2a,color:#3a2a10
    classDef kin fill:#cfe8c6,stroke:#4f8a3c,color:#1d3317
    classDef reach fill:#c9dcf2,stroke:#3d6fa8,color:#13263d
    classDef key fill:#3a3a3a,stroke:#000,color:#fff
    CER_15["CER-15<br/>Fiber Optics ◂"]:::craft
    MIN_15["MIN-15<br/>Rare Earths and Lithium ◂"]:::craft
    MEC_14["MEC-14<br/>Integrated Circuits ◂"]:::craft
    MEC_15["MEC-15<br/>Personal Computing"]:::craft
    NRG_13["NRG-13<br/>Solar Photovoltaics ◂"]:::craft
    NRG_14["NRG-14<br/>Wind Turbines ◂"]:::craft
    NRG_15["NRG-15<br/>Battery Storage"]:::craft
    NRG_16["NRG-16<br/>Sustainable Grid"]:::craft
    WAY_13["WAY-13<br/>Satellite Constellation ◂"]:::reach
    COM_16["COM-16<br/>Internet ◂"]:::reach
    NUM_14["NUM-14<br/>Data Science ◂"]:::reach
    DIP_14["DIP-14<br/>Global Governance Forum ◂"]:::reach
    INQ_19["INQ-19<br/>Ecology and Climate Science ◂"]:::reach
    KEY_10{{"KEY-10<br/>Planetary Network"}}:::key
    MEC_14 --> MEC_15
    MEC_14 --> NRG_13
    NRG_13 --> NRG_15
    MIN_15 --> NRG_15
    NRG_13 --> NRG_16
    NRG_14 --> NRG_16
    NRG_15 --> NRG_16
    INQ_19 --> NRG_16
    MEC_15 --> COM_16
    CER_15 --> COM_16
    MEC_15 --> NUM_14
    COM_16 --> DIP_14
    NUM_14 --> INQ_19
    COM_16 --> KEY_10
    WAY_13 --> KEY_10
    NRG_16 --> KEY_10
    DIP_14 --> KEY_10
```

(KEY-3's critical path is fully visible in the E2 map in §4.3.)

## 6. Method ladders across the eras: "greater and greater efficiency"

Each row is a core action family. Each cell is the best method a civilization *could* have in that era if it invested in the lineage. This table is the efficiency fantasy in one view.

| Action | E0 Dawn | E1 Hunters | E2 Roots | E3 Bronze | E4 Iron | E5 Faith & Fief | E6 Discovery | E7 Industry | E8 Modern | E9 Information |
|---|---|---|---|---|---|---|---|---|---|---|
| **Chop wood** | Chopper → hand axe → hafted axe | Blade tools | Polished axe | Copper / bronze axe | Iron axe, crosscut saw | Water sawmill | (coal replaces wood fuel) | Steam sawmill | Chainsaw, plywood | Harvester, sustainable yield |
| **Gather / farm** | Plant lore, tubers | Wild-cereal sickles, stand tending | Cultivation, staples, legumes | Ard, irrigation, manuring | Iron scythe, heavy plough, terraces | Three-field rotation | Four-course rotation, Columbian Exchange | Seed drill, reaper | Tractors, fertilizer, Green Revolution | Precision farming, GM crops |
| **Hunt / fish** | Scavenging, spears | Drives, atlatl, bow, nets | Weirs | Seal & whale hunting | — | Game reserves | Deep-sea fleets | Steam trawlers | Aquaculture | Sustainable fisheries |
| **Herd** | — | Dogs, herd following | Goats, sheep, cattle, pigs | Dairy, wool, horses, nomadism | Camels | Selective breeding | Fodder crops | Veterinary science | Feedlots, cold chain | Animal genetics |
| **Store / preserve** | Cooking | Smoking, pits, pots | Fermentation, granaries | Salting, bakeries | Public grain supply | — | Sugar | Canning | Refrigeration, processing | Cold-chain logistics |
| **Build** | Lean-to | Tent, pit house | Wattle, mudbrick, longhouse | Post-and-lintel, masonry | Arch, aqueduct, concrete | Gothic, master masons | Renaissance planning | Reinforced concrete | Steel-frame skyscrapers | Green & smart buildings |
| **Mine / smelt** | — | Flint & ochre seeking | Flint mines, quarry, copper | Shaft mining, bronze, bloomery iron | Iron tools, steel, drainage | Crucible steel, blast furnace | Coal, coke, blasting | Puddling, Bessemer, dynamite | Aluminium, oil, open pit, arc furnace | Rare earths, recycling, remote sensing |
| **Power** | Muscle | Muscle (+ dog travois) | Draft oxen | Yoke | Water wheel | Windmill, horse collar | Steam engine | Watt engine, generator | Grid, combustion engine, hydro, fission | Solar, wind, batteries, sustainable grid |
| **Haul / travel** | Tumpline, rafts | Travois, skis, canoes | Pack animals, dugouts | Carts, riding, sail | Roads, bridges, harbors | Stirrup, keels, lateen | Turnpikes, canals, caravels | Railways, steamships | Trucks, highways, aviation, steel hulls | Containers, high-speed rail, EVs |
| **Make (craft)** | Knapping | Blades, carving, sewing | Polished stone, spinning, weaving | Wheel, potter's wheel, lathe | Gears, screws, crane, glassblowing | Crank & cam, clocks, treadle loom | Precision instruments, factory system | Machine tools, power loom | Assembly line, computers | ICs, robotics |
| **Generate Insight** | Imitation | Oral tradition, shamans | Apprenticeship | Scribes, writing | Academies, Great Library | Universities, monasteries | Printing, learned societies, scientific method | Public schooling | Mass education, R&D labs | Internet, open knowledge, AI-assisted research |
| **Claim land** | Home range | Camp circuits, markers | Villages, daughter villages | Boundary stones, city-states | Provinces, frontier colonies, castles | Manors | Overseas colonies, star forts | Cadastre | Suburbs, broadcast soft power | Megacities, global pop culture |
| **Communicate** | Calls, language | Symbols | Signal fires | Writing, papyrus | Alphabet, courier relays | Paper, movable type | Newspapers | Telegraph | Telephone, radio, TV | Internet, mobile, social media |

## 7. Example strategic paths (openings)

Each opening below is **validated**: every project appears after its prerequisites, eras are respected, and keystone breadth is met. Slot notation: the first three projects are the opening Primary / Secondary / Tertiary.

### 7.1 The Grower (Kin-leaning)
*Goal:* population and fission. Bands split early and spread across the land. Sedentism comes early, fed by cereals and storage.
- **Opening slots:** P = FOR-01 Plant Lore, S = SOC-01 Kin Bonds, T = FIR-01 Fire Keeping
- **Up to KEY-1 (19 projects):** FOR-01 Plant Lore → SOC-01 Kin Bonds → FIR-01 Fire Keeping → FOD-01 Cooking → SOC-02 Food Sharing → STN-01 Pebble Choppers → WOD-01 Digging Sticks → FOR-02 Roots & Tubers → FIB-01 Cordage → COM-01 Calls & Gestures → COM-02 Spoken Language → ART-01 Beads & Body Ornament → ART-02 Storytelling → STN-02 Hand Axes → FIR-02 Fire Making → FIR-03 Hearths → STN-03 Prepared Cores → STN-04 Hafting → WAY-01 Landmarks & Trails → **KEY-1**
- **KEY-1 → KEY-2 (19 projects, including cheap E0 backfills):** SOC-03 Band Alliances → WAY-02 Seasonal Round → FIB-05 Basketry & Netting → FOR-03 Nut & Seed Gathering → STN-05 Blade Industry → FOD-02 Smoking & Drying → STN-06 Microliths → FOR-04 Wild Cereal Harvest → FOD-03 Storage Pits → MED-01 Herbal Remedies → FOR-05 Seed Grinding → FOR-06 Tending Wild Stands → FIB-02 Hide Scraping → BLD-01 Windbreaks & Lean-tos → WOD-02 Woodcarving → BLD-02 Hide Tents → BLD-03 Pit House → LRN-01 Imitation & Demonstration → LRN-02 Oral Tradition → **KEY-2**
- *Result:* Sedentism at 39 completions, with medicine, oral tradition and alliances already in place. Weak tools and no defenses: vulnerable to a Warlord neighbor.

### 7.2 The Builder (Craft-leaning)
*Goal:* tool tiers and fire mastery first. A small, highly efficient band whose every action runs on the best method.
- **Opening slots:** P = STN-01 Pebble Choppers, S = FIR-01 Fire Keeping, T = FOR-01 Plant Lore
- **Up to KEY-1 (16 projects):** STN-01 Pebble Choppers → FIR-01 Fire Keeping → FOR-01 Plant Lore → STN-02 Hand Axes → WOD-01 Digging Sticks → FIB-01 Cordage → FIR-02 Fire Making → STN-03 Prepared Cores → FIR-03 Hearths → FIR-04 Fire-Hardened Points → BLD-01 Windbreaks & Lean-tos → STN-04 Hafting → FIB-02 Hide Scraping → COM-01 Calls & Gestures → COM-02 Spoken Language → SOC-01 Kin Bonds → **KEY-1**
- **KEY-1 → KEY-2 (21 projects, including cheap E0 backfills):** STN-05 Blade Industry → FIR-06 Heat-Treating Stone → STN-06 Microliths → CHM-01 Ochre Pigments → CHM-02 Birch-Tar Adhesive → ART-01 Beads & Body Ornament → WOD-02 Woodcarving → MEC-01 Lever & Wedge → BLD-02 Hide Tents → BLD-03 Pit House → FIB-03 Tanning & Leather → FIB-05 Basketry & Netting → MIN-01 Flint & Ochre Seeking → FOR-02 Roots & Tubers → WAY-01 Landmarks & Trails → WAY-02 Seasonal Round → FOR-03 Nut & Seed Gathering → FOR-04 Wild Cereal Harvest → FOD-01 Cooking → FOD-02 Smoking & Drying → FOD-03 Storage Pits → **KEY-2**
- *Result:* Sedentism at 38 completions, with heat-treated blades, adhesives, levers, leather and mining reveals. Fastest route toward metal (Copper Smelting then needs Kilns and Ore Lore). Small population.

### 7.3 The Wanderer (Reach-leaning)
*Goal:* see, claim and meet first. Scouts, trails, territory and peaceful contact, with sedentism delayed in favor of a wide seasonal round.
- **Opening slots:** P = WAY-01 Landmarks & Trails, S = COM-01 Calls & Gestures, T = HNT-01 Power Scavenging
- **Up to KEY-1 (15 projects):** WAY-01 Landmarks & Trails → COM-01 Calls & Gestures → HNT-01 Power Scavenging → TER-01 Home Range → DIP-01 Stranger Greetings → COM-02 Spoken Language → STN-01 Pebble Choppers → FIR-01 Fire Keeping → FOR-01 Plant Lore → SOC-01 Kin Bonds → STN-02 Hand Axes → FIB-01 Cordage → FIR-02 Fire Making → STN-03 Prepared Cores → STN-04 Hafting → **KEY-1**
- **KEY-1 → KEY-2 (32 projects, including cheap E0 backfills):** WOD-01 Digging Sticks → FOR-02 Roots & Tubers → WAY-02 Seasonal Round → TER-02 Camp Circuits → INQ-02 Sky Watching → WAY-03 Star Paths → SEA-01 Floats & Rafts → HNT-02 Wooden Thrusting Spears → HNT-03 Stone-Tipped Spears → INQ-01 Tracking & Animal Lore → HNT-04 Cooperative Drives → HUS-01 Wolf Companions → TRN-01 Tumplines & Carry Frames → TRN-02 Travois & Sledges → SOC-02 Food Sharing → SOC-03 Band Alliances → DIP-02 Hospitality Customs → FIB-05 Basketry & Netting → FOR-03 Nut & Seed Gathering → STN-05 Blade Industry → STN-06 Microliths → FOR-04 Wild Cereal Harvest → FIR-03 Hearths → FOD-01 Cooking → FOD-02 Smoking & Drying → FOD-03 Storage Pits → FIB-02 Hide Scraping → BLD-01 Windbreaks & Lean-tos → BLD-02 Hide Tents → ART-01 Beads & Body Ornament → WOD-02 Woodcarving → BLD-03 Pit House → **KEY-2**
- *Result:* Sedentism arrives late (48 completions), but by then the Wanderer holds the widest claims, has dogs and travois, has met its neighbors peacefully, and has triggered exposure gates (herds, clay, copper) across a large area. That makes its Roots era unusually cheap and broad.

### 7.4 The Warlord (Reach/WAR-leaning, sketch)
Rush **WAR-01 → WAR-02 Raiding → WAR-03 Archery Warfare** in E1 (requires the hunting chain HNT-02…06), then **WAR-04 Warrior Bands** and **GOV-02 Chieftainship** (unlocks the *Raid* posture) in E2. Raid a Grower neighbor's granaries during their first winter of sedentism. Risky: Cohesion costs, and Hospitality-minded neighbors ally against you (DIP-03).

### 7.5 The Sage (LRN/INQ-leaning, sketch)
Prioritize **LRN-01 → LRN-02 Oral Tradition**, **BEL-03 Shamanism**, **INQ-02 Sky Watching → NUM-01/NUM-02** in E1. Keep elders alive (MED-01, FOD-04) for their double Insight. The Sage falls behind on land early but compounds Insight, then surges through E3–E6 via Scribes, Academies, the Great Library and Universities.

## 8. Forks, alternatives and geography-driven branches

| Fork | Options | What decides it |
|------|---------|-----------------|
| **Bronze** | MET-04 Arsenical **or** MET-05 Tin (one-of for Bloomery Iron, Lost-Wax, Bronze Arms) | Which ore you `[Has]` in territory or by trade. Tin bronze is better; tin is rare, so it makes a strategic trade good. |
| **Yarn** | FIB-06 needs HUS-03 Goats & Sheep **or** AGR-02 Staples (flax) | Herders spin wool; farmers spin flax |
| **Long-distance trade** | TRD-07 via HUS-09 Camels (caravan) **or** SEA-07 Harbors (maritime) | Desert and steppe civilizations go overland; coastal ones go by sea |
| **River vs. rain** | AGR-06 Irrigation and NRG-03 Water Wheel are `[Near: River]` | River civilizations get early intensive farming and mill power; others lean on Windmill (NRG-05) and rotation |
| **Cold vs. warm** | FIB-04 Tailored Clothing unlocks Cold bands; TRN-03 Skis is discounted there | Cold starts must sew early or stay boxed in |
| **Steppe** | HUS-07 Horse Taming (`[Seen: Wild horses]`) → riding, chariots, nomadism, cavalry, horse collar | Only if horses roam nearby; huge mobility and claim radius in grassland |
| **Coast** | HNT-07/08/10 fishing and whaling, SEA lineage | Coastal starts get protein and an early path to the sea |
| **Optional luxuries** | Silk (FIB-09), Salt (MIN-06), Sugar (FOD-10), Porcelain (CER-09), Gold (MET-07) | Gate-dependent luxuries drive trade routes and wars |
| **Late-game posture** | DEF-14 Deterrence vs. DIP-13 Arms Control vs. WAR-17 Precision Warfare / WAR-18 Peacekeeping | Whether the endgame race for the Apex is peaceful or contested |
