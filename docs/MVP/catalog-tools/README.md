# Reference catalog tools (Python)

Throwaway-grade scripts that were used to design and check the catalog. They parse the **markdown tables** in `03a`–`03c`. They are kept as the **reference implementation** for the C# validator specified in [16-catalog-format-and-tooling.md](../16-catalog-format-and-tooling.md), and the C# tool must reproduce their results (the golden numbers in 16 §6). Once the YAML catalog exists they are retired.

```bash
python3 -I validate_catalog.py <docs/MVP dir> graph.json   # references, cycles, era order, cross-theme, counts
python3 -I gate_analysis.py graph.json                      # which [Seen] gates block keystones
python3 -I critical_paths.py graph.json                     # minimal closure and in-era critical path per keystone
python3 -I check_openings.py graph.json                     # the Grower / Builder / Wanderer openings are legal
```

`graph.json` is written by `validate_catalog.py`. All scripts use only the standard library.
