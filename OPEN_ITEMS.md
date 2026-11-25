# Open items

## 1. Does recentering change what is missed?

**Status:** open. Not tested on a headset yet.

**Question.** Does recentering the headset before a test change how many flashes are missed, especially at 25 degrees?

**Why it is open.** Early single runs hinted at fewer misses after recentering, but flash size, practice and recentering changed
together, and one run of each is too few to tell them apart.

**What the code does now.** "Straight ahead", the center of the area in which targets appear, is set from the head direction when
the test starts. Recentering before START should therefore make no difference. Recentering *during* a test used to leave straight
ahead where it was; it now discards the trial in progress and sets straight ahead again. Automated tests cover this, but it has not
been checked on a headset.

### Before starting

- [ ] Build and install a new APK (**FOVTest > Build APK**). Older builds have neither the companion file nor the recentering fix.
- [ ] Headset brightness fixed, automatic brightness off, headset fitted the same way for all runs.
- [ ] One participant ID that is used for nothing else.

### The test

Four short runs, Training-30deg, **flash size 0.43 deg**, both eyes, blind-spot checks on, same person, same session:

| Run | Recentering before START | Log file | Done |
|---|---|---|---|
| 1 | without | | [ ] |
| 2 | with | | [ ] |
| 3 | with | | [ ] |
| 4 | without | | [ ] |

This order (A B B A) balances practice and tiredness between the two conditions.

Rules for every run:

- Look at the orange target until the flash has ended. **Flash seen:** move the pointer into its area. **No flash seen:** pull the trigger.
- "With" means recentering once, just before START, looking straight ahead. Never recenter during these runs.
- Do not change the settings, the headset fit or the chair position between runs.
- Finish all 34 locations. A run stopped early has to be repeated.

### What to look at

From each log, on first presentations only (retests make missed locations count twice):

- [ ] Not seen at 5, 15 and 25 degrees, per run. Compare runs 1 and 4 with runs 2 and 3.
- [ ] Whether the same locations are missed each time, in one eye or both.
- [ ] Response times and wrong areas, to see whether practice explains the change.

From the companion files in `LogsExtra`:

- [ ] `HeadFromTargetDeg` at the flash: was the target centered on the head (expected within about 3 degrees)?
- [ ] `TargetFromStraightAheadDeg` and `StraightAhead`: was the target area where the head was comfortable?
- [ ] `HeadRollDeg`: was the head tilted?
- [ ] `Discarded`: was any trial interrupted or recentered? If so, repeat that run.

### How to read the result

| Result | Reading |
|---|---|
| Runs 2 and 3 clearly better than 1 and 4, and the companion files show a difference in centering | Recentering matters. Find out why before testing participants |
| All four runs similar | Recentering does not matter. Missed flashes at 25 degrees have another cause |
| Better from run to run, whatever the condition | Practice. Add a practice run before the real ones |

With four runs only a large difference will show. If the result is unclear, repeat the block of four with other people.

## 2. Recentering during a test, on the headset

- [ ] Recenter once in the middle of a run. Expected: the trial in progress disappears, the next target appears near the new
  straight ahead, the companion file has a `recentered` entry, and the location is tested again.
