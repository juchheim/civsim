using System.Collections.Generic;
using System.Text;
using CivSim.Game.Ui;
using CivSim.Sim.View;
using Godot;

namespace CivSim.Game;

/// <summary>
/// The Discovery-chapter section of the People sheet's Overview tab (docs/MVP/38 §8, M13 WP-E6-11): what the viewer's own civilization has reached
/// (ocean reach, colonies, the exchange, the road to the Industrial Revolution) and what each of its towns runs (coal, engines, factory, garden, society, fort).
/// It reads only <see cref="MatchFrame.Discovery"/> and is hidden in every other chapter.
/// </summary>
public partial class Game
{
    private PeopleSection? _discoverySection;
    private Label? _discoveryCiv, _discoveryTowns;

    private PeopleSection BuildDiscoverySection()
    {
        PeopleSection section = NewPeopleSection("The age of discovery", "DiscoverySection", "What your realm has reached, and what each town runs.");
        _discoveryCiv = UiKit.Text("", "Body", wrap: true);
        _discoveryTowns = UiKit.Text("", "Caption", wrap: true);
        section.Body.AddChild(_discoveryCiv);
        section.Body.AddChild(_discoveryTowns);
        _discoverySection = section;
        UiKit.SetVisible(section.Panel, false);
        return section;
    }

    private void RefreshDiscoverySection(MatchFrame frame)
    {
        if (_discoverySection is null || _discoveryCiv is null || _discoveryTowns is null) return;
        DiscoveryFrame d = frame.Discovery;
        UiKit.SetVisible(_discoverySection.Panel, d.Active);
        if (!d.Active) return;

        var civ = new List<string>();
        civ.Add(d.OceanReach ? "Your ships cross the deep ocean." : "Your ships keep to coasts and island chains.");
        if (d.Colonies > 0) civ.Add($"{d.Colonies} overseas {(d.Colonies == 1 ? "colony" : "colonies")}.");
        if (d.ExchangeStaffed) civ.Add("A staffed exchange house" + (d.JointStockOpen ? ", with joint-stock voyages" : "") + (d.InsuranceOpen ? " and insurance." : "."));
        if (d.SocietyStaffed) civ.Add("A learned society gathers your scholars.");
        civ.Add($"{d.Key7Permille / 10.0:0.#}% of the way to the Industrial Revolution.");
        UiKit.SetText(_discoveryCiv, string.Join(" ", civ));

        var towns = new StringBuilder();
        int shown = 0;
        for (int i = 0; i < frame.SettlementCount && i < d.CoalLitres.Length; i++)
        {
            if (!PeopleInScope(frame, i)) continue;
            var parts = new List<string>();
            double coal = Units(d.CoalLitres[i]);
            if (coal >= 0.5) parts.Add($"{FormatUnits(coal)} L coal");
            if (d.Engines[i] > 0) parts.Add($"{d.Engines[i]} steam {(d.Engines[i] == 1 ? "engine" : "engines")}");
            if (d.Ovens[i] > 0) parts.Add("coke oven");
            if (d.Canal[i] != 0) parts.Add("canal");
            if (d.Factory[i] != 0) parts.Add($"factory at {Units(d.FactoryCoverage[i]) * 100:0}% power" + (d.Operatives[i] > 0 ? $", {d.Operatives[i]} operatives" : ""));
            if (d.GardenStaffed[i] != 0) parts.Add("botanical garden");
            if (d.Society[i] != 0) parts.Add("learned society");
            if (d.Bastion[i] != 0) parts.Add("bastion fort");
            if (d.GalleonCrewed[i] != 0) parts.Add("galleon in port");
            if (d.ExchangeHouse[i] != 0) parts.Add("exchange house");
            if (d.Colony[i] != 0) parts.Add("overseas colony");
            if (parts.Count == 0) continue;
            if (towns.Length > 0) towns.Append('\n');
            towns.Append(SettlementLabel(frame.SettlementIndex[i])).Append(": ").Append(string.Join(", ", parts)).Append('.');
            shown++;
        }
        UiKit.SetText(_discoveryTowns, shown == 0 ? "No town has taken up coal, steam or the new works yet." : towns.ToString());
    }
}
