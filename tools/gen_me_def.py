from pathlib import Path
from pydantic import TypeAdapter

from shader_def import ShaderPropCollection, FloatProp, TexProp, ColorProp, VectorProp, KeywordProp
from me_def import ShaderCollection, ShaderDefinitions, Texture, Vector, Float, Keyword


input_path = Path.cwd() / "tools" / "defs"
output_path = Path.cwd() / "resources" / "ShaderPack" / "MaterialEditorDefinitions"

if __name__ == "__main__":
    for i in input_path.glob("*.json"):
        with i.open("r", encoding="utf-8") as f:
            shader_prop_collections = TypeAdapter(list[ShaderPropCollection]).validate_json(f.read())
            me_shaders: list[ShaderDefinitions] = []
            for shader in shader_prop_collections:
                me_textures: list[Texture] = []
                me_vectors: list[Vector] = []
                me_floats: list[Float] = []
                me_keywords: list[str] = []
                for prop in shader.props:
                    match prop:
                        case FloatProp():
                            me_floats.append(
                                Float(
                                    name=prop.name,
                                    minimum=prop.min or 0,
                                    maximum=prop.max or 1,
                                    is_toggle=prop.is_toggle,
                                    is_whole=prop.is_int,
                                )
                            )
                        case TexProp():
                            me_textures.append(Texture(name=prop.name))
                        case ColorProp():
                            me_vectors.append(Vector(name=prop.name, is_color=True))
                        case VectorProp():
                            me_vectors.append(Vector(name=prop.name, is_color=False))
                        case KeywordProp():
                            me_keywords.append(prop.name)
                me_shaders.append(
                    ShaderDefinitions(
                        name=shader.name,
                        textures=me_textures,
                        vectors=me_vectors,
                        floats=me_floats,
                        keywords=me_keywords,
                    )
                )
            me_collection = ShaderCollection(shaders=me_shaders)
            with (output_path / f"{i.stem}.xml").open("wb") as f:
                f.write(me_collection.to_xml(pretty_print=True))  # type: ignore
