# Scripts-COM3D2

Some COM3D2 functional scripts

## Description

| Script Name                                 | Description                                      | Require                                                                                                                                        |
| ------------------------------------------- | ------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| [change_tex_fix](#change_tex_fix)           | Fix TBody.ChangeTex when file does not exist     | -                                                                                                                                              |
| [dcm_sync_anm](#dcm_sync_anm)               | DCM Sync With Item Anm and MovieTexture          | COM3D2.DanceCameraMotion.Plugin                                                                                                                |
| [extract_ks_scripts](#extract_ks_scripts)   | Extract *.ks scripts from game                   | -                                                                                                                                              |
| [infinity_color_fix](#infinity_color_fix)   | Fix InfinityColor on Alpha Channel and Add Mask  | [*Optional*] COM3D2.MaidLoader                                                                                                                 |
| [partsedit_add_bone](#partsedit_add_bone)   | Make Mune & Hip Bone Moveable and Scaleable      | COM3D2.PartsEdit.Plugin                                                                                                                        |
| [inoutanim_sync](#inoutanim_sync)           | InoutAnim Sync                                   | COM3D2.InOutAnimation.Plugin<br>[*Optional*] COM3D2.VibeYourMaid.Plugin<br>[*Optional*] COM3D2.SceneCapture.Plugin<br>[*Optional*] vym_enhance |
| [wrap_mode_extend_sc](#wrap_mode_extend_sc) | Make textures repeated for SceneCapture          | COM3D2.SceneCapture.Plugin                                                                                                                     |
| [model_extend](#model_extend)               | Model Extend                                     | -                                                                                                                                              |
| [npr_addition](#npr_addition)               | Add Shader to COM3D2.NPRShader.Plugin            | COM3D2.NPRShader.Plugin                                                                                                                        |
| [pmat_extend](#pmat_extend)                 | Allow Mate set RenderQueue (pmat)                | [*Optional*] COM3D2.NPRShader.Plugin<br>[*Optional*] COM3D2.SceneCapture.Plugin                                                                |
| [vym_enhance](#vym_enhance)                 | VYM Function Enhance                             | COM3D2.VibeYourMaid.Plugin                                                                                                                     |
| [mipmap_extend](#mipmap_extend)             | Enable Mipmap for textures with `mipmap` in name | -                                                                                                                                              |
| [mate_tex_cache](#mate_tex_cache)           | Mate & Tex Cache                                 | [*Optional*] COM3D2.NPRShader.Plugin<br>[*Optional*] COM3D2.MaidLoader                                                                         |
| [~~npr_930_dpi_fix~~](#npr_930_dpi_fix)     | DPI Fix on 2K for NPRShader v930                 | COM3D2.NPRShader.Plugin(v930)                                                                                                                  |

## Install

Download {script_name}.cs under `scripts/` as needed, and move it to `game_root_directory/scripts/`.
Some scripts need additional steps according to the `Note` section.
For old `BepInEx.ScriptLoader` (<=1.2.4.0), you need to replace `UnpatchSelf()` with `UnpatchAll(instance.Id)` in scripts.

## Details

### [change_tex_fix](./scripts/change_tex_fix.cs)

Some mod textures do not follow the naming rules, such as `test_hair.tex`, this script is designed to load these textures and handle infinity colors normally, and it is recommended to install COM3D2.ExtendedErrorHandling.

Previously, these textures could only be loaded through `COM3D2.StopQuitting.Plugin`(without `COM3D2.ExtendedErrorHandling`), which interrupted the entire loading process by raising an exception, and the infinity color was not handled.

### [dcm_sync_anm](./scripts/dcm_sync_anm.cs)

When clicking the play button of `COM3D2.DanceCameraMotion.Plugin`, the item anm will be synchronized when DCM Play/Pause/Seek. If `COM3D2.MovieTexture.Plugin` is installed, the texture will also be synchronized.
Also added the DCM play shortcut key "`".

### [extract_ks_scripts](./scripts/extract_ks_scripts.cs)

Extract *.ks scripts for other purposes.
Press the E key on the main interface to start extracting.

### [infinity_color_fix](./scripts/infinity_color_fix.cs)

* **Note**: Download `resources/InfinityColor_Fix` folder and move to `game_root_directory/BepinEx/config/`

Allow infinity colors to handle transparent channels.

Add Mask texture for infinity color.
Only need to create {name}.infinity_mask.tex for the texture {name}.tex that needs mask.
The grayscale [0-1] in the Mask texture determines whether the infinity color is displayed, 0 means that the original texture is not affected by the infinity color, and 1 means that the infinity color texture is displayed. The grayscale is allowed to be an intermediate value for mixing.

If `COM3D2.MaidLoader` is installed, the infinity color Mask texture will also be refreshed when the Mod Refresh button is clicked.

### [partsedit_add_bone](./scripts/partsedit_add_bone.cs)

Make Mune and Hip Bone Moveable and Scaleable for `COM3D2.PartsEdit.Plugin`.

### [inoutanim_sync](./scripts/inoutanim_sync.cs)

* **Note**: If vym_syasei_sync_with_inoutanim has been installed before, you need to delete `vym_syasei_sync_with_inoutanim.cs` in `game_root_directory/scripts/`.

Allow InoutAnim to Sync with Vym Syasei, Vym_Enhance AheEyeAnim, SceneCapture Emission Anim.
Only `COM3D2.InOutAnimation.Plugin` is a mandatory dependency. Others are optional.

### [wrap_mode_extend_sc](./scripts/wrap_mode_extend_sc.cs)

Make textures repeated for SceneCapture.
Copy from `wrap_mode_extend` (by ghorsington).

### [model_extend](./scripts/model_extend.cs)

* **Note**: If slot_change has been installed before, you need to delete `slot_change.cs` in `game_root_directory/scripts/`.

Load the extended configs (basebone/shadow).  
Automatically set BaseBoneName to `_ROOT_` when loading `*.bodybone.model` [legacy feature]

Example Config (create `{name}.exmodel.xml` for `{name}.model` in the same path):

```xml
<plugins>
    <plugin name="ModelExtend">
        <BaseBoneName>_ROOT_</BaseBoneName>
        <ReceiveShadows>True</ReceiveShadows>
        <ShadowCastingMode>On</ShadowCastingMode>
    </plugin>
</plugins>
```

Description:

1. BaseBoneName: Game loads models with different bone structures according to BaseBoneName. If you need to load a full-body model on some slots (like headset), set it to `_ROOT_`. For more information, please refer to the table below.
2. ReceiveShadows: [True|False] Whether to accept shadows
3. ShadowCastingMode: [Off|On|TwoSided|ShadowsOnly]
   * Off: cast no shadow
   * On: Cast shadow
   * TwoSided: Cast two-sided shadow
   * ShadowsOnly: Cast shadow only. The mesh itself will be hidden.
4. VertexColorFilename: Vertex Color. The following blender script can be used for exporting
5. UV2Filename: Extended UV2
6. UV3Filename: Extended UV3
7. UV4Filename: Extended UV4

<details>
<summary>Exporting Script for Blender</summary>
Select object and run:

```python
import bpy
import bmesh
import struct
import numpy as np
from pathlib import Path

save_path = Path(bpy.data.filepath).parent / "model"
save_path.mkdir(exist_ok=True)
selected_objects = bpy.context.selected_objects

for obj in selected_objects:
    if obj.type == "MESH":
        mesh = obj.data
        bm = bmesh.new()
        bm.from_mesh(mesh)
        last_vert_count = -1
        color_layer = bm.loops.layers.color.active if bm.loops.layers.color.active else None
        for i in range(len(bm.loops.layers.uv)):
            uv_lay = bm.loops.layers.uv[i]
            uvs = []
            colors = []
            vert_count = 0
            for vert in bm.verts:
                vert_uv = []
                for loop in vert.link_loops:
                    uv = loop[uv_lay].uv
                    if uv not in vert_uv:
                        vert_uv.append(uv)
                        uvs.append(uv)
                        if color_layer:
                            colors.append(loop[color_layer])
                        vert_count += 1
            if last_vert_count < 0:
                last_vert_count = vert_count
            elif last_vert_count != vert_count:
                raise Exception(f"Mesh \"{mesh.name}\" has different uv counts")
            if i == 0:
                if color_layer:
                    save_file = save_path / f"{mesh.name}.vcol"
                    color_s = np.array(colors, dtype=np.float32)
                    with save_file.open("wb") as f:
                        f.write(len(colors).to_bytes(4, byteorder='little', signed=True) + color_s.tobytes())
            else:
                save_file = save_path / f"{mesh.name}.uv_{i + 1}"
                uv_s = np.array(uvs, dtype=np.float32)
                print(uv_s.shape)
                with save_file.open("wb") as f:
                    f.write((len(uvs)).to_bytes(4, byteorder="little", signed=True) + uv_s.tobytes())
```

</details>
<details>
<summary>Default SlotName & BaseBoneName</summary>

| Slot Name     | BaseBoneName  |
| ------------- | ------------- |
| body          | \_ROOT\_      |
| head          | Bip01 Head    |
| eye           | Bip01 Head    |
| hairF         | Bip01 Head    |
| hairR         | Bip01 Head    |
| hairS         | Bip01 Head    |
| hairT         | Bip01 Head    |
| wear          | \_ROOT\_      |
| skirt         | \_ROOT\_      |
| onepiece      | \_ROOT\_      |
| mizugi        | \_ROOT\_      |
| panz          | \_ROOT\_      |
| bra           | \_ROOT\_      |
| stkg          | \_ROOT\_      |
| shoes         | \_ROOT\_      |
| headset       | Bip01 Head    |
| glove         | \_ROOT\_      |
| accHead       | Bip01 Head    |
| hairAho       | Bip01 Head    |
| accHana       | \_ROOT\_      |
| accHa         | Bip01 Head    |
| accKami_1_    | Bip01 Head    |
| accMiMiR      | Bip01 Head    |
| accKamiSubR   | Bip01 Head    |
| accNipR       | \_ROOT\_      |
| HandItemR     | _IK_handR     |
| accKubi       | Bip01 Spine1a |
| accKubiwa     | Bip01 Neck    |
| accHeso       | Bip01 Head    |
| accUde        | \_ROOT\_      |
| accAshi       | \_ROOT\_      |
| accSenaka     | \_ROOT\_      |
| accShippo     | Bip01 Spine   |
| accAnl        | \_ROOT\_      |
| accVag        | \_ROOT\_      |
| kubiwa        | \_ROOT\_      |
| megane        | Bip01 Head    |
| accXXX        | \_ROOT\_      |
| chinko        | Bip01 Pelvis  |
| chikubi       | \_ROOT\_      |
| accHat        | Bip01 Head    |
| kousoku_upper | \_ROOT\_      |
| kousoku_lower | \_ROOT\_      |
| seieki_naka   | \_ROOT\_      |
| seieki_hara   | \_ROOT\_      |
| seieki_face   | \_ROOT\_      |
| seieki_mune   | \_ROOT\_      |
| seieki_hip    | \_ROOT\_      |
| seieki_ude    | \_ROOT\_      |
| seieki_ashi   | \_ROOT\_      |
| accNipL       | \_ROOT\_      |
| accMiMiL      | Bip01 Head    |
| accKamiSubL   | Bip01 Head    |
| accKami_2_    | Bip01 Head    |
| accKami_3_    | Bip01 Head    |
| HandItemL     | _IK_handL     |
| underhair     | \_ROOT\_      |
| moza          | \_ROOT\_      |

</details>

### npr_addition

* **Note**: Choose one of the following installations depending on the plugin you are using.

  1. For `COM3D2.ShaderServant`:
     * Download all files in `resources/NPRShader/Shader` and move them to `game_root_directory/ShaderServantPacks`

  2. For `COM3D2.NPRShader.Plugin`:
     * Download [npr_addition.cs](./scripts/npr_addition.cs) to `game_root_directory/scripts/`
     * Download `resources/NPRShader` folder and move it to `game_root_directory/Sybaris/UnityInjector/Config`

Add Shader to COM3D2.
[Shader Description](./resources/NPRShader/ShaderList.md)

<details>
<summary>Transparent material debris Fix Script for Blender (Remember to back up project)</summary>
Select object and run:

```python
import bpy
import bmesh
from collections import deque

def reorder_faces(obj):
    if bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')

    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    index = 0
    indexs = [0] * len(bm.faces)
    
    edge_to_faces = {}
    for face in mesh.polygons:
        for edge in face.edge_keys:
            if edge not in edge_to_faces:
                edge_to_faces[edge] = []
            edge_to_faces[edge].append(face)

    visited_faces = set()
    visited_edges = set()
    queue = deque()

    def start_new_component(face):
        for edge in face.edge_keys:
            if edge not in visited_edges:
                queue.append(edge)
                visited_edges.add(edge)

    for face in mesh.polygons:
        if face not in visited_faces:
            start_new_component(face)
            while queue:
                edge = queue.popleft()
                if edge in edge_to_faces:
                    faces = edge_to_faces[edge]
                    for face in faces:
                        if face not in visited_faces:
                            visited_faces.add(face)
                            indexs[face.index] = index
                            index += 1
                            for edge in face.edge_keys:
                                if edge not in visited_edges:
                                    queue.append(edge)
                                    visited_edges.add(edge)
    
    for i, f in enumerate(bm.faces):
        f.index = indexs[i]
    bm.faces.sort()
    bm.to_mesh(mesh)
    bm.free()

reorder_faces(bpy.context.object)
```

</details>

### [pmat_extend](./scripts/pmat_extend.cs)

Allow setting RenderQueue [-1..5000] (-1 to use render queue from shader) ​​directly in pmat column in mate.

### [vym_enhance](./scripts/vym_enhance.cs)

`COM3D2.VibeYourMaid.Plugin` function enhance & potential error fix.

### [mipmap_extend](./scripts/mipmap_extend.cs)

All texture with `mipmap` in the name will enable Mipmap.

### [mate_tex_cache](./scripts/mate_tex_cache.cs)

Cache textures and materials for COM3D2 and NPRShader.

Configuration `BepinEx/config/MateTexCache.cfg`:

1. GolbalEnable: Global Switch
2. IgnoreSkin: Not cache Materials and Textures on body/head
3. IgnoreHair: Not cache Materials and Textures on hair*
4. LoadAlwaysCheck: Always check if resource has been modified (instead of only when marked dirty)
5. UnityTempCacheCapacity: The size of the resource cache pool to be destroyed
6. UseSharedMaterialForcibly[Advanced]: Force to use `SkinnedMeshRenderer.sharedMaterial(s)` instead of `SkinnedMeshRenderer.material(s)`
7. MateCacheType:
    * All: Cache all `*.mate`
    * NPR_Only: Only cache `*NPR*.mate`
    * None: No cache
8. TexCacheType:
    * All: Cache all `*.tex`
    * ByMate: cache based on MateCacheType and material

For Mate:

* You can optionally add Float Value `_Cache=0` or `_Cache=1` in Mate to mark whether the material needs to be cached.

Priority (from high to low):

 1. Global Switch
 2. _Cache value in Mate
 3. IgnoreSkin
 4. MateCacheType & TexCacheType

If `COM3D2.MaidLoader` is installed, the refresh function of MaidLoader will mark all caches as expired. When loading expired caches, it will be decided whether to reload based on the file hash.

### ~~npr_930_dpi_fix~~

**Deprecated**
DPI Fix on 2K for NPRShader v930.
