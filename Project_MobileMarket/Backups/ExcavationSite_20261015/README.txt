Excavation site - backup taken 2026-10-15 when the map builders were retired.
From this point the scene (Assets/_Project/Scenes/Main/Excavation site.unity) is the source of truth.

Contents mirror the project paths (with .meta files, so GUID references stay valid on restore):
- Scenes/Main: the scene, terrain data, NavMesh data, hand-edit record (ExcavationSite_Edits.asset)
- Scripts/Editor: the retired builders (ExcavationSiteBuilder, ExcavationAssetBuilder, ExcavationSiteEdits)
- Materials: M_EX_* materials, TL_EX_* terrain layers
- Models/Excavation: generated meshes (water, cave cliff fill)
- Prefabs/Excavation: all P_EX_* prefabs
- Textures: T_EX_ConcretePaving.png

To restore a file, copy it (and its .meta) back to the same path under the project with Unity closed.
