# Kendo: The Master's Legacy (Unity 6.3, C#)

A desktop-playable kendo campaign, prepared for OpenXR:
- intro → dojo → 7 interactive lessons → 4 levels → journey → boss → ending;
- one shared rules engine;
- the three-agent opponent AI from Fuchs et al. (2024).

> **Read first. This project has not been opened in Unity yet.** The workspace it was built in had no Unity editor and no C# compiler, and package installs were blocked.
> - **What did run:**
>   - the Python mirrors of the rules engine and the AI, with their tests;
>   - the actual A2C training runs;
>   - asset checks against your FBX files;
>   - a static project checker.
> - **What did not run:** C# compilation, the Unity tests, scene generation and gameplay. Expect a few compile errors on first open. They will be local to one file; fix them first.

## Open and play
1. Open the folder in **Unity 6.3 LTS** (6000.3). Let the packages resolve and accept the Input System restart.
2. Run **Kendo ▸ Build Game**. It:
   - applies the project settings and import rules (scale, shinai extraction, Read/Write on the character);
   - builds the prefabs;
   - rebuilds the grey Blender materials;
   - builds the game shinai (1.20 m) and badges 1–5;
   - creates the config and the 9 scenes with mesh colliders;
   - sets the build list.

   Report: `Docs/Generated/BuildReport.md`.
3. Open `Assets/_Project/Scenes/00_Boot.unity` and press Play.

To run everything headless (build content, EditMode and PlayMode tests, desktop player): `UNITY=/path/to/Unity ./Tools/Unity/run_all.sh win`. It writes `Docs/Generated/UnityResults.md`.

**Desktop build:** run **Kendo ▸ Build Desktop Player (Windows)**, or use `run_all.sh linux|mac`. The output goes to `Builds/`.

## Controls (also shown in-game; F1 toggles the overlay)

| Input | Action |
|---|---|
| W / S | Suriashi forward / back |
| A / D | Suriashi left / right |
| Mouse | Look |
| Hold right mouse button | Aim the kensen |
| Left click or Q | Men |
| E | Kote |
| R | Do |
| T | Tsuki |
| Hold Shift | Block |
| Space | Kiai |
| X | Chudan ↔ sageto |
| Hold B, release, press B again | Bow down, pause at that depth, rise |
| F / Enter | Interact / continue |
| G | Watch the demonstration again |
| Tab | Skip |
| Backspace | Back |
| Esc | Pause |
| Hold Alt | Free the cursor |

Fumikomi is a W step timed with the strike.

## What is in the game
- **Scenes:**
  - `00_Boot`: safe save load, then the menu;
  - `01_MainMenu`: Continue, New Game, Badges, Settings, Controls, Credits, Quit;
  - `02_TrainingDojo`: the hub;
  - `03–04` Levels 1–2 in the dojo;
  - `05–06` Levels 3–4 in the temple;
  - `07_FinalBattle`: the supplied shiai-jo;
  - `08_Ending`: ending and credits.
- **Story:**
  - Narrated intro over the dojo, then the master's first meeting.
  - The master speaks:
    - before and after every lesson and level;
    - after repeated failures;
    - when you improve;
    - before the final battle (with your recorded weakness);
    - after it.
  - The final journey leads to the former student (Kuroda), who speaks before the fight.
  - Lines that judge you are built from saved statistics and evaluations. None are random.
- **Seven lessons:** introduction and rules, rei, chudan-no-kamae, suriashi, basic strikes, ki-ken-tai-itchi, zanshin. Every lesson follows the same sequence:
  1. Introduction, with new Japanese terms explained (skippable).
  2. Explanation.
  3. A live demonstration by the master on the bogu stand or the senior student acting as motodachi.
  4. Visual guides: footprints, kensen ring, target rings, floor marks, kensen trail, bow meter, timing bars.
  5. Practice: each attempt is measured from the real player state and evaluated by the rules engine.
  6. Feedback panel: correct, needs work, principle, how to improve, what next.
  7. Multiple attempts, with checkpoints after each success.
  8. Completion.

  Lessons are sequential, and completed ones can be revisited from the lesson board.
- **Player:**
  - first-person CharacterController with okuri-ashi footwork (no run or jump);
  - procedural gi sleeves and kote hands on a two-handed grip;
  - the supplied shinai in chudan, sageto, strikes and blocks;
  - the shinai is swung away from walls;
  - the furikaburi adapts to the ceiling (temple 2.58 m).
- **Combat:**
  - a strike sensor sweeps the real blade against hit zones on the opponent's rigged body;
  - the evidence it records: zone, position along the blade (monouchi), blade side, hasuji from tip velocity, swing or thrust evidence, kiai and fumikomi timing, distance, body drive, posture, opportunity, and zanshin watched for 1.8 s after contact;
  - then the engine evaluates the strike and a three-shinpan panel decides;
  - blade-on-blade blocks cause a deflection and open a counter window;
  - ai-uchi, jogai, hansoku, encho and hantei are all handled.

  **A contact alone is never a point**, and training scores are separate from ippon.
- **Levels:**

  | Stage | Opponent | Agent | Notes |
  |---|---|---|---|
  | Level 1 | Kenji | rule-based, easy | no tsuki |
  | Level 2 | Haruka | rule-based, medium | 3 shinpan |
  | Level 3 | The Shadow | Agent 2, imitating you | |
  | Level 4 | Temple champion | paper's PDDA: rule-based → A2C, fine-tuned against your imitation during the bout | |
  | Final | Kuroda | own style (feints, combos, debana-kote, hiki-waza), DDA scaling, then PDDA A2C | |

  Every stage has rei, retry, feedback, a badge, and win/loss screens.
- **Characters:** the only supplied character is the master. It is auto-rigged at runtime (17 bones, skin weights measured from the mesh) and posed procedurally with IK: hands on the shinai, feet on the footwork, bow, kneel and gestures. Opponents and the former student reuse that mesh with the supplied men, a generated do and different gi colours. **This is an adaptation, not new character art.**
- **Badges:** the supplied Badge_01–04 with level palettes. Badge 5, "Master's Legacy", is built from Badge_04's geometry with a new label.
- **Save:** JSON with a backup; corrupt files are quarantined, never overwritten. It stores progress, lessons and checkpoints, levels, badges, title, story flags, settings and performance statistics, plus the AI player model.
- **UI:** built in code in the SUTEMI mock-up style, with the master portrait cut from your mock-up. Includes fades, a loading screen, toasts, settings (volumes, sensitivity, invert, pace, FOV, quality, fullscreen) and HUDs.
- **Audio:** procedurally generated music, shinai SFX, footwork, bell, taiko and synthesised kiai (`Tools/Audio/gen_audio.py`).

AI method and real training results: [Docs/AI_Training.md](Docs/AI_Training.md). VR: [Docs/VR.md](Docs/VR.md). Dojo layout: `Docs/Generated/Dojo_LessonLayout.png`.

## Project structure
```
Assets/_Project/
  Art/            supplied FBX (byte-identical, SHA-256 in Docs/Reference/OriginalAssets.sha256) + UI sprites
  Audio/          generated WAVs           Resources/  KendoGameConfig (built), AI/opponent_a2c.json (trained policy)
  Scripts/Runtime/
    Core, Data, Characters   Phase-1 modules (kept)
    Rules     (no engine)    KendoEvaluationEngine, ShinpanPanel, KendoMatch, KendoReward, rule config + profiles
    AI        (no engine)    DuelSim, RuleBasedPolicy, AdaptiveRandomForest, ActorCritic/A2CTrainer, BackgroundTrainer, PDDA
    Campaign  (no engine)    content (7 lessons, 5 stages, badges), save model, CampaignProgress, LessonSession, MasterFeedback, MasterDialogue
    Game                     Core (root, saves, scene flow, audio), Input (desktop/XR/scripted), Characters (auto-rig, IK, factory),
                             Combat (shinai motion/rig, footwork, sensor, fighter, match, opponent brain), Player, Training (lessons),
                             UI, Scenes (directors), XR
  Scripts/Editor             import rules (Phase 1), KendoGameBuilder, material/badge rebuilders, player build
  Tests/EditMode, PlayMode   NUnit tests (golden data from the Python mirrors)
Tools/  Rules (mirror+tests) · AI (sim, agents, training, runs, golden) · AssetPrep (FBX parser) · Audio · Art · Unity · verify_project.py
```

## Test results – what actually ran (in this workspace, without Unity)

| Check | Result |
|---|---|
| `Tools/Rules/test_rules.py`: scenarios per criterion, contact-only, tsuki and left-kote rules, panel, match, kamae/rei/suriashi, 30,000 random strike invariants, monotonicity | **28/28 passed** |
| `Tools/AI/test_ai.py`: deterministic sim, passive/out-of-reach never score, tier ordering, ARF learns, A2C gradient vs finite differences, shipped policy beats the hard tier | **7/7 passed** |
| A2C training runs and evaluation (table in Docs/AI_Training.md) | completed, logs in `Tools/AI/runs` |
| 9 supplied FBX restored and matched to the Phase-1 inspection data (size, version, triangle counts) | **9/9 match** |
| `Tools/verify_project.py` (asmdef graph, references, namespace/type visibility, member names, brackets, file names) | **OK: 85 C# files, 10 asmdefs** |
| C# EditMode tests (golden replay of the engine, sim, network, ARF, campaign, save recovery, lesson evidence rules) and PlayMode tests (boot → menu, dojo spawn, suriashi step, level start) | **written, not run** |
| Unity compilation, scene generation, gameplay, desktop build, VR | **not run** |

## Remaining limitations
- **Nothing has run in Unity.** That means compilation, the tests, scene generation, playability and balance are all unverified.
- **Characters:**
  - The runtime auto-rig and IK are procedural: there are no finger bones and no cloth, and the deformation quality has not been seen.
  - Every opponent and the boss use the master's mesh (no other character exists).
  - The men helmet fit and the badge orientation and label are first guesses; they are adjustable in the config and builder.
- **Desktop limits:**
  - Grip height, hand position and feet are set by the stance model; the player cannot place them. The kamae lesson therefore checks being on the footprints, the kensen line, stillness and posture from the camera.
  - Real hand placement needs VR.
- **AI:**
  - The policies were trained and evaluated in the abstract duel simulator; behaviour in the 3D game is untested.
  - Domain randomisation did not improve results within 4 M steps.
  - The SB3/River scripts are written but not run.
- **Textures:** no texture files were supplied (as in Phase 1), so materials are colour-only reconstructions.
- **Rules:** Art. 20/26 details (one flag with two abstentions, sosai) are paraphrased switches; check them against the printed FIK regulations.
- **Editor-only behaviour:** opening a locked level directly in the editor is allowed for testing, and the result is not recorded. Builds return to the dojo.
- **Phase 2–5 code:** the earlier Phase 2–5 code was unavailable (the Phase 5 zip was not re-uploaded). These systems were rebuilt from the phase reports, not restored.
