# VR (OpenXR): what is prepared and how to test it later

> **Not tested on a headset.** There was no VR hardware and no Unity editor here.

## Prepared in code
- **Input.** `XRKendoInput` reads the tracked head, both controllers, grip/trigger/sticks and buttons through Input System XR bindings, and fills the same `KendoInputFrame` as the desktop. The game switches to it automatically when `XRSettings.isDeviceActive`.
- **Rig.** `PlayerRig.UpdateXR` uses:
  - the tracked head as the camera, with the calibrated height offset;
  - snap turn (30°) on the right stick;
  - suriashi steps on the left stick, through the same footwork model and collisions;
  - kiai on A, kamae on X, bow on Y, interact/confirm on B, pause on the left menu button, skip on a right-stick click.
- **Two-handed shinai.** `XRShinaiTracker` puts the left hand at the kashira and aims the blade from the left controller to the right controller, with roll from the right controller. Strikes, blocks and contacts then go through the same `StrikeSensor` and rules engine, so a physical swing is evaluated exactly like a desktop strike. Blocking in VR is simply holding the shinai in the path.
- **Calibration.** On the first run, stand straight in chudan and pull both triggers. This sets the eye-height offset and the arm scale, saved in settings.
- **Tracking loss.** If a controller is lost for more than 0.3 s, the shinai holds still and the game pauses with a notice until tracking returns.
- **XR UI.** `XRSupport` turns every canvas into a world-space panel 1.6 m in front of you. You select buttons by looking at them, then press B or the trigger.

## Enable and test
1. In **Project Settings ▸ XR Plug-in Management**, enable **OpenXR** for Standalone (and Android for Quest). Add your headset's interaction profile, for example Oculus Touch, Valve Index or HP Reverb G2.
2. Connect the headset (Link/SteamVR) and press Play in `00_Boot`.
3. Things to check:
   - calibration;
   - that the shinai follows both controllers;
   - that a men cut on the bogu stand in Lesson 5 is evaluated;
   - the tracking-loss pause;
   - the world-space menus.
4. Report any binding that does not respond. Bindings are strings in `XRKendoInput` and easy to adjust.

The XR Interaction Toolkit package (3.3.2, the version released for Unity 6.3) stays in the manifest for future ray interactors. The current code does not depend on it.
