// Every primitive the UI draws, from one structured buffer: the vertex shader expands each record into a quad in its
// node's space and places it in its root's, and the fragment shader draws it as a signed distance field (shapes,
// shadows, images cut to rounded corners, SDF glyphs) or a texture (bitmap glyphs, group images, a matched pair's two
// images summed), cut by up to four rounded-rect clips, with premultiplied alpha. Colours are sRGB values and blend as
// they are (the project blends in gamma).
//
// What a pixel costs is what matters on a phone, where the large shapes (backgrounds, cards, their shadows) cover the
// screen many times over: what a primitive is drawn with comes to the fragment shader from the vertex shader, read
// once a vertex, rather than from the buffers once a pixel; a pixel samples a texture only for a primitive that has
// one, takes each clip's scale as worked out once on the CPU, and is filtered only when its saturation or brightness
// is not as it is. Nor is a pixel shaded only to be clipped away: the vertex shader cuts a quad down to its clips where
// it can (what a scroll view has scrolled out from under its edges is never shaded), and a pixel tests only the clips
// its quad crosses. Nor, in the game view, only to be covered: each primitive is drawn at the depth of its place in the
// order, after the occluders have laid down the depth of every opaque shape's inside, so what a card covers (its
// shadow, the page under it) fails the depth test before it is shaded.
Shader "Hidden/TimboJimbo/UINext/Primitive"
{
    Properties
    {
        _Texture0 ("Texture 0", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE

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

        // A clip: its transform from its root's space into its own, its size (and in z that transform's scale), and
        // its corner radii.
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
        // Above 0, the overdraw view: every fragment shaded adds heat rather than drawing (UIDebug.Overdraw).
        float _UINextOverdraw;
        // Above 0, the game view's main pass, which has a depth buffer: each of its _InstanceTotal primitives is drawn
        // at the depth of its place in the order (OrderDepth), so the occluders drawn first hide what lies under them.
        float _UINextOcclusion;
        int _InstanceTotal;
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
            // Where the pixel is in its primitive's space, from its centre (xy), and in its root's (zw).
            float4 local : TEXCOORD0;
            float2 uv : TEXCOORD1;
            // What it is drawn with, the same over the whole primitive: its colours, corner radii and parameters as its
            // record has them (at half precision, all they need), its texture rect, its half size, its opacity and
            // saturation, and its type, flags and texture with its brightness.
            nointerpolation half4 color : TEXCOORD2;
            nointerpolation half4 color2 : TEXCOORD3;
            nointerpolation half4 radii : TEXCOORD4;
            nointerpolation half4 params : TEXCOORD5;
            nointerpolation float4 uvRect : TEXCOORD6;
            nointerpolation float4 clips : TEXCOORD7;
            nointerpolation float4 shape : TEXCOORD8;
            nointerpolation uint2 kind : TEXCOORD9;
        };

        // How far along a gradient at `degrees` (0 left to right, 90 top to bottom) a point in a rect is, 0 to 1.
        float GradientT(float2 local, float2 halfSize, float degrees)
        {
            float angle = radians(degrees);
            float2 direction = float2(cos(angle), sin(angle));
            float extent = abs(direction.x) * halfSize.x + abs(direction.y) * halfSize.y;
            return saturate(dot(local, direction) / max(2 * extent, 1e-5) + 0.5);
        }

        // How far inside a rounded rect a point is (negative outside), from its distances to the rect's edges (left, top,
        // right, bottom) and the corners' radii (top-left, top-right, bottom-right, bottom-left, none more than half its
        // shorter side): within a corner's radius of both its edges, from the corner's circle; elsewhere from its nearest
        // edge.
        float InsideRoundRect(float4 edges, float4 radii)
        {
            float2 near = float2(min(edges.x, edges.z), min(edges.y, edges.w));
            float r = edges.x < edges.z ? (edges.y < edges.w ? radii.x : radii.w) : (edges.y < edges.w ? radii.y : radii.z);
            float2 q = r - near;
            return q.x > 0 && q.y > 0 ? r - length(q) : min(near.x, near.y);
        }

        // Signed distance to a rounded rect centred on the origin (y down), each corner its own radius: top-left,
        // top-right, bottom-right, bottom-left.
        float SdRoundRect(float2 p, float2 halfSize, float4 radii)
        {
            float r = p.x < 0 ? (p.y < 0 ? radii.x : radii.w) : (p.y < 0 ? radii.y : radii.z);
            r = min(r, min(halfSize.x, halfSize.y));
            float2 q = abs(p) - halfSize + r;
            return min(max(q.x, q.y), 0) + length(max(q, 0)) - r;
        }

        // One axis of a nine-slice: where a point `p` along a rect `size` long samples, as a share of its image from that
        // axis's start, with borders `head` and `tail` long (points) that are `headShare` and `tailShare` of the image;
        // `rate` is how fast that share moves per point. Borders longer together than the rect shrink alike to fit it.
        float SliceAxis(float p, float size, float head, float tail, float headShare, float tailShare, out float rate)
        {
            float fit = min(1, size / max(head + tail, 1e-5));
            head *= fit;
            tail *= fit;
            if (p < head)
            {
                rate = headShare / max(head, 1e-5);
                return p * rate;
            }
            if (p > size - tail)
            {
                rate = tailShare / max(tail, 1e-5);
                return 1 - (size - p) * rate;
            }
            rate = (1 - headShare - tailShare) / max(size - head - tail, 1e-5);
            return headShare + (p - head) * rate;
        }

        // Where a point `p` in a nine-sliced image's rect (from its top-left corner, y down) samples its `uvRect`, and how
        // fast that moves per point each way: `border` holds its borders in points and `share` the same as shares of its
        // image, each left, top, right, bottom.
        float2 SliceUv(float2 p, float2 size, float4 border, float4 share, float4 uvRect, out float2 rate)
        {
            float rateX, rateY;
            float x = SliceAxis(p.x, size.x, border.x, border.z, share.x, share.z, rateX);
            float y = SliceAxis(p.y, size.y, border.y, border.w, share.y, share.w, rateY);
            // Textures run up from their bottom; this space runs down.
            rate = float2(rateX * uvRect.z, -rateY * uvRect.w);
            return float2(uvRect.x + x * uvRect.z, uvRect.y + (1 - y) * uvRect.w);
        }

        // Whether a 2x3 transform's rows (xy the matrix, z the offset) keep a rect upright: no turn, no shear, no axis
        // squashed flat.
        bool Upright(float4 row0, float4 row1)
        {
            return abs(row0.y) < 1e-6 && abs(row1.x) < 1e-6 && abs(row0.x) > 1e-6 && abs(row1.y) > 1e-6;
        }

        // What of a primitive's quad its clips leave, and which clips still cut it pixel by pixel. The quad runs from
        // `quadMin` to `quadMax` in its node's space, its top moved right by `skew` (an italic glyph's). A clip it lies
        // wholly inside (a pixel in from its edges and clear of its rounded corners) leaves all of it, so it is not
        // tested a pixel; one it lies wholly outside leaves none, so it is not drawn. One it crosses is kept, and where
        // the node and the clip are both upright the quad is cut down to the clip's rect, `spare` pixels out from it: a
        // primitive keeps one past it for its smoothed edge, and what lies further is never shaded, where it would
        // otherwise be shaded only to be cut away (an occluder keeps only what lies a pixel inside it, -1). Whether each
        // clip it crosses cut it exactly so (both upright, the clip square-cornered).
        bool CutToClips(Slot slot, float skew, float spare, inout float2 quadMin, inout float2 quadMax, inout float4 clips)
        {
            if (clips.x < 0)
                return true;
            float2x2 toRoot = float2x2(slot.row0.xy, slot.row1.xy);
            float2 offset = float2(slot.row0.z, slot.row1.z);
            float2 r0 = mul(toRoot, float2(quadMin.x + skew, quadMin.y)) + offset;
            float2 r1 = mul(toRoot, float2(quadMax.x + skew, quadMin.y)) + offset;
            float2 r2 = mul(toRoot, quadMax) + offset;
            float2 r3 = mul(toRoot, float2(quadMin.x, quadMax.y)) + offset;
            // A pixel in the root's points: the node's own (sizeOpacity.w) by its transform's scale.
            float pixel = slot.sizeOpacity.w * sqrt(abs(determinant(toRoot)));
            bool upright = skew == 0 && Upright(slot.row0, slot.row1);
            float2 rootMin = min(min(r0, r1), min(r2, r3));
            float2 rootMax = max(max(r0, r1), max(r2, r3));
            float4 kept = -1;
            uint count = 0;
            bool culled = false;
            bool exact = true;
            [unroll]
            for (int k = 0; k < 4; k++)
            {
                float index = clips[k];
                if (index < 0) break;
                Clip c = _Clips[(uint)index];
                // The quad's corners in the clip's space, and a pixel there.
                float2x2 toClip = float2x2(c.inverse0.xy, c.inverse1.xy);
                float2 shift = float2(c.inverse0.z, c.inverse1.z);
                float2 k0 = mul(toClip, r0) + shift, k1 = mul(toClip, r1) + shift;
                float2 k2 = mul(toClip, r2) + shift, k3 = mul(toClip, r3) + shift;
                float2 clipMin = min(min(k0, k1), min(k2, k3));
                float2 clipMax = max(max(k0, k1), max(k2, k3));
                float clipPixel = pixel * c.size.z;
                culled = culled || any(clipMax < -clipPixel) || any(clipMin > c.size.xy + clipPixel);
                // Clear of the corners: inside its rect brought in by its largest radius across, or down.
                float radius = max(max(c.radii.x, c.radii.y), max(c.radii.z, c.radii.w));
                float2 inMin = clipMin - clipPixel, inMax = c.size.xy - clipPixel - clipMax;
                bool inside = (all(inMin >= float2(radius, 0)) && all(inMax >= float2(radius, 0)))
                    || (all(inMin >= float2(0, radius)) && all(inMax >= float2(0, radius)));
                if (inside) continue;
                kept = count == 0 ? float4(index, kept.yzw) : count == 1 ? float4(kept.x, index, kept.zw)
                    : count == 2 ? float4(kept.xy, index, kept.w) : float4(kept.xyz, index);
                count++;
                bool cut = upright && Upright(c.inverse0, c.inverse1);
                exact = exact && cut && radius <= 0;
                if (cut)
                {
                    float2 scale = float2(c.inverse0.x, c.inverse1.y);
                    float2 edge0 = (-spare * clipPixel - shift) / scale, edge1 = (c.size.xy + spare * clipPixel - shift) / scale;
                    rootMin = max(rootMin, min(edge0, edge1));
                    rootMax = min(rootMax, max(edge0, edge1));
                }
            }
            clips = kept;
            if (upright)
            {
                // Back into the node's space.
                float2 scale = float2(slot.row0.x, slot.row1.y);
                float2 l0 = (rootMin - offset) / scale, l1 = (rootMax - offset) / scale;
                quadMin = max(quadMin, min(l0, l1));
                quadMax = min(quadMax, max(l0, l1));
                culled = culled || any(quadMax <= quadMin);
            }
            if (culled)
                quadMax = quadMin;
            return exact;
        }

        // A primitive's rect in its node's space: as its record has it, or insets from its node's edges (RelativeToNode).
        float4 RectOf(Instance inst, Slot slot)
        {
            float4 rect = inst.rect;
            if ((inst.flags & 1u) != 0u)
                rect = float4(rect.x, rect.y, slot.sizeOpacity.x - rect.x - rect.z, slot.sizeOpacity.y - rect.y - rect.w);
            rect.zw = max(rect.zw, 0);
            return rect;
        }

        // The depth in its root's space (the matrix makes it the platform's) of the id-th primitive in the game view's
        // main pass: from the far end, for the first, to the near, for the last. Elsewhere all lie at 0.
        float OrderDepth(uint id)
        {
            return _UINextOcclusion > 0.5 ? 2 * (id + 1.0) / (_InstanceTotal + 2.0) - 1 : 0;
        }

        Varyings Vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
        {
            Varyings o;
            uint id = (uint)_InstanceOffset + instanceID;
            Instance inst = _Instances[id];
            Slot slot = _Slots[inst.slot];
            float4 rect = RectOf(inst, slot);

            // Room around it for anti-aliasing (a pixel and a half, in its own units) and for a shadow's blur; none
            // for a glyph, whose quad has its own and whose neighbours in the atlas lie just past it, or for a group's
            // image, which covers its region exactly.
            bool glyph = inst.type == 3u || inst.type == 4u;
            float margin = glyph || inst.type == 5u || inst.type == 6u ? 0 : 1.5 * max(slot.sizeOpacity.w, 0.01) + (inst.type == 1u ? inst.params.y * 2.5 : 0);
            float2 quadMin = rect.xy - margin, quadMax = rect.xy + rect.zw + margin;
            // Italic: a glyph's top is moved right by its skew.
            float skew = glyph ? inst.params.z : 0;
            float4 clips = slot.clips;
            CutToClips(slot, skew, 1, quadMin, quadMax, clips);
            float2 corner = kCorners[vertexID % 6];
            float2 p = quadMin + corner * (quadMax - quadMin);
            float2 t = (p - rect.xy) / max(rect.zw, 1e-5);
            p.x += skew * (1 - corner.y);
            float2 root = float2(dot(slot.row0.xy, p) + slot.row0.z, dot(slot.row1.xy, p) + slot.row1.z);

            o.position = mul(_UIMatrix, float4(root, OrderDepth(id), 1));
            o.local = float4(p - (rect.xy + rect.zw * 0.5), root);
            // Textures run up from their bottom; this space runs down.
            o.uv = float2(inst.uv.x + t.x * inst.uv.z, inst.uv.y + (1 - t.y) * inst.uv.w);
            o.color = inst.color;
            o.color2 = inst.color2;
            o.radii = inst.radii;
            o.params = inst.params;
            o.uvRect = inst.uv;
            o.shape = float4(rect.zw * 0.5, slot.sizeOpacity.z, slot.row0.w);
            o.kind = uint2(inst.type | (inst.flags << 4) | (inst.textureIndex << 12), asuint(slot.row1.w));
            o.clips = clips;
            return o;
        }

        float4 Frag(Varyings i) : SV_Target
        {
            uint type = i.kind.x & 15u;
            uint flags = (i.kind.x >> 4) & 255u;
            uint textureIndex = i.kind.x >> 12;
            float2 local = i.local.xy;
            float2 root = i.local.zw;
            float2 halfSize = i.shape.xy;
            float opacity = i.shape.z;
            float saturation = i.shape.w;
            float brightness = asfloat(i.kind.y);

            // Derivatives first, outside any branch.
            float2 localPixel = fwidth(local);
            float aa = max(max(localPixel.x, localPixel.y), 1e-5);
            float2 rootPixel = fwidth(root);
            float rootAA = max(max(rootPixel.x, rootPixel.y), 1e-5);
            float2 localDx = ddx(local), localDy = ddy(local);
            // Gradients taken before choosing the texture, so the choice is free to branch.
            float2 uvDx = ddx(i.uv), uvDy = ddy(i.uv);
            // Only what draws from a texture samples one (an image, a glyph, a group's image, a pair's): shapes and
            // shadows draw from their distance fields alone. Every pixel of a 2x2 block shaded together draws the
            // same primitive, so a block takes one way here, and the field's width on screen (below) is taken from
            // samples all of a block made.
            float4 tex = 0;
            float2 uv = i.uv;
            [branch]
            if (type >= 2u)
            {
                // Kept inside its own part of the texture: an image's crop or a glyph's cell in its atlas.
                float2 uvMin = min(i.uvRect.xy, i.uvRect.xy + i.uvRect.zw);
                float2 uvMax = max(i.uvRect.xy, i.uvRect.xy + i.uvRect.zw);
                if ((flags & 16u) != 0u)
                {
                    // A nine-slice samples by where the point is in its rect, its gradients following each part's stretch.
                    float2 rate;
                    uv = SliceUv(local + halfSize, halfSize * 2, i.params, i.color2, i.uvRect, rate);
                    uvDx = localDx * rate;
                    uvDy = localDy * rate;
                }
                uv = clamp(uv, uvMin, uvMax);
                tex = SampleTexture(textureIndex & 15u, uv, uvDx, uvDy);
            }
            float fieldPixel = max(fwidth(tex.a), 1e-4);

            // The overdraw view: every fragment shaded adds a little heat (alpha 0 adds under premultiplied blending),
            // whatever it covers, as the GPU pays for it; a group's quad adds its image's heat (what was drawn into
            // it) at full strength. A debug view's own primitives draw as they are.
            if (_UINextOverdraw > 0.5 && (flags & 8u) == 0u)
            {
                float4 heat = float4(0.12, 0.055, 0.02, 0);
                return type == 5u ? float4(tex.rgb, 0) + heat : heat;
            }

            // A group's image: premultiplied already, drawn at the group's opacity, covering the root.
            if (type == 5u)
                return tex * (i.color.a * opacity);

            float4 color = i.color;
            float coverage = 1;
            bool premultiplied = false;
            if (type == 6u)
            {
                // A matched pair: the picture of where it came from and the node it goes to, each an image of its own
                // (premultiplied) holding its whole end, cross-faded by summing them, as the web's view transitions blend
                // an image pair (plus-lighter), so where both are opaque it stays opaque all the way.
                float4 live = SampleTexture((textureIndex >> 4) & 15u, uv, uvDx, uvDy);
                color = min(tex * (1.0 - i.color.a) + live * i.color.a, 1.0);
                premultiplied = true;
            }
            else if (type == 0u)
            {
                float d = SdRoundRect(local, halfSize, i.radii);
                coverage = saturate(0.5 - d / aa);
                if ((flags & 2u) != 0u)
                    color = lerp(i.color, i.color2, GradientT(local, halfSize, i.params.z));
                if ((flags & 4u) != 0u)
                {
                    // A ring: only its border, params.x wide inside its edge, in its colour or gradient.
                    coverage *= 1 - saturate(0.5 - (d + i.params.x) / aa);
                }
                else if ((flags & 2u) == 0u && i.params.x > 0)
                {
                    float inner = saturate(0.5 - (d + i.params.x) / aa);
                    color = lerp(i.color2, i.color, inner);
                }
            }
            else if (type == 1u)
            {
                float d = SdRoundRect(local, halfSize, i.radii);
                float blur = max(i.params.y, aa);
                coverage = 1 - smoothstep(-blur, blur, d);
                if ((flags & 2u) != 0u)
                    color = lerp(i.color, i.color2, GradientT(local, halfSize, i.params.z));
            }
            else if (type == 2u)
            {
                float d = SdRoundRect(local, halfSize, i.radii);
                coverage = saturate(0.5 - d / aa);
                color = tex * i.color;
            }
            else if (type == 3u)
            {
                // The distance field holds 0.5 at the glyph's edge (less where synthetic bold widens it), resolved
                // over about a pixel by how fast the field changes on screen, so edges stay crisp at any size. An
                // outline (params.w, a share of the field) lies past that edge in the second colour.
                float edge = 0.5 - i.params.y;
                float fill = saturate((tex.a - edge) / fieldPixel + 0.5);
                coverage = fill;
                if (i.params.w > 0)
                {
                    coverage = saturate((tex.a - (edge - i.params.w)) / fieldPixel + 0.5);
                    color = lerp(i.color2, i.color, fill);
                }
            }
            else
            {
                // A colour bitmap glyph (emoji): its own colours, the tint's alpha.
                color = tex * i.color;
            }

            // Its clips, each its rounded rect in its own space, smoothed over a pixel by its scale.
            [unroll]
            for (int k = 0; k < 4; k++)
            {
                float index = i.clips[k];
                if (index < 0) break;
                Clip c = _Clips[(uint)index];
                float2 cp = float2(dot(c.inverse0.xy, root) + c.inverse0.z, dot(c.inverse1.xy, root) + c.inverse1.z);
                coverage *= saturate(0.5 + InsideRoundRect(float4(cp, c.size.xy - cp), c.radii) / max(rootAA * c.size.z, 1e-5));
            }

            // A pair is drawn as its images were, their filters applied already.
            if (premultiplied)
                return color * (coverage * opacity);

            float alpha = saturate(color.a) * coverage * opacity;
            float3 rgb = color.rgb * alpha;
            // Its colour filters, on the premultiplied colour: towards grey by its saturation, then by its brightness.
            [branch]
            if (saturation != 1 || brightness != 1)
            {
                float luma = dot(rgb, float3(0.2126, 0.7152, 0.0722));
                rgb = lerp(luma.xxx, rgb, saturation) * brightness;
            }
            return float4(rgb, alpha);
        }

        // What of a primitive is opaque all over, in its node's space: a shape (not a ring) of opaque colours, its node
        // upright and fully shown, within its rounded corners (its rect brought in by 0.29 of its largest radius, where
        // the corners' circles cross their diagonals) and a pixel and a half in from its smoothed edge, and cut to what
        // its clips leave all of. None (no area) for anything else.
        void OpaqueRect(Instance inst, Slot slot, float4 rect, out float2 opaqueMin, out float2 opaqueMax)
        {
            float2 halfSize = rect.zw * 0.5;
            float radius = min(max(max(inst.radii.x, inst.radii.y), max(inst.radii.z, inst.radii.w)), min(halfSize.x, halfSize.y));
            // A pixel in its own space, along the axis its transform shrinks the most.
            float2 scale = abs(float2(slot.row0.x, slot.row1.y));
            float pixel = slot.sizeOpacity.w * sqrt(scale.x * scale.y) / max(min(scale.x, scale.y), 1e-6);
            float inset = 0.2929 * radius + 1.5 * pixel;
            opaqueMin = rect.xy + inset;
            opaqueMax = rect.xy + rect.zw - inset;
            bool twoColours = (inst.flags & 2u) != 0u || inst.params.x > 0;
            bool opaque = inst.type == 0u && (inst.flags & 4u) == 0u && slot.sizeOpacity.z >= 1 && Upright(slot.row0, slot.row1)
                && inst.color.a >= 1 && (!twoColours || inst.color2.a >= 1) && all(opaqueMax > opaqueMin);
            float4 clips = slot.clips;
            if (!opaque || !CutToClips(slot, 0, -1, opaqueMin, opaqueMax, clips))
                opaqueMax = opaqueMin;
        }

        float4 VertOccluder(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID) : SV_POSITION
        {
            uint id = (uint)_InstanceOffset + instanceID;
            Instance inst = _Instances[id];
            Slot slot = _Slots[inst.slot];
            float2 opaqueMin, opaqueMax;
            OpaqueRect(inst, slot, RectOf(inst, slot), opaqueMin, opaqueMax);
            float2 p = opaqueMin + kCorners[vertexID % 6] * (opaqueMax - opaqueMin);
            float2 root = float2(dot(slot.row0.xy, p) + slot.row0.z, dot(slot.row1.xy, p) + slot.row1.z);
            return mul(_UIMatrix, float4(root, OrderDepth(id), 1));
        }

        float4 FragOccluder() : SV_Target
        {
            return 0;
        }
        ENDHLSL

        // Everything, in order. In the game view each primitive is drawn at the depth of its place in the order, and
        // what a later one hides (see Occluders) fails the depth test before it is shaded.
        Pass
        {
            Name "Primitive"
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            ENDHLSL
        }

        // In the game view, before everything: the depth of each primitive where it is opaque all over, so what lies
        // under it is never shaded.
        Pass
        {
            Name "Occluders"
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma vertex VertOccluder
            #pragma fragment FragOccluder
            #pragma target 4.5
            ENDHLSL
        }
    }
}
