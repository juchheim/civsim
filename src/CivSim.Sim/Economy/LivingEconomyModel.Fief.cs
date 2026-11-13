namespace CivSim.Sim.Economy;

/// <summary>
/// The Faith &amp; Fief chapter's switch on the economy (docs/MVP/27, M12 WP-E5-4). Like <see cref="IronChain"/> it is set once from the chapter
/// rules; off, the economy is byte-for-byte the Iron or Bronze economy. It contains the Iron chain: turning it on turns the Iron chain on.
/// <para>
/// **Plumbing only.** No E5 system reads it yet, so a Fief economy plays exactly like an Iron one except that E5 project completions are
/// recorded (<see cref="ProjectCeiling"/>). Each E5 system added later (power ledger, blast furnace, monasteries, powder and the rest) gates on
/// this flag and keeps its state in its own snapshot and hash section, written only when the flag is on, as the Iron state is. The raw-era
/// lookups that index the registry by research era (reach, claim radius, mortality rows read through <c>LivingEconomySource</c> and
/// <c>PoliticsSystem</c>) will read the E5 row in a Fief match; that is intended and is audited in docs/MVP/balance/m12-wp6-mode-plumbing.md.
/// </para>
/// </summary>
public sealed partial class LivingEconomyModel
{
    private bool _fiefChain;

    /// <summary>True when the match plays the Faith &amp; Fief chapter. Setting it also turns the Iron chain on.</summary>
    public bool FiefChain
    {
        get => _fiefChain;
        init
        {
            _fiefChain = value;
            if (value) IronChain = true;
        }
    }
}
