# M11 WP10: the Iron chapter in the view layer and the game

Iron chapter only: every new frame field is empty and `IronFrame.Active` is false in any other chapter, the new orders refuse there, and the game shows the new rows only when the frame says the chapter is Iron.

## Frame block (`IronFrame`, `FrameBuilder.Iron.cs`)

Built from the same state the rules use, filtered by what the viewer may see. Arrays only grow, so building a frame still allocates nothing once warm.

| Data | Who sees it |
| --- | --- |
| Works counts per Iron structure kind for each settlement (stride `WorkKinds`; ids from `LivingEconomyModel.IronWorkId`) | Settlements the viewer sees now, its own, or everything when omniscient; a remembered town shows none. |
| Soldiers in post, scholars, missionaries, the Iron structure being built and its progress, the settlement this town's labor is pledged to, the strongest foreign faith in it | The viewer's own towns only. |
| The share of a seen foreign town that follows the viewer's faith | Seen foreign towns (not Free Folk). |
| Sieges: besieger, engines, wall integrity (1000 intact, 0 breached), seasons | The besieger, the besieged, or anyone who sees the town. |
| Blockades: harbor, civ, the galley's home | The two sides, or anyone who sees the harbor. |
| Tributes: overlord, subject | The two sides. |
| Leagues: member masks | Members. |
| Wonders: holder settlement for the Library, Lighthouse, Dome | When the town is known to the viewer. |

## Events

`ViewEventKind` 69 to 74 and `ChronicleKey` 111 to 116 (append only, with `chronicle.*` text keys): blockade began and ended, tribute agreed and ended, league joined and ended. Each comes once per side, so a third party sees none.

## Commands (append only)

`CommandKind` 24 `Blockade` (A = rival harbor settlement), 25 `ImposeTribute` (A = civilization), 26 `FormLeague` (A = civilization); `PlayerCommands.Blockade`, `ImposeTribute`, `FormLeague`. They run the same rules as the AI's orders and a refusal is an `OrderRefused` event with the usual reason.

## Game

`Game.Diplomacy.cs` gets three rows in the existing order list, using the existing row pattern: **Form league** (friendly, shown for an ally), **Impose tribute** and **Blockade harbor** (hostile, shown at war, each with a confirm step). Each row states in its hint what it needs. I also fixed a duplicate local declaration at `Game.Diplomacy.cs:465` that stopped the game project compiling.

## Not done, and why

The game now compiles, but none of this has been run in Godot, and the game does not yet draw several E3 equivalents either (compact and pact events, for one). The cards in doc 25 §12 that need new screens are left for a pass in the editor: tool coverage and replacement inputs, mine and forge and fuel bottlenecks, grain donors, water and sanitation, province strain, siege integrity and breach ETA, the blockade route, the tribute and league cards, and the KEY-5 countdown. Their data is in the frame (or already in the E3 economy block); the screens, the Chronicle prose for the new keys and the "built, queued or dormant" outcome line after each research completion are presentation work that needs the editor. Research does not fabricate a structure to satisfy a visual gate, and nothing here does.
