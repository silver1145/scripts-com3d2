from typing import Any, Literal

from pydantic import BaseModel


class BaseProp(BaseModel):
    type: Any
    name: str
    tip: str | None = None


class FloatProp(BaseProp):
    type: Literal["float"] = "float"
    min: float | None = None
    max: float | None = None
    default: float | None = None
    is_toggle: bool = False
    is_int: bool = False
    is_enum: bool = False
    enum_info: list[tuple[int, str]] | None = None

    def model_post_init(self, __context: Any) -> None:
        self.check_enum()

    def check_enum(self) -> None:
        if self.enum_info:
            values = [i for i, e in self.enum_info]
            self.min = min(values)
            self.max = max(values)


class TexProp(BaseProp):
    type: Literal["tex"] = "tex"
    tex_type: Literal["2D", "CUBE"] = "2D"


class VectorProp(BaseProp):
    type: Literal["vector"] = "vector"


class ColorProp(BaseProp):
    type: Literal["color"] = "color"
    is_hdr: bool = False


class KeywordProp(BaseProp):
    type: Literal["keyword"] = "keyword"


class ShaderPropCollection(BaseModel):
    name: str
    props: list[FloatProp | TexProp | VectorProp | ColorProp | KeywordProp]
