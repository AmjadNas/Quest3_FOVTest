# Open items

## 1. Does recentering change what is missed?

**Status:** open. Not tested on a headset yet.

**Question.** Does recentering the headset before a test change how many flashes are missed, especially at 25 degrees?

**Why it is open.** Early single runs hinted at fewer misses after recentering, but flash size, practice and recentering changed
together, and one run of each is too few to tell them apart.

**What the code did.** "Straight ahead", the center of the area in which targets appear, is the head direction when A is pressed on
"The test starts now". In earlier builds that text was put where the head happened to point when it appeared (usually off to one
side, after the last practice trial) and the menu text could stay up to 40 degrees off. The head turned to read it, so the center of
the test could be well away from the way the participant naturally faced. Recentering on the home screen may have changed
where the participant faced when the app opened, so it could change the center. This has not been shown to cause missed flashes.

**What it does now.** The start text stays in front of the head until A is pressed, so it cannot pull the head to one side. The menu
text follows a turn of more than 25 degrees and is placed again when tracking starts. Recentering *during* a test discards the trial in
progress and sets straight ahead again. Automated tests cover this, but it has not been checked on a headset.

**Consequence for this test.** A build with the fix may show no effect of recentering even if the old build had one. To look for the old
effect, run the block below on the old build first; its logs have no companion file, so the center cannot be checked there.

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

- [ ] `StraightAhead`: yaw and pitch of the center, in tracking space. Yaw near 0 is the headset's recentered forward. Compare the
  runs with and without recentering, and note how far each is from the way the participant sat.
- [ ] `HeadFromTargetDeg` at the flash: was the target centered on the head (expected within about 3 degrees)?
- [ ] `TargetFromStraightAheadDeg`: were the targets within 8 degrees of the center?
- [ ] `HeadRollDeg`: was the head tilted?
- [ ] `Discarded`: was any trial interrupted or recentered? If so, repeat that run.

### How to read the result

| Result | Reading |
|---|---|
| Runs 2 and 3 clearly better than 1 and 4, and the companion files show a difference in centering | Recentering matters. Find out why before testing participants |
| All four runs similar | Recentering does not matter. Missed flashes at 25 degrees have another cause |
| Better from run to run, whatever the condition | Practice. Add a practice run before the real ones |

With four runs only a large difference will show. If the result is unclear, repeat the block of four with other people.

## 2. Start centering, on the headset

- [ ] Open the app while facing to one side, then start a test facing forward. Expected: the menu text comes round to face you, the
  start text stays in front of your head, and the first target appears within about 8 degrees of where you face when you press A.

## 3. Recentering during a test, on the headset

- [ ] Recenter once in the middle of a run. Expected: the trial in progress disappears, the next target appears near the new
  straight ahead, the companion file has a `recentered` entry, and the location is tested again.
