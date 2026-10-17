# Third-party notices

Almost everything in this project is generated from code and original to it. The exceptions are
listed here, with their licences. Every third-party asset is under a licence that allows it to be
shipped in this public repository and its public demo.

## Quaternius - Universal Animation Library (Standard)

- Files: `Assets/_Project/Art/ThirdParty/Quaternius/UniversalAnimationLibrary/`
- Source: https://opengameart.org/content/universal-animation-library
  (also https://quaternius.com/packs/universalanimationlibrary.html)
- Author: Quaternius
- Licence: CC0 1.0 Universal (public domain dedication) - see `License.txt` beside the files
- Used for: the humanoid mannequin rig and its locomotion, roll, sword, hit and death clips

## Quaternius - Universal Animation Library 2 (Standard)

- Files: `Assets/_Project/Art/ThirdParty/Quaternius/UniversalAnimationLibrary2/`
- Source: https://opengameart.org/content/universal-animation-library-2
  (also https://quaternius.com/packs/universalanimationlibrary2.html)
- Author: Quaternius
- Licence: CC0 1.0 Universal (public domain dedication) - see `License.txt` beside the files
- Used for: the sword combo, block, dash, knockback and get-up clips

## Poly Haven - Stone Tiles 02, Castle Brick 07, Medieval Blocks 02

- Files: `Assets/_Project/Art/ThirdParty/PolyHaven/<asset>/`
- Source: https://polyhaven.com/a/stone_tiles_02, https://polyhaven.com/a/castle_brick_07,
  https://polyhaven.com/a/medieval_blocks_02 (1K JPG maps from dl.polyhaven.org)
- Authors: Charlotte Baglioni (Stone Tiles 02); Rob Tuytel (Castle Brick 07, Medieval Blocks 02)
- Licence: CC0 1.0 Universal (public domain dedication) - see `License.txt` beside the files
- Changed: each set's `_arm` map is repacked as `_mask` (metallic, occlusion, smoothness) for URP Lit
- Used for: the cathedral floor, walls and columns

CC0 requires no attribution. It is given anyway, because the work is excellent and credit costs
nothing.

## Kenney - Impact Sounds and RPG Audio

- Files: `Assets/_Project/Audio/ThirdParty/Kenney/ImpactSounds/`, `Assets/_Project/Audio/ThirdParty/Kenney/RPGAudio/`
- Source: https://kenney.nl/assets/impact-sounds, https://kenney.nl/assets/rpg-audio
- Author: Kenney (www.kenney.nl)
- Licence: CC0 1.0 Universal (public domain dedication) - see `License.txt` beside the files
- Used for: shield blocks, blade deflects and parries, body hits, stone footsteps, the brute's footfalls,
  blade draws, armour movement and the execution

## Freesound - individual sounds

- Files: `Assets/_Project/Audio/ThirdParty/Freesound/` (roars as downloaded; `Takes/` holds single sounds cut from
  compilations at their quiet gaps, and a 60-second seamless loop of the ambience)
- Licence: every one CC0 1.0 Universal (public domain dedication), as listed on its Freesound page
- Sounds (id, title, author):
  - 489901 Scary Monster Roar #2 - NicknameLarry (the brute's roar)
  - 132874 Monster Short Roar - ecfike (the brute's roar)
  - 497056 Low Monster Roar - Robson220pl (the brute's death)
  - 125405 Monster Groans, Grunts, Slobbers - jasonLON (the brute's wind-up grunts)
  - 869056 Monster_Growls_Grunts_10 - SignatureSoundsOrg (the brute's wind-up grunts)
  - 507470 Several low fast swooshes - Danjocross (the great sword's swings)
  - 317849 Whoosh Swipe Fight Sounds Sword Air - benniknop (the knight's sword swings)
  - 471097 Knife/sword swing - spycrah (the knight's sword swings)
  - 174279 Cathedral Ambience 1 (XY Stereo) - Quistard (kept in the repository; no longer played since the
    room sound became the generated wind and embers)
- Source: https://freesound.org/people/<author>/sounds/<id>/

## Sketchfab - "Igris-Boss (Solo Leveling Arsie)" (the boss)

- Author: 20062020year on Sketchfab, who credits nekopixil (https://www.deviantart.com/nekopixil)
- Source: https://sketchfab.com/3d-models/ec6c37cb8d31456ea7acaf1a443a73aa
- Licence: CC Attribution 4.0 (CC BY 4.0), as listed on its Sketchfab page
- Files: not in this repository. Downloaded into the git-ignored `Assets/_Project/Art/Licensed/` folder and built
  into the player; a clone without it builds the earlier boss instead.
- Changes: rescaled; lower levels of detail and the built-in weapon removed; armour, seam glow and plume colour
  regenerated from the model's own maps.
- The character is from Solo Leveling; this free, non-commercial demo uses the fan model as published.

## Cardinal Zebra - "LowPoly Fantasy Weapon Pack" (the knight's sword and greatsword)

- Files: `Assets/_Project/Art/Licensed/Weapons/Low Poly Fantasy Weapon Pack/` (not in the repository; downloaded by the
  developer)
- Author: Rostislav Gnatovsky (Cardinal Zebra), https://cardinalzebra.itch.io/low-poly-fantasy-weapon-pack
- Licence: CC0 1.0 Universal. Credit is not required; it is given anyway.

## OpenGameArt - "Energy Sword" (the knight's energy blade)

- Files: `Assets/_Project/Art/Licensed/Weapons/Energy Sword/` (not in the repository; downloaded by the developer and
  converted from glTF to OBJ for import)
- Source: https://opengameart.org/content/energy-sword
- Licence: CC0 1.0 Universal. Credit is not required; it is given anyway.
