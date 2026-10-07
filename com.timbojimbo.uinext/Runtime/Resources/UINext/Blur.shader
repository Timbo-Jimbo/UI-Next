// A group's image blurred by dual Kawase passes (ARM's): down to half the size each step, then back up, each step
// taking a few bilinear samples around the pixel, so a wide blur costs a handful of cheap passes. The images are
// premultiplied, so they blur as they are.
Shader "Hidden/TimboJimbo/UINext/Blur"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        // xy: half a texel of the image read, times how far apart the samples are spread.
        float4 _UINextBlur;
        ENDHLSL

        Pass
        {
            Name "Down"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Down

            half4 Down(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 h = _UINextBlur.xy;
                half4 sum = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv) * 4.0;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - h);
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + h);
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(h.x, -h.y));
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(h.x, -h.y));
                return sum * 0.125;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Up"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Up

            half4 Up(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 h = _UINextBlur.xy;
                half4 sum = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-h.x * 2.0, 0.0));
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(h.x * 2.0, 0.0));
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0.0, -h.y * 2.0));
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0.0, h.y * 2.0));
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-h.x, h.y)) * 2.0;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(h.x, h.y)) * 2.0;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-h.x, -h.y)) * 2.0;
                sum += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(h.x, -h.y)) * 2.0;
                return sum / 12.0;
            }
            ENDHLSL
        }
    }
}
