# M9 first-time player playtest

## Goal

Observe 5–8 people who have never played CivSim. Do not explain the controls or the game before a run. Record whether each person reaches a meaningful research assignment in 30 seconds, completes the first 10 minutes without help, wants to continue after the end screen, and can describe one AI action they saw. The M9 P9 target is at least 5 of 8 people who both want to continue and can explain an AI action.

## Prepare each session

1. Use a fresh game profile and a fresh match. Remove or rename the prior save and replay files under Godot's user-data directory, then launch with `--show-onboarding`.
2. Set the game to Standard speed and a visible 1600×900 window. Use the same build and settings for every participant.
3. Tell the participant only: **“Please play this game as you normally would. I’ll stay quiet and take notes. Say what you’re thinking if that feels natural.”** Do not point out the research web, slot meanings, controls, Council, or AI explanations.
4. Start a real-time stopwatch when the game window appears. Do not use the in-game clock for the 30-second first-choice measure.
5. Observe silently through the 10-minute onboarding. If the participant asks how to do something, say: **“Please do what you would do if I weren’t here.”** Record the question and continue without answering.
6. Let the match continue to its normal end screen. Participants may change speed or pause on their own. Do not skip ahead or operate the game for them.

## Ask after the end screen

Ask in this order, without suggesting answers:

1. “Would you choose to play more of this game? What makes you say that?”
2. “What is one thing an AI civilization did during the match? What did you see that told you it happened?”
3. “In your own words, what did Primary, Secondary, and Tertiary mean?”
4. “Was there a point where you wanted to do something but could not figure out how? What were you trying to do?”

Do not explain the game until all answers are recorded.

## Session record

Use an anonymous participant code. Record behavior and verbatim answers; do not record names or contact details.

| Field | Observation |
|---|---|
| Participant code / date / build | |
| First research assignment (seconds) | |
| First assignment action (set slot / queued / other) | |
| Needed help or asked a how-to question? At what point? | |
| Completed the 10-minute sequence without moderator help? | |
| Did they notice a project Moment and describe its effect? | |
| Did they notice an AI action? Which one? | |
| Wants to continue after the end screen? (yes/no) | |
| Can explain one observed AI action? (yes/no; quote) | |
| Primary / Secondary / Tertiary explanation (quote) | |
| Most confusing point / exact observed behavior | |
| Most engaging point / exact observed behavior | |

## Optional island check (docs/MVP/21)

Only for sessions that run past the Outriggers project: did the participant notice a village on an island, or a party in boats,
and could they say how it got there? Note the toast ("A village on an island") and whether the boats under the party were read as
boats. Do not prompt.

## UI measures to add to each record

The redesign in [20-ui-design-spec.md](20-ui-design-spec.md) §7.2 lists the observable UI measures (time to first assignment and how it was made, first pause and how, first zoom and input device, accidental Beacons (target 0), Esc success, whether the player can say why an Almost-ready project cannot start, Moment engagement, whether they use a civ name when explaining an AI action). Record them next to the P9 questions.

## Result

Report the number of first-time participants, the median and range for time to first assignment, how many completed the first 10 minutes without help, and the count who both want to continue and explain an AI action. P9 passes at **5 or more combined successes** in a group of 5–8; preserve the denominator and individual anonymous records. Summarize the three most repeated confusions and the changes they suggest.

### Early pilot · 2026-10-21

One first-time player reached 10:00 in an uncoached run. This is an early usability observation, not a P9 result; the match was not continued to its end screen.

| Participant | First assignment | Action | Reached 10:00 | Slot explanation | End-screen questions |
|---|---:|---|---|---|---|
| P01 | Estimated 5–8 seconds | Set as Primary | Yes | Primary is most critical; Secondary next; Tertiary is less time-sensitive | Continue intent and AI action explanation not assessed |

The player reported that open slots were easy to miss, project prerequisites were hard to read, repeat slot choices felt tiring, Pause was not discoverable, project IDs were less helpful than names, the drag instruction was confusing, and Escape did not close the web. These findings are being addressed in the M7d usability follow-up. The first ten-minute run is complete; the post-fix confirmation and the remaining 4–7 first-time players are still needed for M7d/M9 closure.
