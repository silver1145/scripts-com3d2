# Scripts-COM3D2

Some COM3D2 functional scripts

## Description

| Script Name                    | Description                                      | Require                                                    |
| ------------------------------ | ------------------------------------------------ | ---------------------------------------------------------- |
| change_tex_fix                 | Fix TBody.ChangeTex when file does not exist     | -                                                          |
| dcm_sync_anm                   | DCM Sync With Item Anm and MovieTexture          | COM3D2.DanceCameraMotion.Plugin                            |
| extract_ks_scripts             | Extract *.ks scripts from game                   | -                                                          |
| infinity_color_fix             | Fix InfinityColor on Alpha Channel and Add Mask  | -                                                          |
| npr_930_dpi_fix                | DPI Fix on 2K for NPRShader v930                 | COM3D2.NPRShader.Plugin.dll(v930)                          |
| partsedit_add_bone             | Make Mune & Hip Bone Moveable and Scaleable      | COM3D2.PartsEdit.Plugin                                    |
| vym_syasei_sync_with_inoutanim | Vym Syasei Sync With Inoutanim                   | COM3D2.VibeYourMaid.Plugin<br>COM3D2.InOutAnimation.Plugin |
| wrap_mode_extend_sc            | Make textures repeated for SceneCapture          | COM3D2.SceneCapture.Plugin                                 |
| slot_change                    | Allow All Slots to Use Body Bone                 | -                                                          |
| npr_addition                   | Add Shader to COM3D2.NPRShader.Plugin            | COM3D2.NPRShader.Plugin                                    |
| vym_enhance                    | VYM Function Enhance                             | COM3D2.VibeYourMaid.Plugin                                 |
| mipmap_extend                  | Enable Mipmap for textures with `mipmap` in name | -                                                          |
| mate_tex_cache                 | Mate & Tex Cache                                 | COM3D2.NPRShader.Plugin.dll(v930)                          |

## Install

Download {script_name}.cs under `scripts/` as needed, and move it to the `game_root_directory/scripts/`
Some scripts need to do additional steps according to the `Note` section.

### change_tex_fix

Some mod textures do not follow the naming rules, such as `test_hair.tex`, this script is designed to load these textures and handle infinity colors normally, and it is recommended to install COM3D2.ExtendedErrorHandling.

Previously, these textures could only be loaded through `COM3D2.StopQuitting.Plugin`(without `COM3D2.ExtendedErrorHandling`), which interrupted the entire loading process by raising an exception, and the infinity color was not handled.

### dcm_sync_anm

When clicking the play button of `COM3D2.DanceCameraMotion.Plugin`, the item anm will be synchronized to frame 0 (also support SceneCapture). If `COM3D2.MovieTexture.Plugin` is installed, the texture will also be synchronized.
Also added the DCM play shortcut key "`".

### extract_ks_scripts

Extract *.ks scripts for other purposes.
Press the E key on the main interface to start extracting.

### infinity_color_fix

* Note: Download `resources/InfinityColor_Fix` folder and move to `game_root_directory/BepinEx/config/`

Allow infinity colors to handle transparent channels.

Add Mask texture for infinity color.
Only need to create {name}.infinity_mask.tex for the texture {name}.tex that needs mask.
The grayscale [0-1] in the Mask texture determines whether the infinity color is displayed, 0 means that the original texture is not affected by the infinity color, and 1 means that the infinity color texture is displayed. The grayscale is allowed to be an intermediate value for mixing.

If `COM3D2.MaidLoader` is installed, the infinity color Mask texture will also be refreshed when the Mod Refresh button is clicked.

### npr_930_dpi_fix

DPI Fix on 2K for NPRShader v930.

### partsedit_add_bone

Make Mune and Hip Bone Moveable and Scaleable for `COM3D2.PartsEdit.Plugin`.

### vym_syasei_sync_with_inoutanim

Allow Vym Syasei to sync With Inoutanim.

### wrap_mode_extend_sc

Make textures repeated for SceneCapture.
Copy from `wrap_mode_extend` (by ghorsington).

### slot_change

Allow All Slots to Use Body Bone.
Just rename `{origin}.model` to `{origin}.bodybone.model`.

### npr_addition

* Note: Download `resources/NPRShader` folder and move to `game_root_directory/Sybaris/UnityInjector/Config`

Add Shader to `COM3D2.NPRShader.Plugin`.
[Shader Description](./resources/NPRShader/ShaderList.md)

### vym_enhance

`COM3D2.VibeYourMaid.Plugin` function enhance & potential error fix.

### mipmap_extend

All texture with `mipmap` in the name will enable Mipmap.

### mate_tex_cache

Cache textures and materials for COM3D2 and NPRShader.

Configuration `BepinEx/config/MateTexCache.cfg`:

1. GolbalEnable: Global Switch
2. IgnoreSkin: Not cache Materials and Textures on body/head
3. TempCacheCapacity: The size of the resource cache pool to be destroyed
4. MateCacheType:
    * All: Cache all `*.mate`
    * NPR_Only: Only cache `*NPR*.mate`
    * None: No cache
5. TexCacheType:
    * All: Cache all `*.tex`
    * ByMate: cache based on MateCacheType and material

For Mate:

* You can optionally add Float Value `_Cache=0` or `_Cache=1` in Mate to mark whether the material needs to be cached.

Priority (from high to low):

 1. Global Switch
 2. IgnoreSkin
 3. _Cache value in Mate
 4. MateCacheType & TexCacheType

If `COM3D2.MaidLoader` is installed, the refresh function of MaidLoader will mark all caches as expired. When loading expired caches, it will be decided whether to reload based on the file hash.
