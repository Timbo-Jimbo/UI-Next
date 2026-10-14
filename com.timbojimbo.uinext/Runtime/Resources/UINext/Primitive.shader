// Every primitive the UI draws, from one structured buffer, a pass a kind: the compositor draws each run of one kind
// with that kind's pass, plain or clipped (see PrimitiveKinds.hlsl, which holds the shaders), after the occluders have
// laid down the depth of every large opaque shape's inside. The passes lie in the order PrimitiveKind has them, a
// plain and a clipped pass each; then the occluders, then the overdraw view (UIDebug.Overdraw), which draws every
// primitive as heat.
Shader "Hidden/TimboJimbo/UINext/Primitive"
{
    Properties
    {
        _Texture0 ("Texture 0", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass { Name "Shape" Blend One OneMinusSrcAlpha ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #define KIND_SHAPE 1
            #include "PrimitiveKinds.hlsl"
            ENDHLSL }
        Pass { Name "ShapeClipped" Blend One OneMinusSrcAlpha ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #define KIND_SHAPE 1
            #define CLIPPED 1
            #include "PrimitiveKinds.hlsl"
            ENDHLSL }

        Pass { Name "Shadow" Blend One OneMinusSrcAlpha ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #define KIND_SHADOW 1
            #include "PrimitiveKinds.hlsl"
            ENDHLSL }
        Pass { Name "ShadowClipped" Blend One OneMinusSrcAlpha ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #define KIND_SHADOW 1
            #define CLIPPED 1
            #include "PrimitiveKinds.hlsl"
            ENDHLSL }

        Pass { Name "Image" Blend One OneMinusSrcAlpha ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #define KIND_IMAGE 1
            #include "PrimitiveKinds.hlsl"
            ENDHLSL }
        Pass { Name "ImageClipped" Blend One OneMinusSrcAlpha ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #define KIND_IMAGE 1
            #define CLIPPED 1
            #include "PrimitiveKinds.hlsl"
            ENDHLSL }

        Pass { Name "Sliced" Blend One OneMinusSrcAlpha ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #define KIND_SLICED 1
            #include "PrimitiveKinds.hlsl"
            ENDHLSL }
        Pass { Name "SlicedClipped" Blend One OneMinusSrcAlpha ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #define KIND_SLICED 1
            #define CLIPPED 1
            #include "PrimitiveKinds.hlsl"
            ENDHLSL }

        Pass { Name "Glyph" Blend One OneMinusSrcAlpha ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #define KIND_GLYPH 1
            #include "PrimitiveKinds.hlsl"
            ENDHLSL }
        Pass { Name "GlyphClipped" Blend One OneMinusSrcAlpha ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #define KIND_GLYPH 1
            #define CLIPPED 1
            #include "PrimitiveKinds.hlsl"
            ENDHLSL }

        Pass { Name "Bitmap" Blend One OneMinusSrcAlpha ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #define KIND_BITMAP 1
            #include "PrimitiveKinds.hlsl"
            ENDHLSL }
        Pass { Name "BitmapClipped" Blend One OneMinusSrcAlpha ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #define KIND_BITMAP 1
            #define CLIPPED 1
            #include "PrimitiveKinds.hlsl"
            ENDHLSL }

        Pass { Name "Group" Blend One OneMinusSrcAlpha ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #define KIND_GROUP 1
            #include "PrimitiveKinds.hlsl"
            ENDHLSL }
        Pass { Name "GroupClipped" Blend One OneMinusSrcAlpha ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #define KIND_GROUP 1
            #define CLIPPED 1
            #include "PrimitiveKinds.hlsl"
            ENDHLSL }

        Pass { Name "Pair" Blend One OneMinusSrcAlpha ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #define KIND_PAIR 1
            #include "PrimitiveKinds.hlsl"
            ENDHLSL }
        Pass { Name "PairClipped" Blend One OneMinusSrcAlpha ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #define KIND_PAIR 1
            #define CLIPPED 1
            #include "PrimitiveKinds.hlsl"
            ENDHLSL }

        // In the game view, before everything: the depth of each primitive where it is opaque all over, so what lies
        // under it is never shaded.
        Pass { Name "Occluders" ZWrite On ZTest LEqual ColorMask 0 Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #define KIND_OCCLUDER 1
            #include "PrimitiveKinds.hlsl"
            ENDHLSL }

        // The overdraw view: every primitive drawn as a little heat, whatever it is.
        Pass { Name "Overdraw" Blend One OneMinusSrcAlpha ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #define KIND_OVERDRAW 1
            #include "PrimitiveKinds.hlsl"
            ENDHLSL }
        // A shape's bands (its corners drawn by the shape passes): the box's edge alone.
        Pass { Name "ShapeFlat" Blend One OneMinusSrcAlpha ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #define KIND_SHAPE 1
            #define FLAT_ONLY 1
            #include "PrimitiveKinds.hlsl"
            ENDHLSL }
        Pass { Name "ShapeFlatClipped" Blend One OneMinusSrcAlpha ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #define KIND_SHAPE 1
            #define FLAT_ONLY 1
            #define CLIPPED 1
            #include "PrimitiveKinds.hlsl"
            ENDHLSL }
        // An image's bands (its corners drawn by the image passes): the box's edge alone.
        Pass { Name "ImageFlat" Blend One OneMinusSrcAlpha ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #define KIND_IMAGE 1
            #define FLAT_ONLY 1
            #include "PrimitiveKinds.hlsl"
            ENDHLSL }
        Pass { Name "ImageFlatClipped" Blend One OneMinusSrcAlpha ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #define KIND_IMAGE 1
            #define FLAT_ONLY 1
            #define CLIPPED 1
            #include "PrimitiveKinds.hlsl"
            ENDHLSL }


    }
}
