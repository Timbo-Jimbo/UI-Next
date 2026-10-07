// A group's image cut by a mask: a quad over the whole image multiplies what is drawn there by the mask texture's alpha,
// the texture stretched over the masked node's rect as it is drawn (moved, scaled and turned with it), and nothing
// kept outside that rect, as CSS's mask-image. The image is premultiplied, so colour and alpha are multiplied alike.
Shader "Hidden/TimboJimbo/UINext/Mask"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Mask"
            Blend Zero SrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MaskTexture);
            SAMPLER(sampler_MaskTexture);
            float4x4 _MaskMatrix;
            // The image's part of its root, in points (x, y, width, height).
            float4 _MaskRegion;
            // From the root's points to the masked node's own space: (a, b, tx) and (c, d, ty).
            float4 _MaskInverse0;
            float4 _MaskInverse1;
            // The masked node's size.
            float4 _MaskSize;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 root : TEXCOORD0;
            };

            Varyings Vert(uint vertexID : SV_VertexID)
            {
                // Two triangles over the image's region.
                static const float2 corners[6] = { float2(0, 0), float2(1, 0), float2(1, 1), float2(0, 0), float2(1, 1), float2(0, 1) };
                float2 corner = corners[vertexID % 6];
                Varyings o;
                o.root = _MaskRegion.xy + corner * _MaskRegion.zw;
                o.positionCS = mul(_MaskMatrix, float4(o.root, 0, 1));
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float2 local = float2(dot(_MaskInverse0.xy, i.root) + _MaskInverse0.z, dot(_MaskInverse1.xy, i.root) + _MaskInverse1.z);
                float2 uv = local / max(_MaskSize.xy, 1e-4);
                // Textures run up from their bottom; this space runs down.
                uv.y = 1 - uv.y;
                bool inside = all(uv >= 0) && all(uv <= 1);
                half alpha = inside ? SAMPLE_TEXTURE2D(_MaskTexture, sampler_MaskTexture, uv).a : 0;
                return half4(0, 0, 0, alpha);
            }
            ENDHLSL
        }
    }
}
