# CityBuilder.Client.Editor

**Path:** `client/CityBuilder/Assets/_Project/Editor/` · **Kind:** Unity assembly (`CityBuilder.Client.Editor.asmdef`, C# 9), Editor only ·
**References:** none (only Unity's own editor and engine modules) · **Referenced by:** nothing

## Purpose
Scripts that run only in the Unity Editor and change how the project's assets are imported, so that no setting depends on someone's clicks
(`core-instructions/09-unity-client.md` rule 1, CR-0005). It is **not** part of a player build, and it is not responsible for showing anything in the
game: drawing the models is `CityBuilder.Client.Presentation`.

## Key types
| Type | Responsibility |
|---|---|
| `ArtImportPostprocessor` | An `AssetPostprocessor` over `Assets/_Project/Art/`. A palette under `Art/Textures/` gets Point filter, no compression, no mips and Clamp. A model under `Art/Models/` gets scale 1, no cameras, lights, blend shapes, animation or colliders, the file's normals and no tangents. After the import it links the model's material `M_Palette_Era1` to the one shared material `Art/Materials/M_Palette_Era1.mat` (an external object remap, kept in the model's `.meta` file) and imports it once more. It makes that material when it is missing: URP Lit, Base Map the palette, Smoothness 0.1 |

## Rules and invariants
- The assembly is Editor only and references none of the client's assemblies (`ClientAssemblyTests`).
- The material link is made in `OnPostprocessAllAssets`, not while a model imports: loading another asset is safe only after the import.
- A model that has the link is left alone, so importing a project with the committed `.meta` files changes no file. This was checked with an empty
  `Library` folder (CR-0005-T2).
- A new FBX with a material of another name would keep its own material: the Blender build refuses a second material for Era I, and
  `ArtImportTests.EveryModel_Renderers_UseOnlyTheSharedMaterial` fails. A new era's palette needs a new source name and material here.

## How to extend
Another editor-only tool goes in this folder (recipe "New client screen or view" does not apply: this is not a view). The settings of the art are
described for artists in `art/README.md`; change both together.

## Tests
`Assets/_Project/Tests/EditMode/Art/ArtImportTests.cs` reads the imported assets, so the test assembly needs no reference to this one: the palette's
settings, the shared material, the settings and the material of every model, `house_l1`'s size and anchors, and a new FBX without a `.meta` file getting all of it
on its first import. Run with `scripts/dev/test-unity.sh` (`client/README.md` → Run the tests).

## Known limitations
- One palette only: the source material name and the material path are constants for Era I. Era II changes this.

Last updated: CR-0005-T2
