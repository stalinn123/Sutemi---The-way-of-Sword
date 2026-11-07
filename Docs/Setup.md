# Opening and finishing the setup

## 1. Open the project
1. Install **Unity 6.3 LTS (6000.3.x)** with *Windows Build Support* (PC VR) and *Android Build Support* (Quest and other standalone headsets).
2. In Unity Hub, choose *Add → Add project from disk* and select this folder. `ProjectVersion.txt` says `6000.3.0f1`; let Hub use whichever 6000.3 patch you have.
3. The first import resolves the packages and imports ~150 MB of FBX, which takes a few minutes. Accept the Input System prompt to restart the editor.

## 2. Run the setup
Run **Kendo → Setup → Run Full Setup**. It does the following:
- Project settings: linear colour space, Input System, IL2CPP + ARM64 for Android, the layers `Environment, Characters, Shinai, HitZone, XRInteractable, Teleport`, and a URP asset assigned to Graphics and every Quality level.
- Reapplies the import rules (`Scripts/Editor/Import/KendoImportRules.cs`).
- Builds prefabs: `KendoMaster`, `Shinai`, `Bogu_Men`, `Badge_01–04`, and `Env_TrainingDojo / Temple / FinalEnvironment`.
- Builds data assets from the GDD: campaign, 5 levels, 5 badges, 5 opponent profiles, conversations.
- Builds scenes `00_Boot … 06_FinalBattle_FormerStudent` and adds them to the build scene list.
- Writes `Docs/Generated/AssetValidationReport.md`.

Then run the edit-mode tests in **Window → General → Test Runner → EditMode**.

## 3. Manual steps (these can't be scripted reliably)
1. **XR Plug-in Management** (Project Settings): enable **OpenXR** on the PC and Android tabs.
2. **OpenXR → Enabled Interaction Profiles**: Oculus Touch Controller, Meta Quest Touch Pro, Valve Index, HTC Vive Controller. On Android, also enable *Meta Quest Support*.
3. **OpenXR → Render Mode**: *Single Pass Instanced*.
4. If you use XR Hands, enable the *Hand Tracking Subsystem* feature.
5. **XR Interaction Toolkit → Samples → Starter Assets → Import**. This provides the XR Origin prefab and input actions for the next phase.
6. Copy the missing textures into each `Art/**/Textures/` folder (see the `REQUIRED_TEXTURES.md` files there).

## Packages (`Packages/manifest.json`)
Versions are the ones Unity lists as *released for 6000.3*.

| Package | Version | Why |
|---|---|---|
| Universal RP | 17.3.0 | VR-friendly renderer, single-pass instanced, works on Quest and PC |
| Input System | 1.20.1 | Required by XRI and OpenXR |
| XR Plug-in Management | 4.6.1 | Loader management |
| OpenXR | 1.16.1 | One runtime for Quest, Index, Vive and WMR |
| XR Interaction Toolkit | 3.3.2 | XR Origin, grab/attach for the shinai, UI interaction |
| XR Hands | 1.7.3 | Optional hand tracking and hand visuals |
| Animation Rigging | 1.4.1 | IK for the master's demonstrations and opponent aim/hand placement, once rigged |
| Timeline | 1.8.13 | Opening sequence and story beats (GDD §13.1) |
| Cinemachine | 3.1.7 | Non-VR cut-scene and spectator cameras, plus trailer capture |
| ML-Agents | 4.2.0 | Offline training of opponent policies with imitation + RL (research paper) |
| Inference Engine | 2.6.1 | Runs the trained opponent models in the headset |
| uGUI (incl. TextMeshPro) | 2.0.0 | World-space dialogue and badge UI |
| Test Framework | 1.6.0 | Edit-mode tests in `Tests/EditMode` |

Suggested later, not added yet: **Addressables** (stream the two 2.5 M-tri environments), **FBX Exporter** (round-trip the rigged master), **Localization** (subtitles).
