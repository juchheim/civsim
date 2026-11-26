namespace CivSim.Sim.Legends;

/// <summary>
/// Legend names (docs/MVP/40 §6): a name is a code over three short syllable tables, so a record carries one integer and the presentation composes the text
/// ("Ama", "Dovirath"). The civilization suffix ("of the River Folk") is the presentation's. Pure and allocation-free except <see cref="Compose"/>.
/// </summary>
public static class LegendNames
{
    private static readonly string[] Starts =
        { "A", "Be", "Ca", "Da", "E", "Fe", "Ga", "Ha", "I", "Jo", "Ka", "Le", "Ma", "Ne", "O", "Pa", "Ra", "Se", "Ta", "U", "Va", "We", "Yo", "Za" };
    private static readonly string[] Middles = { "", "ma", "ni", "ra", "lo", "ke", "su", "ta", "vi", "do", "an", "el" };
    private static readonly string[] Ends = { "", "n", "r", "s", "th", "l", "na", "ra", "ta", "mi" };

    /// <summary>How many distinct names the tables make.</summary>
    public static int Count => Starts.Length * Middles.Length * Ends.Length;

    /// <summary>The code of the <paramref name="index"/>th name (0 up to <see cref="Count"/>).</summary>
    public static int Code(int index) => index;

    public static string Compose(int code)
    {
        if (code < 0 || code >= Count) code = 0;
        int end = code % Ends.Length, middle = code / Ends.Length % Middles.Length, start = code / (Ends.Length * Middles.Length);
        return Starts[start] + Middles[middle] + Ends[end];
    }
}
