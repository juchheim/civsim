namespace CivSim.Sim.Core;

/// <summary>
/// The player and AI actions that reach the simulation. Presentation code builds <see cref="Command"/>s and
/// never writes simulation state (docs/MVP/15 §5.1, rule D7). Numeric values are part of the replay
/// format: never renumber, only append.
/// </summary>
public enum CommandKind : ushort
{
    None = 0,
    /// <summary>A = slot (0 primary, 1 secondary, 2 tertiary), B = project id.</summary>
    SetSlot = 1,
    /// <summary>A = project id appended to the civilization's queue.</summary>
    QueueProject = 2,
    /// <summary>A = queue index to remove.</summary>
    DequeueProject = 3,
    /// <summary>A = theme (0 craft, 1 kin, 2 reach), B = weight in percent.</summary>
    SetLeaning = 4,
    /// <summary>A = tile x, B = tile y, C = 1 to replace the nearest Beacon when at capacity.</summary>
    PlaceBeacon = 5,
    /// <summary>A = beacon index.</summary>
    RemoveBeacon = 6,
    /// <summary>A = posture (see <see cref="Posture"/>).</summary>
    SetPosture = 7,
    /// <summary>A = dense project index from the loaded content pack.</summary>
    CompleteProject = 8,
    /// <summary>A = 1 to let the Council of Elders fill empty focus slots, 0 to leave them empty.</summary>
    SetCouncil = 9,

    // Politics orders for a human player (docs/MVP/18 §7): the same rules as the AI's orders (PoliticsSystem.Order*),
    // applied in PoliticsSystem.Step. An order the rules refuse does nothing.
    /// <summary>A = recipient civilization: send a gift party (TRD-01, contact, food to spare).</summary>
    SendGift = 10,
    /// <summary>A = partner civilization: send a barter trip (TRD-02 on both sides, Relations above zero).</summary>
    SendBarter = 11,
    /// <summary>A = target settlement index, B = party size (at least 4): a raid (WAR-02, contact, no alliance or truce).</summary>
    LaunchRaid = 12,
    /// <summary>A = target settlement index: the field army assaults it (at war with its owner).</summary>
    LaunchAssault = 13,
    /// <summary>A = civilization (contacted, not allied).</summary>
    DeclareWar = 14,
    /// <summary>A = civilization at war with us (no occupation between the two pending).</summary>
    MakePeace = 15,
    /// <summary>A = civilization (DIP-03 on both sides, Relations above 40).</summary>
    FormAlliance = 16,
    /// <summary>A = expedition index: recall the selected party to its origin settlement.</summary>
    RecallParty = 17,

    // Bronze levers (docs/MVP/22 §4.5): applied in PoliticsSystem.Step, resolved at the end of the season.
    /// <summary>
    /// A = Decree kind 1–6 (Found, Expedition, Mine, Levy, Corvée, Compact); B = target tile index, settlement index or civilization
    /// (a Free Folk village as −(settlement + 1)); C = packed extra (Compact: good | direction &lt;&lt; 8 | variant &lt;&lt; 9; Corvée: the
    /// structure). Mandate is spent only if it succeeds; it resolves in the politics step at the end of the season.
    /// </summary>
    IssueDecree = 18,
    /// <summary>A = civilization, B = good (<c>TradeGood</c>), C = 1 to stop selling it to them, 0 to lift the embargo. Free.</summary>
    SetEmbargo = 19,
    /// <summary>A = visible mining camp id, B = party size (at least 4): raid its crew and stored ore.</summary>
    LaunchMiningCampRaid = 20,
    /// <summary>A = target civilization, B = party size (at least 4): raid its closest settlement (WAR-02, contact, no alliance or truce).</summary>
    LaunchRaidAgainst = 21,
}

/// <summary>Civilization-wide stance (docs/MVP/01 §5.4).</summary>
public enum Posture : byte
{
    Balanced = 0,
    Expand = 1,
    Prosper = 2,
    Fortify = 3,
    Raid = 4,
}

/// <summary>One scheduled command. <see cref="Tick"/> is the simulation tick at which it takes effect.</summary>
public readonly record struct Command(long Tick, byte Civ, CommandKind Kind, int A, int B, int C);
