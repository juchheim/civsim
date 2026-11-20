namespace CivSim.Sim.Economy;

/// <summary>
/// The Discovery chapter's switch on the economy (docs/MVP/38, M13 WP-E6-4). Like <see cref="FiefChain"/> it is set once from the chapter
/// rules; off, the economy is byte-for-byte the Fief, Iron or Bronze economy. It contains the Faith &amp; Fief chain: turning it on turns that
/// chain (and so the Iron chain) on.
/// <para>
/// **Plumbing only.** No E6 system reads it yet, so a Discovery economy plays exactly like a Fief one except that E6 project completions are
/// recorded (<see cref="ProjectCeiling"/>). Each E6 system added later (coal and steam, ocean and colonies, factories and the rest) gates on this
/// flag and keeps its state in its own snapshot and hash section, written only when the flag is on, as the Fief state is. The raw-era lookups that
/// index the registry by research era (reach, claim radius, mortality rows read through <c>LivingEconomySource</c> and <c>PoliticsSystem</c>) read the
/// E6 row in a Discovery match; that is intended and is audited in docs/MVP/balance/m13-wp4-mode-plumbing.md.
/// </para>
/// </summary>
public sealed partial class LivingEconomyModel
{
    private bool _discoveryChain;

    /// <summary>True when the match plays the Discovery chapter. Setting it also turns the Faith &amp; Fief chain on.</summary>
    public bool DiscoveryChain
    {
        get => _discoveryChain;
        init
        {
            _discoveryChain = value;
            if (value) FiefChain = true;
        }
    }
}
