// Every primitive the UI draws, from one structured buffer: the vertex shader expands each record into a quad in its
// node's space and places it in its root's, and the fragment shader draws it as a signed distance field (shapes,
// shadows, images cut to rounded corners, SDF glyphs) or a texture (bitmap glyphs, group images, a matched pair's two
// images summed), cut by up to four rounded-rect clips, with premultiplied alpha. Colours are sRGB values and blend as
// they are (the project blends in gamma).
Shader "Hidden/TimboJimbo/UINext/Primitive"
{
    Properties
    {
        _Texture0 ("Texture 0", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Primitive"
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            struct Instance
            {
                float4 rect;
                float4 radii;
                float4 color;
                float4 color2;
                float4 params;
                float4 uv;
                uint slot;
                uint type;
                uint flags;
                uint textureIndex;
            };

            struct Slot
            {
                float4 row0;
                float4 row1;
                float4 sizeOpacity;
                float4 clips;
            };

            struct Clip
            {
                float4 inverse0;
                float4 inverse1;
                float4 size;
                float4 radii;
            };

            StructuredBuffer<Instance> _Instances;
            StructuredBuffer<Slot> _Slots;
            StructuredBuffer<Clip> _Clips;
            int _InstanceOffset;
            float4x4 _UIMatrix;
            // A batch draws with up to eight textures (atlases of different fonts, emoji, images); each primitive says
            // which it samples.
            Texture2D _Texture0, _Texture1, _Texture2, _Texture3, _Texture4, _Texture5, _Texture6, _Texture7;
            SamplerState sampler_linear_clamp;

            float4 SampleTexture(uint index, float2 uv, float2 dx, float2 dy)
            {
                switch (index)
                {
                    case 1: return _Texture1.SampleGrad(sampler_linear_clamp, uv, dx, dy);
                    case 2: return _Texture2.SampleGrad(sampler_linear_clamp, uv, dx, dy);
                    case 3: return _Texture3.SampleGrad(sampler_linear_clamp, uv, dx, dy);
                    case 4: return _Texture4.SampleGrad(sampler_linear_clamp, uv, dx, dy);
                    case 5: return _Texture5.SampleGrad(sampler_linear_clamp, uv, dx, dy);
                    case 6: return _Texture6.SampleGrad(sampler_linear_clamp, uv, dx, dy);
                    case 7: return _Texture7.SampleGrad(sampler_linear_clamp, uv, dx, dy);
                    default: return _Texture0.SampleGrad(sampler_linear_clamp, uv, dx, dy);
                }
            }

            static const float2 kCorners[6] =
            {
                float2(0, 0), float2(1, 0), float2(1, 1),
                float2(0, 0), float2(1, 1), float2(0, 1)
            };

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 local : TEXCOORD0;
                float2 root : TEXCOORD1;
                float2 uv : TEXCOORD2;
                nointerpolation float2 halfSize : TEXCOORD3;
                nointerpolation uint id : TEXCOORD4;
            };

            // Signed distance to a rounded rect centred on the origin (y down), each corner its own radius: top-left,
            // top-right, bottom-right, bottom-left.
            float SdRoundRect(float2 p, float2 halfSize, float4 radii)
            {
                float r = p.x < 0 ? (p.y < 0 ? radii.x : radii.w) : (p.y < 0 ? radii.y : radii.z);
                r = min(r, min(halfSize.x, halfSize.y));
                float2 q = abs(p) - halfSize + r;
                return min(max(q.x, q.y), 0) + length(max(q, 0)) - r;
            }

            Varyings Vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
            {
                Varyings o;
                uint id = (uint)_InstanceOffset + instanceID;
                Instance inst = _Instances[id];
                Slot slot = _Slots[inst.slot];

                float4 rect = inst.rect;
                if ((inst.flags & 1u) != 0u)
                    rect = float4(rect.x, rect.y, slot.sizeOpacity.x - rect.x - rect.z, slot.sizeOpacity.y - rect.y - rect.w);
                rect.zw = max(rect.zw, 0);

                // Room around it for anti-aliasing (a pixel and a half, in its own units) and for a shadow's blur; none
                // for a glyph, whose quad has its own and whose neighbours in the atlas lie just past it, or for a group's
                // image, which covers its region exactly.
                bool glyph = inst.type == 3u || inst.type == 4u;
                float margin = glyph || inst.type == 5u ? 0 : 1.5 * max(slot.sizeOpacity.w, 0.01) + (inst.type == 1u ? inst.params.y * 2.5 : 0);
                float2 corner = kCorners[vertexID % 6];
                float2 p = rect.xy - margin + corner * (rect.zw + 2 * margin);
                float2 t = (p - rect.xy) / max(rect.zw, 1e-5);
                // Italic: a glyph's top is moved right by its skew.
                if (glyph)
                    p.x += inst.params.z * (1 - corner.y);
                float2 root = float2(dot(slot.row0.xy, p) + slot.row0.z, dot(slot.row1.xy, p) + slot.row1.z);

                o.position = mul(_UIMatrix, float4(root, 0, 1));
                o.local = p - (rect.xy + rect.zw * 0.5);
                o.root = root;
                // Textures run up from their bottom; this space runs down.
                o.uv = float2(inst.uv.x + t.x * inst.uv.z, inst.uv.y + (1 - t.y) * inst.uv.w);
                o.halfSize = rect.zw * 0.5;
                o.id = id;
                return o;
            }

            float4 Frag(Varyings i) : SV_Target
            {
                Instance inst = _Instances[i.id];
                Slot slot = _Slots[inst.slot];

                // Derivatives first, outside any branch.
                float2 localPixel = fwidth(i.local);
                float aa = max(max(localPixel.x, localPixel.y), 1e-5);
                float2 rootPixel = fwidth(i.root);
                float rootAA = max(max(rootPixel.x, rootPixel.y), 1e-5);
                // Kept inside its own part of the texture: an image's crop or a glyph's cell in its atlas.
                float2 uvMin = min(inst.uv.xy, inst.uv.xy + inst.uv.zw);
                float2 uvMax = max(inst.uv.xy, inst.uv.xy + inst.uv.zw);
                // Gradients taken before choosing the texture, so the choice is free to branch.
                float2 uvDx = ddx(i.uv), uvDy = ddy(i.uv);
                float2 uv = clamp(i.uv, uvMin, uvMax);
                float4 tex = SampleTexture(inst.textureIndex & 15u, uv, uvDx, uvDy);
                float fieldPixel = max(fwidth(tex.a), 1e-4);

                // A group's image: premultiplied already, drawn at the group's opacity, covering the root.
                if (inst.type == 5u)
                    return tex * (inst.color.a * slot.sizeOpacity.z);

                float4 color = inst.color;
                float coverage = 1;
                bool premultiplied = false;
                if (inst.type == 6u)
                {
                    // A matched pair: the picture of where it came from and the node it goes to, each an image of its own
                    // (premultiplied), cross-faded by summing them, as the web's view transitions blend an image pair
                    // (plus-lighter), so where both are opaque it stays opaque all the way; cut to the pair's rounded shape.
                    float4 live = SampleTexture((inst.textureIndex >> 4) & 15u, uv, uvDx, uvDy);
                    color = min(tex * (1.0 - inst.color.a) + live * inst.color.a, 1.0);
                    float d = SdRoundRect(i.local, i.halfSize, inst.radii);
                    coverage = saturate(0.5 - d / aa);
                    premultiplied = true;
                }
                else if (inst.type == 0u)
                {
                    float d = SdRoundRect(i.local, i.halfSize, inst.radii);
                    coverage = saturate(0.5 - d / aa);
                    if ((inst.flags & 2u) != 0u)
                    {
                        float angle = radians(inst.params.z);
                        float2 direction = float2(cos(angle), sin(angle));
                        float extent = abs(direction.x) * i.halfSize.x + abs(direction.y) * i.halfSize.y;
                        float t = saturate(dot(i.local, direction) / max(2 * extent, 1e-5) + 0.5);
                        color = lerp(inst.color, inst.color2, t);
                    }
                    else if (inst.params.x > 0)
                    {
                        float inner = saturate(0.5 - (d + inst.params.x) / aa);
                        color = lerp(inst.color2, inst.color, inner);
                    }
                }
                else if (inst.type == 1u)
                {
                    float d = SdRoundRect(i.local, i.halfSize, inst.radii);
                    float blur = max(inst.params.y, aa);
                    coverage = 1 - smoothstep(-blur, blur, d);
                }
                else if (inst.type == 2u)
                {
                    float d = SdRoundRect(i.local, i.halfSize, inst.radii);
                    coverage = saturate(0.5 - d / aa);
                    color = tex * inst.color;
                }
                else if (inst.type == 3u)
                {
                    // The distance field holds 0.5 at the glyph's edge (less where synthetic bold widens it), resolved
                    // over about a pixel by how fast the field changes on screen, so edges stay crisp at any size.
                    coverage = saturate((tex.a - (0.5 - inst.params.y)) / fieldPixel + 0.5);
                }
                else
                {
                    // A colour bitmap glyph (emoji): its own colours, the tint's alpha.
                    color = tex * inst.color;
                }

                [unroll]
                for (int k = 0; k < 4; k++)
                {
                    float index = slot.clips[k];
                    if (index < 0) break;
                    Clip c = _Clips[(uint)index];
                    float2 cp = float2(dot(c.inverse0.xy, i.root) + c.inverse0.z, dot(c.inverse1.xy, i.root) + c.inverse1.z);
                    float2 halfSize = c.size.xy * 0.5;
                    float d = SdRoundRect(cp - halfSize, halfSize, c.radii);
                    float scale = sqrt(abs(c.inverse0.x * c.inverse1.y - c.inverse0.y * c.inverse1.x));
                    coverage *= saturate(0.5 - d / max(rootAA * scale, 1e-5));
                }

                // A pair is drawn as its images were, their filters applied already.
                if (premultiplied)
                    return color * (coverage * slot.sizeOpacity.z);

                float alpha = saturate(color.a) * coverage * slot.sizeOpacity.z;
                // Its colour filters, on the premultiplied colour: towards grey by its saturation, then by its brightness.
                float3 rgb = color.rgb * alpha;
                float luma = dot(rgb, float3(0.2126, 0.7152, 0.0722));
                rgb = lerp(luma.xxx, rgb, slot.row0.w) * slot.row1.w;
                return float4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
