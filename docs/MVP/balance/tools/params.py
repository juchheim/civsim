"""Parameters for the WS-A balance model.

Two scenarios share one structure:
  "v01"   -- the numbers as written in docs 01/02/05 (v0.1), plus modeling
             assumptions where the docs are silent (marked ASSUMED).
  "tuned" -- the recommended constants from docs/MVP/07-balance-findings.md.

The spreadsheet generator (build_xlsx.py) writes every value below into the
Params sheet, and the Sim sheets reference those cells, so the .xlsx stays live.
"""
import copy

ERAS = ["E0 Dawn", "E1 Hunters", "E2 Roots", "E3 Bronze", "E4 Iron", "E5 Faith & Fief",
        "E6 Discovery", "E7 Industry", "E8 Modern", "E9 Information"]

# ---- scalars: name -> (v01, tuned, unit, note) -------------------------------------
SCALARS = {
    "SeasonSeconds":   (15, 15, "s", "05 §3.1: 150 ticks at 10 Hz"),
    "StartChildren":   (2, 2, "people", "05 §20"),
    "StartAdults":     (8, 8, "people", "05 §20 (4 men, 4 women)"),
    "StartElders":     (2, 2, "people", "05 §20"),
    "WomenShare":      (0.5, 0.5, "share", "share of adults who are fertile women"),
    "ChildYears":      (14, 14, "years", "05 §5.3: adult at 14"),
    "AdultYears":      (31, 31, "years", "05 §5.3: elder at 45"),
    "BaseFertility":   (0.06, 0.13, "births/woman/season", "05 §9.2 (tuned: WS-H rec.)"),
    "CrowdingExp":     (6, 6, "exp", "09 §3.2: births × (1 − (Pop/Cap)^k)"),
    "GroupInCrowding": (1, 0, "flag", "09 §3.2: 1 = crowding counts the group cap; tuned: food and housing only"),
    "OvercapLoss":     (0.10, 0.10, "share/season", "ASSUMED: people above cap who die or leave"),
    "CohesionFactor":  (1.0, 1.0, "×", "05 §10.1 (0.6–1.2); 1.0 = average"),
    "FissionTrigger":  (0.85, 0.85, "× settlement cap", "05 §9.4: over cap OR food/housing pressure high"),
    "FissionRate":     (0.34, 0.34, "share of settlements/season", "ASSUMED: over-cap settlements that split per season"),
    "DaughterShare":   (0.40, 0.40, "share", "05 §9.4: 30–45% of pop"),
    "MinDaughter":     (6, 6, "people", "ASSUMED: smallest viable daughter band"),
    "SettlementPacking": (0.6, 0.6, "× claim area", "ASSUMED: tiles per settlement = hex area of claim radius × packing"),
    "LowTiesShare":    (0.5, 0.5, "×", "05 §9.4: daughter bands share Insight at 50% before SOC-03"),
    "CommonsCoef":     (0.6, 4.8, "Insight/yr", "02 §8.1 Commons coefficient"),
    "CommonsExp":      (0.85, 0.85, "exp", "02 §8.1"),
    "ElderCommons":    (2.0, 2.0, "×", "02 §8.1: elders count double"),
    "RoleScale":       (1.0, 8.0, "×", "multiplies every role and structure output in 02 §8.1"),
    "OutElder":        (1.0, 1.0, "Insight/yr", "GOV-01 (×RoleScale)"),
    "OutShaman":       (1.5, 1.5, "Insight/yr", "BEL-03"),
    "OutPriest":       (2.0, 2.0, "Insight/yr", "BEL-06"),
    "OutScribe":       (3.0, 3.0, "Insight/yr", "LRN-04 / COM-05"),
    "OutScholar":      (6.0, 6.0, "Insight/yr", "LRN-05"),
    "OutResearcher":   (15.0, 15.0, "Insight/yr", "LRN-12"),
    "OutMonastery":    (2.0, 2.0, "Insight/yr", "BEL-10 (per monastery)"),
    "EraBoonPct":      (0.05, 0.05, "share", "02 §13: +5% Insight Era Boon"),
    "EraBoonTake":     (0.5, 0.5, "prob", "ASSUMED: Leaning picks Insight boon half the time"),
    "KHPerAdult":      (0.05, 0.0, "KH/adult/season", "02 §9: 0.05 per figure-equivalent per season (linear)"),
    "KHOfCommons":     (0.0, 0.35, "× Commons", "tuned: Know-how = share of Commons Insight (after learning mults)"),
    "KHUsable":        (0.7, 0.7, "share", "ASSUMED: practice in lineages that get slotted"),
    "KHMaxRatio":      (0.75, 0.75, "× Insight", "02 §9 drain ≤ slot rate and ≤ 50% of cost; ~6/7 of slots practised"),
    "SlotPrimary":     (4, 4, "parts", "02 §8.2"),
    "SlotSecondary":   (2, 2, "parts", "02 §8.2"),
    "SlotTertiary":    (1, 1, "parts", "02 §8.2"),
    "KeystoneMult":    (2.5, 2.5, "×", "02 §4 type K"),
    "EraLag":          (0.7, 0.7, "× per era", "02 §8.3 EraLagMult"),
    "BackfillOverlap": (0.3, 0.3, "share", "ASSUMED: forced backfills already taken as earlier free picks"),
    "DiffusionMult":   (1.0, 1.0, "×", "02 §8.3; 1.0 = the leader, no contacted civ knows it"),
    # population caps and the winter trigger (09-population-and-health.md)
    "CapsByProject":   (0, 1, "flag", "09 §5: caps rise project by project (Caps sheet); 0 = per-era tables only"),
    "WinterTrigger":   (0, 1, "flag", "09 §7.2: late-winter food-pressure fission trigger (0 = off)"),
    "LeanBase":        (1.0, 0.77, "× FoodCap", "09 §6.2: late-winter food a camp can count on, before any storage project"),
}

# Pace: (research-cost multiplier, seconds per season) per scenario.
# v0.1 (01 §6) scales costs only; tuned scales the calendar instead (see 07 §F7).
PACES = {"Standard": {"v01": (1.0, 15), "tuned": (1.0, 15)},
         "Quick":    {"v01": (0.5, 15), "tuned": (1.0, 7.5)},
         "Epic":     {"v01": (1.6, 15), "tuned": (1.0, 26)}}

# ---- per-era tables: name -> {scenario: [E0..E9]} -----------------------------------
def same(v):
    return {"v01": list(v), "tuned": list(v)}

ERA = {
    # research
    "EraBase": {"v01":   [20, 50, 125, 300, 750, 1900, 4700, 12000, 30000, 75000],
                "tuned": [22, 50, 100, 160, 660, 3750, 19500, 175000, 1050000, 5500000]},  # progressive cadence; E3 stays at the Bronze live fit (07c §10)
    "BreadthN": {"v01":  [13, 14, 20, 22, 21, 16, 14, 14, 18, 17],
                 "tuned": [13, 14, 20, 21, 21, 16, 14, 14, 18, 17]},
    "CostMod": same([1, 1, 1, 1, 1, 1, 1, 0.9, 0.9, 0.9]),   # INQ-08 Scientific Method −10% from E7
    "TargetMinLo": same([7, 9, 11, 12, 14, 16, 18, 22, 26, 28]),
    "TargetMinHi": same([9, 11, 13, 14, 16, 18, 20, 24, 28, 32]),
    "TargetDoneLo": same([13, 14, 20, 22, 21, 16, 14, 14, 18, 17]),
    "TargetDoneHi": same([16, 17, 23, 25, 24, 19, 17, 17, 21, 20]),
    "RepScale": {"v01":   [1, 1, 2, 5, 10, 25, 50, 100, 250, 500],
                 "tuned": [1, 1, 1, 1, 2, 5, 25, 100, 250, 500]},
    # population. v0.1 keeps the WS-A placeholders; tuned = 09-population-and-health.md.
    # Caps: v0.1 holds one value per era; tuned holds the start value in every row and the
    # Caps table (CAPS below) raises it project by project (CapsByProject = 1).
    "GroupCap": {"v01":   [35, 100, 500, 5000, 10000, 10000, 1e6, 1e6, 1e6, 1e6],
                 "tuned": [25] * 10},   # 05 §20 base group cap
    "FoodCap": {"v01":   [22, 30, 60, 120, 250, 500, 1000, 2500, 6000, 12000],
                "tuned": [20] * 10},    # 09 §6.1: a radius-2 home range, no techniques
    "HousingCap": {"v01":   [60, 60, 100, 200, 400, 800, 1600, 4000, 10000, 20000],
                   "tuned": [40] * 10},  # 09 §6.3: open camp, sleeping rough
    # 09 §8.2; tuned = 07b R6: the map's room (~1,600 land tiles per civ) from E5 on
    "Territory": {"v01":   [60, 200, 500, 900, 1400, 1800, 2200, 2400, 2500, 2500],
                  "tuned": [60, 200, 500, 900, 1400, 1600, 1600, 1600, 1600, 1600]},
    "ClaimRadius": same([2, 3, 3, 4, 5, 5, 5, 7, 9, 9]),
    # births and annual hazards (09 §3–4); hazards are per year outside winter, × WinterMult in winter
    "FertMult": {"v01":   [1.0, 1.25, 1.35, 1.3, 1.2, 1.1, 1.05, 1.0, 0.75, 0.55],
                 "tuned": [1.0, 1.15, 1.3, 1.25, 1.2, 1.1, 1.1, 1.0, 0.55, 0.35]},
    "MortChild": {"v01":   [0.040, 0.035, 0.035, 0.030, 0.028, 0.025, 0.022, 0.015, 0.006, 0.003],
                  "tuned": [0.036, 0.033, 0.035, 0.034, 0.030, 0.029, 0.025, 0.017, 0.0045, 0.0009]},
    "MortAdult": {"v01":   [0.015, 0.013, 0.012, 0.011, 0.010, 0.010, 0.009, 0.007, 0.004, 0.0025],
                  "tuned": [0.012, 0.011, 0.012, 0.011, 0.0105, 0.0105, 0.0095, 0.0078, 0.003, 0.0015]},
    "MortElder": {"v01":   [0.10, 0.08, 0.07, 0.065, 0.06, 0.06, 0.055, 0.05, 0.035, 0.025],
                  "tuned": [0.053, 0.053, 0.057, 0.055, 0.053, 0.051, 0.049, 0.043, 0.033, 0.027]},
    "WinterMult": {"v01":   [2.0, 1.6, 1.3, 1.15, 1.0, 1.0, 1.0, 1.0, 1.0, 1.0],
                   "tuned": [2.0, 1.7, 1.4, 1.25, 1.2, 1.15, 1.1, 1.08, 1.05, 1.05]},
    # specialists (shares of adults; per-settlement for Elder/Shaman/Monastery; 09 §9)
    "ElderRolePerSett": same([0, 0.5, 1, 1, 1, 1, 1, 1, 1, 1]),
    "ShamanPerSett": same([0, 0.5, 1, 1, 1, 1, 1, 1, 1, 1]),
    "PriestShare": same([0, 0, 0, 0.002, 0.004, 0.005, 0.005, 0.004, 0.003, 0.002]),
    "ScribeShare": same([0, 0, 0, 0.001, 0.004, 0.007, 0.009, 0.010, 0.010, 0.010]),
    "ScholarShare": same([0, 0, 0, 0, 0.0005, 0.002, 0.005, 0.009, 0.014, 0.020]),
    "ResearcherShare": same([0, 0, 0, 0, 0, 0, 0, 0, 0.001, 0.005]),
    "MonasteryPerSett": same([0, 0, 0, 0, 0, 0.15, 0.3, 0.3, 0.2, 0.1]),
}

# ---- Insight modifiers from the catalog (kind, pct as fraction, take probability) ----
# Probability 1.0 = on a keystone closure (every civ completes it). Optional ones are
# weighted by how likely an average civ takes them. Applied at half strength in the
# era they belong to (completed mid-era on average), full strength after.
LEARNING = [
    ("FIR-03", "Hearths", 0, "Commons", 0.10, 1.0),
    ("LRN-02", "Oral Tradition", 1, "Elder", 0.50, 1.0),
    ("GOV-08", "Republic & Assemblies", 4, "Commons", 0.15, 1.0),
    ("COM-07", "Alphabet", 4, "Commons", 0.05, 1.0),
    ("COM-07", "Alphabet (scribes ×2)", 4, "Scribe", 1.00, 1.0),
    ("BEL-08", "Philosophy & Ethics", 4, "All", 0.10, 1.0),
    ("INQ-04", "Natural Philosophy", 4, "Scholar", 0.20, 1.0),
    ("LRN-06", "Great Library (Wonder)", 4, "All", 0.20, 0.4),
    ("CER-10", "Lens Grinding", 5, "Elder", 0.25, 1.0),
    ("COM-10", "Movable Type", 5, "All", 0.15, 1.0),
    ("NUM-09", "Algebra", 5, "Scholar", 0.15, 1.0),
    ("LRN-08", "Universities", 5, "Scholar", 0.50, 1.0),
    ("ART-09", "Illuminated Manuscripts", 5, "Monastery", 0.10, 1.0),
    ("SOC-12", "Middle Class", 6, "Commons", 0.20, 1.0),
    ("BEL-12", "Reformation & Tolerance", 6, "All", 0.10, 1.0),
    ("ART-10", "Renaissance Patronage", 6, "All", 0.10, 1.0),
    ("INQ-08", "Scientific Method", 6, "All", 0.10, 1.0),
    ("LRN-10", "Public Schooling", 7, "Commons", 0.50, 1.0),
    ("GOV-13", "Constitutional Democracy", 7, "All", 0.10, 1.0),
    ("BEL-13", "Secular Humanism", 7, "All", 0.10, 1.0),
    ("FIR-13", "Gas Lighting (cities)", 7, "All", 0.05, 0.5),
    ("LRN-11", "Mass Education", 8, "Commons", 0.50, 1.0),
    ("NRG-09", "Electric Grid", 8, "All", 0.10, 1.0),
    ("GOV-14", "Universal Suffrage", 8, "Commons", 0.10, 0.6),
    ("MEC-13", "Electronic Computer", 8, "Researcher", 0.50, 1.0),
    ("MEC-15", "Personal Computing", 9, "Commons", 0.30, 1.0),
    ("COM-16", "Internet", 9, "All", 0.25, 1.0),
    ("COM-17", "Mobile Networks", 9, "Commons", 0.10, 0.5),
    ("LRN-14", "AI-Assisted Research", 9, "Researcher", 1.00, 0.5),
    ("MED-15", "Genomic Medicine", 9, "Elder", 0.25, 0.4),
    ("ART-15", "Interactive Media", 9, "Commons", 0.05, 0.3),
]
KINDS = ["Commons", "All", "Elder", "Scribe", "Scholar", "Researcher", "Monastery"]

# ---- population caps raised project by project (09 §5) -------------------------------
# (id, project, era, at, cap, ×, prob). `era` is the era in which the project typically
# completes, which for a forced backfill is the era whose keystone forces it (e.g. SOC-04,
# an E1 project on KEY-3's closure, completes early in E2). The cap applies once research
# reaches era + at, where `at` is the share of that era's requirement done by then
# (critical path by depth; free picks early; forced backfills at 0.1–0.2). Prob < 1 for optional projects: the cap is × mult^prob.
# Caps: Food (sustainable food per settlement), Lean (late-winter food as a share of
# Food, capped at 1), Housing (dwelling capacity), Group (social layer × CohesionFactor).
CAP_KINDS = ["Food", "Lean", "Housing", "Group"]
CAPS = [
    # E0 Dawn: forage and scavenging, one band
    ("FOR-01", "Plant Lore", 0, 0.10, "Food", 1.10, 1.0),
    ("HNT-01", "Power Scavenging", 0, 0.25, "Food", 1.12, 1.0),
    ("SOC-01", "Kin Bonds", 0, 0.35, "Group", 1.40, 1.0),            # 25 -> 35
    ("FOD-01", "Cooking", 0, 0.45, "Food", 1.10, 1.0),
    ("BLD-01", "Windbreaks & Lean-tos", 0, 0.85, "Housing", 1.25, 1.0),
    # E1 Hunters: storage and the seasonal round end the hungry winter
    ("FOR-02", "Roots & Tubers", 1, 0.40, "Lean", 1.10, 1.0),
    ("HNT-02", "Wooden Thrusting Spears", 1, 0.20, "Food", 1.04, 1.0),
    ("BLD-02", "Hide Tents", 1, 0.25, "Housing", 1.20, 1.0),
    ("FOR-03", "Nut & Seed Gathering", 1, 0.30, "Food", 1.08, 1.0),
    ("FOR-03", "Nut & Seed Gathering (stores)", 1, 0.30, "Lean", 1.25, 1.0),
    ("FOD-02", "Smoking & Drying", 1, 0.40, "Lean", 1.15, 1.0),
    ("HNT-03", "Stone-Tipped Spears", 1, 0.45, "Food", 1.04, 1.0),
    ("WAY-02", "Seasonal Round", 1, 0.50, "Food", 1.05, 1.0),
    ("HNT-07", "Harpoons & Fish Spears", 1, 0.50, "Food", 1.10, 0.6),
    ("BLD-03", "Pit House", 1, 0.55, "Housing", 1.50, 1.0),
    ("FOR-04", "Wild Cereal Harvest", 1, 0.60, "Food", 1.10, 1.0),
    ("FOD-03", "Storage Pits", 1, 0.70, "Lean", 1.30, 1.0),
    ("SOC-03", "Band Alliances", 1, 0.80, "Group", 50 / 35, 1.0),     # 35 -> 50
    ("HNT-04", "Cooperative Drives", 1, 0.85, "Food", 1.10, 0.7),
    # E2 Roots: fields, villages and the social ladder
    ("SOC-04", "Clans & Totems", 2, 0.10, "Group", 3.0, 1.0),         # 50 -> 150
    ("SOC-04", "Clans & Totems (hamlet)", 2, 0.10, "Housing", 1.30, 1.0),
    ("FOD-04", "Boiling & Stews", 2, 0.10, "Food", 1.05, 1.0),
    ("FOR-05", "Seed Grinding", 2, 0.10, "Food", 1.04, 1.0),
    ("FOR-06", "Tending Wild Stands", 2, 0.15, "Food", 1.06, 1.0),
    ("AGR-01", "Cultivation", 2, 0.30, "Food", 1.30, 1.0),
    ("BLD-04", "Wattle & Daub", 2, 0.35, "Housing", 1.50, 1.0),
    ("AGR-02", "Domesticated Staples", 2, 0.50, "Food", 1.22, 1.0),
    ("SOC-06", "Village Life", 2, 0.50, "Group", 500 / 150, 1.0),     # 150 -> 500
    ("SOC-06", "Village Life (village)", 2, 0.50, "Housing", 1.50, 1.0),
    ("BLD-06", "Longhouses", 2, 0.60, "Housing", 1.50, 1.0),
    ("FOD-05", "Fermentation", 2, 0.65, "Food", 1.05, 1.0),
    ("AGR-03", "Legume Pairing", 2, 0.70, "Food", 1.08, 0.5),
    ("HNT-09", "Weirs & Fish Traps", 2, 0.70, "Food", 1.08, 0.4),
    ("FOR-07", "Orchards", 2, 0.75, "Food", 1.08, 0.4),
    ("SOC-07", "Chiefdom", 2, 0.80, "Group", 4.0, 1.0),               # 500 -> 2,000
    # E3 Bronze: herds, plough, irrigation, cities
    ("HUS-03", "Goats & Sheep", 3, 0.15, "Food", 1.08, 1.0),
    ("HUS-04", "Cattle & Pigs", 3, 0.15, "Food", 1.05, 1.0),
    ("HUS-02", "Herd Following", 3, 0.15, "Food", 1.04, 1.0),
    ("FOD-06", "Granaries", 3, 0.15, "Food", 1.05, 1.0),
    ("BLD-05", "Mudbrick", 3, 0.15, "Housing", 1.50, 1.0),
    ("AGR-05", "Ard Plough", 3, 0.35, "Food", 1.17, 1.0),
    ("TRN-05", "Wheeled Carts", 3, 0.40, "Food", 1.06, 1.0),
    ("HUS-05", "Dairying", 3, 0.50, "Food", 1.08, 0.5),
    ("AGR-06", "Irrigation Canals", 3, 0.55, "Food", 1.17, 1.0),
    ("TRD-05", "Market Towns", 3, 0.60, "Food", 1.05, 1.0),
    ("AGR-07", "Fallow & Manuring", 3, 0.60, "Food", 1.10, 0.6),
    ("SOC-09", "Urbanism", 3, 0.60, "Group", 5.0, 1.0),               # 2,000 -> 10,000
    ("SOC-09", "Urbanism (city)", 3, 0.60, "Housing", 1.60, 1.0),
    ("GOV-04", "Tribute & Redistribution", 3, 0.70, "Food", 1.04, 1.0),
    # E4 Iron: iron tools, heavy plough, roads, aqueducts
    ("BLD-07", "Post-and-Lintel", 4, 0.15, "Housing", 1.20, 1.0),
    ("HNT-06", "Bow & Arrow", 4, 0.15, "Food", 1.03, 1.0),
    ("AGR-09", "Iron Sickle & Scythe", 4, 0.40, "Food", 1.20, 1.0),
    ("BLD-09", "True Arch & Vault", 4, 0.40, "Housing", 1.30, 1.0),
    ("SOC-10", "Citizenship", 4, 0.50, "Group", 5.0, 1.0),            # 10,000 -> 50,000
    ("TRN-07", "Paved Roads", 4, 0.50, "Food", 1.06, 0.7),
    ("AGR-08", "Terracing", 4, 0.50, "Food", 1.10, 0.5),
    ("MEC-05", "Gears & Screws", 4, 0.50, "Food", 1.04, 0.6),
    ("FOR-08", "Grafting", 4, 0.60, "Food", 1.08, 0.4),
    ("FOD-09", "Public Grain Supply", 4, 0.60, "Food", 1.15, 0.4),
    ("SEA-07", "Harbors", 4, 0.60, "Food", 1.08, 0.5),
    ("BLD-10", "Aqueducts", 4, 0.60, "Housing", 1.40, 0.6),
    ("BLD-11", "Concrete & Domes", 4, 0.60, "Housing", 1.20, 0.5),
    ("AGR-10", "Heavy Plough", 4, 0.70, "Food", 1.25, 1.0),
    # E5 Faith & Fief: rotation, breeding, mills
    ("BEL-10", "Monasticism", 5, 0.30, "Food", 1.05, 1.0),
    ("AGR-11", "Three-Field Rotation", 5, 0.50, "Food", 1.35, 0.9),
    ("NRG-05", "Windmill", 5, 0.50, "Food", 1.08, 0.6),
    ("TER-10", "Manorial Estates", 5, 0.50, "Food", 1.05, 0.6),
    ("HUS-10", "Selective Breeding", 5, 0.60, "Food", 1.15, 0.7),
    ("FIR-10", "Chimneys & Flues", 5, 0.60, "Housing", 1.15, 0.5),
    # E6 Discovery: four-course rotation, canals, the Columbian Exchange
    ("AGR-12", "Four-Course Rotation & Enclosure", 6, 0.50, "Food", 1.35, 0.9),
    ("HUS-11", "Fodder Crops & Stall-Feeding", 6, 0.50, "Food", 1.10, 0.5),
    ("FOR-10", "Columbian Exchange", 6, 0.60, "Food", 1.20, 0.6),
    ("TRN-11", "Canals & Locks", 6, 0.60, "Food", 1.10, 0.7),
    ("HNT-12", "Deep-Sea Fishing Fleets", 6, 0.60, "Food", 1.08, 0.4),
    ("BLD-13", "Renaissance Architecture", 6, 0.60, "Housing", 1.30, 0.5),
    ("GOV-12", "Nation-State", 6, 0.60, "Group", 20.0, 1.0),          # 50,000 -> unbounded
    # E7 Industry: railways, machines, sewers
    # 07b R6: with territory at the map's room, late growth is vertical. Two E7 closure
    # projects carry ×1.6 food and housing density (1.265² = 1.6) in project-sized steps:
    # factory towns on rotary steam, then rail-fed cities importing their hinterland's grain.
    ("NRG-07", "Watt Engine (factory towns, 07b R6)", 7, 0.25, "Food", 1.265, 1.0),
    ("NRG-07", "Watt Engine (factory towns, 07b R6)", 7, 0.25, "Housing", 1.265, 1.0),
    ("TRN-12", "Railways", 7, 0.40, "Food", 1.50, 1.0),
    ("TRN-12", "Railways (rail-fed cities, 07b R6)", 7, 0.40, "Food", 1.265, 1.0),
    ("TRN-12", "Railways (rail-fed cities, 07b R6)", 7, 0.40, "Housing", 1.265, 1.0),
    ("AGR-13", "Seed Drill & Mechanical Reaper", 7, 0.50, "Food", 1.40, 0.9),
    ("MED-12", "Sanitation & Sewers", 7, 0.50, "Housing", 1.50, 0.8),
    ("FOR-11", "Plant Breeding", 7, 0.60, "Food", 1.40, 0.8),
    ("BLD-14", "Reinforced Concrete", 7, 0.60, "Housing", 1.20, 0.6),
    ("FOD-11", "Canning", 7, 0.60, "Food", 1.05, 0.6),
    # E8 Modern: fertilizer, the Green Revolution, skyscrapers and suburbs
    ("TRN-13", "Automobiles & Trucks", 8, 0.15, "Housing", 1.10, 1.0),
    ("CHM-12", "Fertilizer Synthesis", 8, 0.20, "Food", 1.50, 0.9),
    ("FOD-12", "Refrigeration", 8, 0.40, "Food", 1.10, 0.8),
    ("AGR-14", "Tractors & Combines", 8, 0.50, "Food", 1.15, 0.8),
    ("BLD-15", "Steel-Frame Skyscrapers", 8, 0.50, "Housing", 2.00, 0.7),
    ("AGR-15", "Green Revolution", 8, 0.60, "Food", 2.00, 0.7),
    ("FOD-13", "Industrial Food Processing", 8, 0.70, "Food", 1.15, 0.6),
    ("TER-13", "Urban Planning & Suburbs", 8, 0.60, "Housing", 1.30, 0.6),
    # E9 Information: precision farming, megacities
    ("AGR-16", "Precision Farming", 9, 0.50, "Food", 1.20, 0.7),
    ("TRN-17", "High-Speed Rail & EVs", 9, 0.50, "Housing", 1.20, 0.5),
    ("FOD-14", "Cold Chain & Nutrition Science", 9, 0.50, "Food", 1.15, 0.6),
    ("AGR-17", "Genetically Engineered Crops", 9, 0.60, "Food", 1.15, 0.6),
    ("TER-15", "Smart Megacities", 9, 0.60, "Housing", 2.00, 0.5),
    ("BLD-16", "Green & Smart Buildings", 9, 0.60, "Housing", 1.10, 0.6),
]

# ---- archetype profile (multipliers and threshold adjustments) -----------------------
# These drive the single-civilization arc model. The live sim re-fits Insight/Roles/KH for the competitive Legacy race
# and drops the Warlord's AdultMort 1.3 (war deaths are simulated since M5); it has no Territory or Diffusion
# multiplier. See docs/MVP/14 §17 and CivSim.Sim.Economy.ArchetypeResearch.
ARCH_FIELDS = ["Fertility", "FoodCap", "Territory", "Insight", "Roles", "KH", "AdultMort", "Diffusion", "LeanShareAdjustment"]
ARCHETYPES = {
    "Balanced": [1.00, 1.00, 1.00, 1.00, 1.00, 1.00, 1.00, 1.00, 0.00],
    "Grower":   [1.15, 1.20, 1.10, 1.00, 0.90, 0.95, 1.00, 1.00, 0.00],
    "Builder":  [0.95, 1.05, 0.90, 1.00, 1.00, 1.30, 1.00, 1.00, -0.02],
    "Wanderer": [1.00, 0.95, 1.40, 0.95, 0.95, 1.10, 1.05, 0.90, 0.00],
    "Warlord":  [1.00, 1.00, 1.30, 0.90, 0.90, 1.00, 1.30, 1.00, 0.00],
    "Sage":     [0.95, 0.95, 0.90, 1.10, 1.40, 1.00, 0.95, 1.00, 0.00],
}


def scenario(name, pace="Standard", archetype="Balanced", overrides=None):
    s = {k: (v[0] if name == "v01" else v[1]) for k, v in SCALARS.items()}
    s["era"] = {k: list(v[name]) for k, v in ERA.items()}
    s["pace"], s["SeasonSeconds"] = PACES[pace][name]
    s["arch"] = dict(zip(ARCH_FIELDS, ARCHETYPES[archetype]))
    s["name"] = name
    if overrides:
        for k, v in overrides.items():
            if k in s["era"]:
                s["era"][k] = list(v)
            else:
                s[k] = v
    return copy.deepcopy(s)
