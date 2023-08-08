# Scripts-COM3D2

Some COM3D2 functional scripts

## Description

| Script Name                    | Description                                     | Require                                                    |
| ------------------------------ | ----------------------------------------------- | ---------------------------------------------------------- |
| change_tex_fix                 | Fix TBody.ChangeTex when file does not exist    | -                                                          |
| extract_ks_scripts             | Extract *.ks scripts from game                  | -                                                          |
| infinity_color_fix             | Fix InfinityColor on Alpha Channel and Add Mask | -                                                          |
| npr_930_dpi_fix                | DPI Fix on 2K for NPRShader v930                | COM3D2.NPRShader.Plugin.dll(v930)                          |
| partsedit_add_bone             | Make Mune & Hip Bone Moveable and Scaleable     | COM3D2.PartsEdit.Plugin                                    |
| vym_syasei_sync_with_inoutanim | Vym Syasei Sync With Inoutanim                  | COM3D2.VibeYourMaid.Plugin<br>COM3D2.InOutAnimation.Plugin |
| wrap_mode_extend_sc            | Make textures repeated for SceneCapture         | COM3D2.SceneCapture.Plugin                                 |

## Install

Download {script_name}.cs under `scripts/` as needed, and move it to the `game_root_directory/scripts/`
Some scripts need to do additional steps according to the `Note` section.

### 1. change_tex_fix

Some mod textures do not follow the naming rules, such as `test_hair.tex`, this script is designed to load these textures and handle infinite colors normally, and it is recommended to install COM3D2.ExtendedErrorHandling.

Previously, these textures could only be loaded through `COM3D2.StopQuitting.Plugin`(without `COM3D2.ExtendedErrorHandling`), which interrupted the entire loading process by raising an exception, and the infinite color was not handled.

### 2. extract_ks_scripts

Extract *.ks scripts for other purposes.
Press the E key on the main interface to start extracting.

### 3. infinity_color_fix

* Note: Download `resources/InfinityColor_Fix` folder and move to `game_root_directory/BepinEx/config/`

Allow infinite colors to handle transparent channels.

Add Mask texture for infinite color.
Only need to create {name}.infinity_mask.tex for the texture {name}.tex that needs mask.
The grayscale [0-1] in the Mask texture determines whether the infinite color is displayed, 0 means that the original texture is not affected by the infinite color, and 1 means that the infinite color texture is displayed. The grayscale is allowed to be an intermediate value for mixing.

If you use transparency and infinite color mask at the same position of the texture, adjusting the infinite color value multiple times will cause the color of the transparent area to be mixed, switch to other items and then switch back to reload the infinite color texture.

### 4. npr_930_dpi_fix

DPI Fix on 2K for NPRShader v930.

### 5. partsedit_add_bone

Make Mune and Hip Bone Moveable and Scaleable for `COM3D2.PartsEdit.Plugin`.

### 6. vym_syasei_sync_with_inoutanim

Allow Vym Syasei to sync With Inoutanim.

### 7. wrap_mode_extend_sc

Make textures repeated for SceneCapture.
Copy from `wrap_mode_extend` (by ghorsington)
