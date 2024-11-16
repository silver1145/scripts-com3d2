from pydantic_xml import BaseXmlModel, element, wrapped


class ShaderCollection(BaseXmlModel):
    shaders: "list[ShaderDefinitions]" = wrapped(path="Shaders", entity=element("Shader"))


class ShaderDefinitions(BaseXmlModel):
    name: str = element(tag="Name")
    textures: "list[Texture]" = wrapped(path="Textures", entity=element("Texture"))
    vectors: "list[Vector]" = wrapped(path="Vectors", entity=element("Vector"))
    floats: "list[Float]" = wrapped(path="Floats", entity=element("Float"))
    keywords: "list[str]" = wrapped(path="Keywords", entity=element("Keyword"))


class Texture(BaseXmlModel):
    name: str = element(tag="Name")


class Vector(BaseXmlModel):
    name: str = element(tag="Name")
    is_color: bool = element(tag="Color", default=True)


class Float(BaseXmlModel):
    name: str = element(tag="Name")
    minimum: float = element(tag="Minimum")
    maximum: float = element(tag="Maximum")
    is_toggle: bool = element(tag="Toggle")
    is_whole: bool = element(tag="WholeNumber")


class Keyword(BaseXmlModel):
    name: str = element(tag="Name")
