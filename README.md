# FOV Test (Quest 3)

Standalone visual field test for Meta Quest 3. It needs no server and no network (the Internet permission is removed
from the app). It writes one log per test in the format of the reference `VisualField.json` (DataVersion 11).

Unity 6000.0.62f1, Meta XR Core SDK 83.0.4, Unity OpenXR 1.15.1, built-in render pipeline, Vulkan, IL2CPP ARM64.

Not a medical device, and not affiliated with Vivid Vision.

## Build and install

- Unity menu **FOVTest > Build APK** writes `Builds/FOVTest.apk`. (**FOVTest > Set up project** re-applies all settings and rebuilds the scene.)
- `adb install -r Builds/FOVTest.apk`
- The app is listed under *Unknown sources* as **FOV Test**.

## Running a test

| Control | Action |
|---|---|
| Thumbstick | Menu: up/down selects, left/right changes |
| A or X | Confirm. On *Participant ID* it starts digit editing |
| Trigger | During a trial: "I saw nothing" |
| Hold B or Y for 2 s | Stop the test early (the log is still saved) |

Each trial has two phases:

1. **Point.** Move the head so the pointer touches the target. The level bar below straight ahead shows how much of the test is done.
2. **Answer.** When the pointer locks on there is a click, a bright ring appears around the target, the green circle disappears
   and lines divide the view into six areas. Once locked, the target stays locked. 0.1 s later a spot flashes for 0.3 s in one eye. Move the pointer into its area, or pull the trigger if nothing appeared.

Then a mark and a sound say what happened: green tick = right area, red cross = wrong area, grey bar = no answer. The bar also
appears when a blind-spot check is correctly not seen; it must not reveal which trials are checks.

Areas, feedback, sound volume and the pointer in phase 2 can be switched in the menu. Taking the headset off discards only the
trial in progress.

## Logs

On the headset: `/sdcard/Android/data/com.fovtest.perimetry/files/Logs/<unix time>_<user id>_VisualField.json`

    adb pull /sdcard/Android/data/com.fovtest.perimetry/files/Logs

The log is saved when the test ends, and also when the app is paused, so an interrupted run is kept with the rest counted in
`SpotsUntested`. A participant who completes no trial, in practice or in the test, also leaves a log: `History` is empty,
`NumStimuliPresented` is 0 and every location is in `SpotsUntested`. The time limit (5 minutes for Training-30deg, 30 for 24-2)
also ends a wait for a target the participant cannot find; practice has no limit, so hold B or Y for 2 s there. The log does not
say why a test stopped: write that down. **Uninstalling the app deletes the logs.** Pull them first.

Each log has a companion with the same name in `files/LogsExtra` (pull it the same way). The log itself keeps the reference format.
The companion records, for every flash, the target direction, the head direction and roll at the flash and at the response, and
where straight ahead was; every time straight ahead was set and why; and trials discarded because of recentering or a removed headset.

Recentering during a test discards the trial in progress (its location is tested again) and moves the target area to the new
straight ahead.

Differences from the reference file:

- `AndroidSerial` is null and `MACAddress` is null: Android does not give them to apps.
- `TimeStart` and `TestDate` are headset local time; `UTCOffset` says which. (The reference mixes UTC and Pacific time.)
- `LayoutDescription` is the layout name (the reference says "Training-10deg" for the 30-degree layout).
- `Reliability` is always 0, as in the reference. It is not a computed score.
- `PixelsPerDegree` holds degrees per pixel, as in the reference (0.0703125 = 90/1280).
- A trial that is not counted (two or more frames lost from the flash, or a response within 0.15 s of it) is logged with the same `TrialIndex` as the trial after it,
  and its location is tested again later. This is how the reference log's first trial (19 of 21 frames) appears.

## Settings on the headset

`/sdcard/Android/data/com.fovtest.perimetry/files/settings.json` is written the first time a test is started. The menu covers the common
options; the rest (name, record number, session numbers, response threshold, fixation spread and so on) can be edited there.

## Luminance calibration (do this before collecting data)

There is no published photometry for the Quest 3, so the app starts with a nominal curve (100 cd/m2 peak, gamma 2.2) and logs
`IsLuminanceDataKnown: false`. Real values depend on the headset's brightness setting. To calibrate:

1. Fix the headset brightness and turn automatic brightness off. Never change it between participants. (`AndroidBrightness` in the
   log has read 255 in every run so far, so do not rely on it to notice a change.)
2. Menu > *Luminance patch*. The patch only shows grey levels to measure: it changes no setting and saves nothing. Measure each step with a photometer through the lens, then list them in `luminance.json` in the
   app's files folder:

       { "Profile": "Quest3-<headset id>", "Measured": true,
         "Params": "[0, 0.1] [0.05, 0.3] [0.1, 0.8] ... [1, 95.2]" }

## Flash size

Menu > *Flash size*: 0.43 deg (Goldmann III, the reference size, default) or 1.72 deg (Goldmann V, for low vision). It applies to
every flash of a run, practice included. With 1.72 deg the log says so in each trial (`StimDiamDeg`), in the CSV (`LayoutData`) and
in the layout name (`..._1.72deg.csv`); `ActivitySettings.Layout` keeps the base name.

Fields measured with different sizes are not comparable: the larger flash makes a field look larger and can hide small scotomas.
Choose the size before collecting data and do not mix sizes within an analysis.

## Layouts

- **Training-30deg**: the exact layout in the reference log (16 points per eye at 5, 15 and 25 degrees, plus a blind-spot check).
- **24-2**: the standard 54 points per eye plus a blind-spot check, built from the published VVP Swift parameters: 0.43 deg black
  spots, 0.3 s, 25 cd/m2 background, two retests of a missed location. The exact vendor file was not available, so location IDs
  and the dark fixation/pointer contrasts are ours.

Layouts are the CSV and JSON pairs in `Assets/FOVTest/Resources/Layouts`, in the log's own `LayoutData` format.

## What is known and what is assumed

Taken from the reference log, or from published descriptions of VVP, and reproduced by the tests:

- Weber contrast, background 2 cd/m2, stimulus 0.43 deg for 0.3 s (21 frames at 72 Hz, so the app runs at 72 Hz).
- Stimulus 0.10 s after the pointer touches the target; 0.7 s between trials; 2.0 s response timeout.
- A response within 30 degrees of polar angle is correct (ChoiceIndex 0). Six sectors, so guess rate = incorrect / (5/6) / trials.
- Eyes and locations randomly interleaved; stimulus seen by one eye; soft-edged spots; world-stationary fixation targets.
- All summary values in the reference log except `LayoutDescription` are reproduced from its trial list (`Summary_ReproducesReferenceLog` in `CoreTests.cs`).

Assumed, because neither source says (all in `settings.json` under `Test`):

- `ResponseThresholdDeg` 4: how far the pointer travels before a response registers. Reference response times do not depend on
  eccentricity, so the response is by direction, not by reaching the spot.
- `FixationSpreadDeg` 8: targets appear within 8 degrees of straight ahead. Published limit on head excursion is 22 degrees.
- The sign convention of ChoiceIndex 1 to 5, and the meaning of `NumInitialMiss` (locations that had to be presented again).
- The pointer is shown in both phases by default. `HeadCursorVisible` in the log says whether it was shown in phase 2 (the
  reference log says false, but what the vendor means by it is not documented).
- `FixationRingVisible` draws a small ring around the target in phase 1; the 32-degree circle is drawn for both eyes and
  hidden in phase 2, as in the original.
- The six areas. The reference log gives the lines' contrast (1) and extent (2 to 32 degrees), but not their number or angle. Six
  equal areas of 60 degrees follow from the 30-degree rule and the 1-in-6 guess rate, and the lines turn with each stimulus so that it is mid-area. They
  appear when the target locks, 0.1 s before the stimulus.
- `LockMaxSpeedDegPerSec` 20: the target locks only when the head is slower than this, so sweeping across it starts nothing.
- The target and pointer are orange and the circle is green, at the luminance set by the layout's contrasts.
- The answer mark, sounds and progress bar are our design, as are their sizes and brightness.

## Validity

Quest 3 is not the reference headset: field of view, optics, resolution and luminance differ, and the sphere follows the head's
position so that head translation does not move the targets. Compare with a reference test before relying on absolute results.
The app does not track the eyes. It checks fixation only through the fixation task and the blind-spot locations.

## Self-test on the headset

Checks the headset build without a wearer: holds a stimulus in the left eye, then the right, then runs the 30-degree layout with a
simulated participant. The result goes to the `SelfTest` folder, not to `Logs`.

    adb shell am start -n com.fovtest.perimetry/com.unity3d.player.UnityPlayerGameActivity --ez selftest true
