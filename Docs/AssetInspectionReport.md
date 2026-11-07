# Asset inspection report: Kendo: The Master's Legacy

How this was inspected: all nine FBX files were parsed directly in binary, without Autodesk's FBX SDK. The parser script and the raw per-file JSON dumps are in `Docs/Reference/FbxInspection/`. Sizes are given in metres: FBX units divided by 100, with object transforms applied. **No Unity editor was available**, so the Unity-side numbers are predictions. `Kendo > Validate > Asset Report` measures the same things inside Unity and writes `Docs/Generated/AssetValidationReport.md`.

## Facts common to every file

| Property | Value |
|---|---|
| Format | Binary FBX 7.4, exported from Blender (4.4.3: master, helmet, final env · 5.2.0 LTS: badges, dojo, temple) |
| Axes / units | Y-up, Z-front, `UnitScaleFactor = 1` (cm). Blender object scale 100. Unity file scale 0.01 |
| Frame rate | 24 fps |
| Embedded textures | **None in any file.** All texture paths are absolute paths on the authors' machines (`/Users/sannthosh/…`, `C:/Users/gargi/…`) |
| Cameras / lights | Present in every file (render-scene leftovers). **Not imported** |
| Blend shapes | None |

## Per-asset findings

### 1. Kendo master + shinai: `KendoMaster_And_Shinai.fbx` (was `samurai_and_sword.fbx`)
| | |
|---|---|
| Hierarchy | `Personaje_low` (empty) → 10 meshes: Cuerpo (body), Parte_Arriba (gi top), Lompa (lower garment), Capa (cape), Calzado (footwear), Medias (socks), Sombrero (straw hat), Masc (bronze face mask), Ojos (eyes), Frasco (flask). The sword is 7 root meshes: `Cylinder` shaft, `Cylinder.001` leather grip, `Torus.005` tsuba, and 4 thin binding rings |
| Geometry | Character 159,057 tris · shinai 39,808 tris (the shaft alone is 31k, which is heavy for a prop) · plus a 335 m ground plane |
| Scale | Character 4.41 m tall including the hat, so authored **~2.44× life size**. Scale factor **0.41** gives 1.81 m. Shinai 3.21 m → **1.31 m** at the same factor (a real adult shinai is ~1.17–1.20 m, so slightly long) |
| Placement | Character stands 52.6 m from the origin (x). A-pose, facing +Z. Shinai stands upright beside it, grip at the bottom |
| Rig / skin | **No armature, no skin clusters, no bind pose.** It's a static mesh |
| Animation | 2 animation stacks (`Lompa_low…Take 001`) with **0 s duration**. Empty, not imported |
| Materials | 17 Phong materials referencing 25 unique external textures (Poliigon WoodVeneerOak, MetalBronzeWorn, MetalGoldPaint, RattanWeave; ambientCG FabricPlainNaturalSheer, FabricLeatherCowhide, TowelCotton). List: `Art/Characters/KendoMaster/Textures/REQUIRED_TEXTURES.md` |
| UV / other | UV set `map1` on all meshes. Calzado and Medias have a second UV set. Vertex colours on Calzado and Cuerpo |
| Integration | Kept as supplied. On import the ground plane is removed and the sword is grouped into a `Shinai` node, pivoted at the grip end with +Y toward the tip, plus `GripEnd` / `Tsuba` / `Tip` markers. Prefabs: `KendoMaster.prefab` (feet at origin, faces +Z, carries the shinai on its `ShinaiRest` socket) and `Shinai.prefab` (for the player's VR hands) |

### 2. Kendo helmet (men): `Bogu_Men_Helmet.fbx` (was `helmate1.fbx`)
| | |
|---|---|
| Geometry | 38 unique meshes instanced into 115 objects (stitches, quilt lines, 13 cage bars), 154,384 tris |
| Scale | 5.17 × 3.31 × 2.23 m. Factor **0.16** gives 0.83 × 0.53 × 0.36 m. The width comes from the two shoulder flaps, which are modelled spread outward |
| Materials | 9 flat-colour materials (Indigo Cloth, Cage Steel, Red Lacquer, Cream/Black Leather, Grey Piping, Quilt Line, Silver Mesh Plate, Cream Thread). No textures |
| Rig / animation | None |
| Integration | `Bogu_Men.prefab`, pivot at bottom centre, tagged `EquipmentPiece(Men)` for opponents |

### 3–6. Badges: `Badge_01…04.fbx` (were `bage1…4.fbx`)
| | |
|---|---|
| Geometry | 32 meshes, ~28.2k tris each. **The four files are identical except the `Text` mesh** (934 / 1,019 / 1,077 / 945 vertices) |
| Scale | 5.33 m disc lying flat. Factor **0.025** gives a 0.13 m badge |
| Defects | `Object` is a stray copy of the centre torus floating **24 m above** the badge, and is removed on import. `Circle`, `Cylinder.001`, `Torus`, `Torus.001`, `Torus.002`, `Cube.010` have no material or degenerate geometry (Blender helpers or boolean cutters), and are kept but disabled |
| Materials | 25 materials, **all exported as default grey (0.8)**. The real colours were in Blender shader nodes, which FBX doesn't carry. They need to be recreated in URP |
| Mapping | Assumed `bage1 → Level 1 … bage4 → Level 4`. Check the text on each |
| Gap | The GDD has **5 badges**. The Master's Legacy badge has no model |

### 7. Training dojo: `TrainingDojo_Interior.fbx` (was `trainining_dojo_interior.fbx`)
| | |
|---|---|
| Geometry | 991 small meshes, only **26,372 tris**. Very cheap |
| Scale | **Already real scale**: 17.2 × 13.2 m stone foundation, wooden platform with floorboards at **0.45 m**, roof ridge at 9.2 m, 60 × 60 m gravel ground |
| Content | Walls, shoji, tatami, scrolls, lanterns, rope, bamboo, floor tape, two bogu display stands (men/do/tare), bokken rack. This matches the GDD §13.3 hub well |
| Materials | 23 flat-colour materials. Several came through as 0.8 grey (node-based colours lost). 10 lights, not imported |
| Integration | `Env_TrainingDojo.prefab`, static, lightmap UVs generated. Used for the Training Center and Levels 1–2 |

### 8. Temple: `Temple_HoryujiReplica.fbx` (was `dojo2.fbx`)
| | |
|---|---|
| Origin | "japanese-horyu-ji-temple-replica". Maya/Arnold scene (`aiStandardSurface` materials, `pCube`/`sweep` names) re-exported via Blender |
| Geometry | 30 meshes, **2.53 M tris**. Three roof-tile meshes alone are 255k–434k vertices. There is a Megascans-style ivy (`Broom_Creeper…`) with LOD0–4 empties and only LOD5 holding geometry (287k vertices) |
| Scale | Real scale (~21 × 18 m hall, 30 × 26 m base). Factor 1.0 |
| Materials | 23 materials, 47 external textures (BaseColor / Roughness / Metallic per surface). List in `Temple/Textures/REQUIRED_TEXTURES.md` |
| Integration | `Env_Temple.prefab`, static, no lightmap UVs (too large). Used for Levels 3–4 |

### 9. Final environment: `FinalEnvironment.fbx` (was `finalenvi.fbx`)
| | |
|---|---|
| Geometry | 54 unique meshes instanced into 856 objects (819 are foliage-card instances), **2.56 M tris** |
| Content | The **same temple as `dojo2`, but 2.44× larger**, plus surrounding buildings (`node_0.*`), a 425 m ground plane, a 720 m HDRI sky sphere, a rhododendron armature and an animated insect |
| Scale | Same 2.44× authoring error as the master. Factor **0.41** |
| Animation | `Insect|fly`, 16.76 s, 197 curve nodes. 84 bones total, but **no skin clusters**, so check in Unity that the insect geometry follows its bones |
| Materials | 43 materials, 121 texture references (37 unique files), including Poliigon sets, an HDRI sky and Cyrillic-named textures from the source pack (`Без названия…`) |
| Lights | 3, two of them suns with Blender wattage intensities (64,418 / 35,673). Not imported |
| Integration | `Env_FinalEnvironment.prefab`, sky sphere disabled (use a skybox), insect left dynamic. Assigned to the final battle (GDD §13.5). Confirm that this is the intended use |

## Blocking issues before gameplay

1. **The master is not rigged.** Demonstrations (GDD §4.1), conversations and every opponent need animation. Rig the supplied mesh, for example with Blender Rigify or Mixamo auto-rig (the A-pose is suitable; the cape and hat will need weight-paint cleanup). Re-export it to the same path, set `KendoImportRules.KendoMaster` to `Humanoid` with `ImportAnimation = true`, then run *Reapply Import Rules* and *Build Prefabs*. Hand sockets are estimates until then.
2. **No textures were supplied.** 25 + 47 + 37 files are missing, so materials import with base colours only. Drop the files into the `Textures` folders and they relink by file name automatically.
3. **Environment budget.** The temple and the final environment are ~2.5 M tris each. That's fine for PC VR, but too heavy for standalone Quest (aim for under ~1 M per frame). Decimate the three roof-tile meshes and the ivy first.
4. **Missing art.** There is no opponent or former-student character, no 5th badge, and no kote/do/tare for opponents (the dojo's bogu pieces are static display props). There is also no UI font, voice audio or SFX.
5. **Licensing.** Poliigon, ambientCG, Megascans-style and Sketchfab-style sources are visible in the paths. Confirm the licences before distribution.

## Smaller follow-ups
- Pack the separate roughness and metallic maps into URP's metallic/smoothness map (R = metallic, A = 1 − roughness).
- Recreate the badge and dojo colours lost in the Blender node export.
- The shinai is 31k tris and ~1.31 m long. It could be shortened to 1.18 m and decimated for the player's hands.
- The two large FBX files are ~70 MB each: use Git LFS (`.gitattributes` is set up).
