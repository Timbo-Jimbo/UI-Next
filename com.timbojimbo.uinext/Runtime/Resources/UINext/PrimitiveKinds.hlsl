// The primitives' shaders, one pass a kind, each built from this file with its kind defined (KIND_SHAPE, KIND_SHADOW,
// KIND_IMAGE, KIND_SLICED, KIND_GLYPH, KIND_BITMAP, KIND_GROUP, KIND_PAIR) and, for its clipped pass, CLIPPED. A pass's
// vertex shader expands a primitive's record into a quad in its node's space, cut to the node's clips (what a scroll view
// has scrolled out from under its edges is never shaded), and passes its pixel shader only what that kind reads, once a
// vertex rather than once a pixel; the pixel shader does only that kind's work: a signed distance field (a shape, a
// shadow, an image's corners), a sample (an image, a glyph, a group's image, a pair's two), the node's colour filters,
// and in a clipped pass the clips the quad crosses, each a rounded rect in its own space. Colours are sRGB values and
// blend as they are (the project blends in gamma), premultiplied here.
//
// The compositor draws a run of primitives with its kind's plain pass when every clip over the run's nodes is upright and
// square-cornered and the nodes are upright, since the vertex shader's cut is then exact and no pixel need test a clip;
// otherwise with the clipped pass. A primitive draws at the depth of its place in the order (OrderDepth), after the
// occluders (VertOccluder) have laid down the depth of every large opaque shape's inside, so what a card covers fails
// the depth test before it is shaded.

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

// A clip: its transform from its root's space into its own, its size (and in z that transform's scale), and its corner
// radii.
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
// The order the primitives are drawn in (by layer, then kind): what an instance index is looked up through.
StructuredBuffer<uint> _Order;
int _InstanceOffset;
float4x4 _UIMatrix;
// Above 0, the game view's main pass, which has a depth buffer: each of its _InstanceTotal primitives is drawn at the
// depth of its place in the order (OrderDepth), so the occluders drawn first hide what lies under them.
float _UINextOcclusion;
int _InstanceTotal;
// A batch draws with up to eight textures (atlases of different fonts, emoji, images); each primitive says which it
// samples.
Texture2D _Texture0, _Texture1, _Texture2, _Texture3, _Texture4, _Texture5, _Texture6, _Texture7;
SamplerState sampler_linear_clamp;

// A sample with the hardware's gradients: images and bitmap glyphs, which may have mipmaps.
float4 SampleTexture(uint index, float2 uv)
{
    switch (index)
    {
        case 1: return _Texture1.Sample(sampler_linear_clamp, uv);
        case 2: return _Texture2.Sample(sampler_linear_clamp, uv);
        case 3: return _Texture3.Sample(sampler_linear_clamp, uv);
        case 4: return _Texture4.Sample(sampler_linear_clamp, uv);
        case 5: return _Texture5.Sample(sampler_linear_clamp, uv);
        case 6: return _Texture6.Sample(sampler_linear_clamp, uv);
        case 7: return _Texture7.Sample(sampler_linear_clamp, uv);
        default: return _Texture0.Sample(sampler_linear_clamp, uv);
    }
}

// A sample of a distance field atlas, which has no mipmaps: its top level, no gradients worked out.
float4 SampleAtlas(uint index, float2 uv)
{
    switch (index)
    {
        case 1: return _Texture1.SampleLevel(sampler_linear_clamp, uv, 0);
        case 2: return _Texture2.SampleLevel(sampler_linear_clamp, uv, 0);
        case 3: return _Texture3.SampleLevel(sampler_linear_clamp, uv, 0);
        case 4: return _Texture4.SampleLevel(sampler_linear_clamp, uv, 0);
        case 5: return _Texture5.SampleLevel(sampler_linear_clamp, uv, 0);
        case 6: return _Texture6.SampleLevel(sampler_linear_clamp, uv, 0);
        case 7: return _Texture7.SampleLevel(sampler_linear_clamp, uv, 0);
        default: return _Texture0.SampleLevel(sampler_linear_clamp, uv, 0);
    }
}

// A quad's four corners, drawn as two triangles by the compositor's index buffer (0 1 2, 0 2 3).
static const float2 kCorners[4] =
{
    float2(0, 0), float2(1, 0), float2(1, 1), float2(0, 1)
};

// How far inside a rounded rect a point is (negative outside), from its distances to the rect's edges (left, top,
// right, bottom) and the corners' radii (top-left, top-right, bottom-right, bottom-left, none more than half its
// shorter side): within a corner's radius of both its edges, from the corner's circle; elsewhere from its nearest edge.
float InsideRoundRect(float4 edges, float4 radii)
{
    float2 near = float2(min(edges.x, edges.z), min(edges.y, edges.w));
    float r = edges.x < edges.z ? (edges.y < edges.w ? radii.x : radii.w) : (edges.y < edges.w ? radii.y : radii.z);
    float2 q = r - near;
    return q.x > 0 && q.y > 0 ? r - length(q) : min(near.x, near.y);
}

// Signed distance to a rounded rect centred on the origin (y down), each corner its own radius: top-left, top-right,
// bottom-right, bottom-left.
float SdRoundRect(float2 p, float2 halfSize, float4 radii)
{
    float r = p.x < 0 ? (p.y < 0 ? radii.x : radii.w) : (p.y < 0 ? radii.y : radii.z);
    r = min(r, min(halfSize.x, halfSize.y));
    float2 q = abs(p) - halfSize + r;
    return min(max(q.x, q.y), 0) + length(max(q, 0)) - r;
}

// Whether a 2x3 transform's rows (xy the matrix, z the offset) keep a rect upright: no turn, no shear, no axis squashed
// flat.
bool Upright(float4 row0, float4 row1)
{
    return abs(row0.y) < 1e-6 && abs(row1.x) < 1e-6 && abs(row0.x) > 1e-6 && abs(row1.y) > 1e-6;
}

// What of a primitive's quad its clips leave, and which clips still cut it pixel by pixel. The quad runs from `quadMin`
// to `quadMax` in its node's space, its top moved right by `skew` (an italic glyph's). A clip it lies wholly inside (a
// pixel in from its edges and clear of its rounded corners) leaves all of it, so it is not tested a pixel; one it lies
// wholly outside leaves none, so it is not drawn. One it crosses is kept, and where the node and the clip are both
// upright the quad is cut down to the clip's rect, `spare` pixels out from it: a primitive keeps one past it for its
// smoothed edge, and what lies further is never shaded, where it would otherwise be shaded only to be cut away (an
// occluder keeps only what lies a pixel inside it, -1). Whether each clip it crosses cut it exactly so (both upright,
// the clip square-cornered): then no pixel need test them, and none is kept.
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
    clips = exact ? float4(-1, -1, -1, -1) : kept;
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

// The depth in its root's space (the matrix makes it the platform's) of the id-th primitive in the game view's main
// pass: from the far end, for the first, to the near, for the last. Elsewhere all lie at 0.
float OrderDepth(uint id)
{
    return _UINextOcclusion > 0.5 ? 2 * (id + 1.0) / (_InstanceTotal + 2.0) - 1 : 0;
}

// A corner of a primitive's quad, as every kind's vertex shader works it out: its record and slot, its rect, its
// position, where it is in its primitive's space (from its centre) and its root's, its texture coordinate, and the clips
// its pixels must still test.
struct Corner
{
    Instance inst;
    Slot slot;
    float4 rect;
    float4 position;
    float2 local;
    float2 root;
    float2 uv;
    float4 clips;
};

// Room around the quad: a pixel and a half of the node's, for a smoothed edge, and a shadow's blur; none for a glyph,
// whose quad has its own and whose neighbours in the atlas lie just past it, or for a group's image or a pair's, which
// cover their region exactly.
#if defined(KIND_GLYPH) || defined(KIND_BITMAP) || defined(KIND_GROUP) || defined(KIND_PAIR)
#define QUAD_MARGIN(inst, slot) 0
#elif defined(KIND_SHADOW)
#define QUAD_MARGIN(inst, slot) (1.5 * max((slot).sizeOpacity.w, 0.01) + (inst).params.y * 2.5)
#else
#define QUAD_MARGIN(inst, slot) (1.5 * max((slot).sizeOpacity.w, 0.01))
#endif

// Italic: a glyph's top is moved right by its skew.
#if defined(KIND_GLYPH) || defined(KIND_BITMAP)
#define QUAD_SKEW(inst) (inst).params.z
#else
#define QUAD_SKEW(inst) 0
#endif

// The parts a primitive's quad is drawn in, each a quad of its own from the same record, placed by the vertex shader
// (the compositor draws a run with its count times its kind's parts, and a vertex takes its primitive from its instance
// index divided by them): a nine-slice's nine slices, each sampling its own part of the image at its own rate, with the
// hardware's gradients, so it draws as an image. One for the rest: a rounded rect's bands between its corners could do a
// box's work, but in one pass the compiler computes both paths and selects, and in a pass of their own the draw order
// of overlapping shapes in a run would go wrong.
// A shape's seven parts: its four corner squares (as big as the largest radius, with the room round the quad), drawn by
// the shape passes, and three bands between them (the two sides, and the middle column), drawn by the flat passes after
// them, where a rounded rect's distance is a box's. Nothing within a layer overlaps, so the order between shapes is free.
#if defined(KIND_SHAPE) && defined(FLAT_ONLY)
#define PARTS 3u
#define PART_FROM 4u
#elif defined(KIND_SHAPE)
#define PARTS 4u
#define PART_FROM 0u
#elif defined(KIND_SLICED)
#define PARTS 9u
#define PART_FROM 0u
#else
#define PARTS 1u
#define PART_FROM 0u
#endif

#if defined(KIND_SHAPE)
// 0 top-left, 1 top-right, 2 bottom-right, 3 bottom-left, 4 the left band, 5 the right band, 6 the middle column.
void PartRect(Instance inst, float4 rect, float margin, uint part, out float2 partMin, out float2 partMax, out float4 sliceX, out float4 sliceY)
{
    float2 lo = rect.xy, hi = rect.xy + rect.zw;
    float r = max(0, min(max(max(inst.radii.x, inst.radii.y), max(inst.radii.z, inst.radii.w)), min(rect.z, rect.w) * 0.5));
    uint col = part == 0u || part == 3u || part == 4u ? 0u : part == 1u || part == 2u || part == 5u ? 2u : 1u;
    uint row = part < 2u ? 0u : part < 4u ? 2u : 1u;
    partMin.x = col == 0u ? lo.x - margin : col == 1u ? lo.x + r : hi.x - r;
    partMax.x = col == 0u ? lo.x + r : col == 1u ? hi.x - r : hi.x + margin;
    partMin.y = part == 6u ? lo.y - margin : row == 0u ? lo.y - margin : row == 1u ? lo.y + r : hi.y - r;
    partMax.y = part == 6u ? hi.y + margin : row == 0u ? lo.y + r : row == 1u ? hi.y - r : hi.y + margin;
    sliceX = sliceY = 0;
}
#endif

#if PARTS == 9u
// One axis of a nine-slice, for slice `k` of three: where it runs, and what share of the image its ends sample, from
// the borders `head` and `tail` (points) and their shares; borders longer together than the rect shrink alike to fit
// it. (x0, x1, u0, u1.)
float4 SliceOf(uint k, float lo, float hi, float head, float tail, float headShare, float tailShare)
{
    float fit = min(1, (hi - lo) / max(head + tail, 1e-5));
    head *= fit;
    tail *= fit;
    float x0 = k == 0u ? lo : k == 1u ? lo + head : hi - tail;
    float x1 = k == 0u ? lo + head : k == 1u ? hi - tail : hi;
    float u0 = k == 0u ? 0 : k == 1u ? headShare : 1 - tailShare;
    float u1 = k == 0u ? headShare : k == 1u ? 1 - tailShare : 1;
    return float4(x0, x1, u0, u1);
}

// A slice's rect in its node's space, the margin round the quad included on the outside, and how a point along each
// axis maps to its share of the image.
void PartRect(Instance inst, float4 rect, float margin, uint part, out float2 partMin, out float2 partMax, out float4 sliceX, out float4 sliceY)
{
    uint col = part % 3u, row = part / 3u;
    float2 lo = rect.xy, hi = rect.xy + rect.zw;
    sliceX = SliceOf(col, lo.x, hi.x, inst.params.x, inst.params.z, inst.color2.x, inst.color2.z);
    sliceY = SliceOf(row, lo.y, hi.y, inst.params.y, inst.params.w, inst.color2.y, inst.color2.w);
    partMin = float2(col == 0u ? sliceX.x - margin : sliceX.x, row == 0u ? sliceY.x - margin : sliceY.x);
    partMax = float2(col == 2u ? sliceX.y + margin : sliceX.y, row == 2u ? sliceY.y + margin : sliceY.y);
}
#endif

Corner CornerOf(uint vertexID, uint instanceID)
{
    Corner c;
    uint id = _Order[(uint)_InstanceOffset + instanceID / PARTS];
    c.inst = _Instances[id];
    c.slot = _Slots[c.inst.slot];
    c.rect = RectOf(c.inst, c.slot);
    float margin = QUAD_MARGIN(c.inst, c.slot);
    float skew = QUAD_SKEW(c.inst);
    float2 quadMin = c.rect.xy - margin, quadMax = c.rect.xy + c.rect.zw + margin;
    c.clips = c.slot.clips;
    CutToClips(c.slot, skew, 1, quadMin, quadMax, c.clips);
#if PARTS > 1u
    // Its part of the quad, and what the clips left of that.
    float2 partMin, partMax;
    float4 sliceX, sliceY;
    PartRect(c.inst, c.rect, margin, PART_FROM + instanceID % PARTS, partMin, partMax, sliceX, sliceY);
    quadMin = max(quadMin, partMin);
    quadMax = min(quadMax, partMax);
    if (any(quadMax <= quadMin))
        quadMax = quadMin;
#endif
    float2 corner = kCorners[vertexID & 3u];
    float2 p = quadMin + corner * (quadMax - quadMin);
#if PARTS == 9u
    // A slice samples its own part of the image: from where it runs to the shares its ends sample.
    float2 t = float2(sliceX.z + (p.x - sliceX.x) / max(sliceX.y - sliceX.x, 1e-5) * (sliceX.w - sliceX.z),
                      sliceY.z + (p.y - sliceY.x) / max(sliceY.y - sliceY.x, 1e-5) * (sliceY.w - sliceY.z));
#else
    float2 t = (p - c.rect.xy) / max(c.rect.zw, 1e-5);
#endif
    p.x += skew * (1 - corner.y);
    c.root = float2(dot(c.slot.row0.xy, p) + c.slot.row0.z, dot(c.slot.row1.xy, p) + c.slot.row1.z);
    c.position = mul(_UIMatrix, float4(c.root, OrderDepth(id), 1));
    c.local = p - (c.rect.xy + c.rect.zw * 0.5);
    // Textures run up from their bottom; this space runs down.
    c.uv = float2(c.inst.uv.x + t.x * c.inst.uv.z, c.inst.uv.y + (1 - t.y) * c.inst.uv.w);
    return c;
}

// A gradient's axis: along `degrees` (0 left to right, 90 top to bottom), scaled so that a point's share along it,
// 0 to 1 across the rect, is dot(local, axis) + 0.5. Worked out once a vertex.
float2 GradientAxis(float degrees, float2 halfSize)
{
    float angle = radians(degrees);
    float2 direction = float2(cos(angle), sin(angle));
    float extent = abs(direction.x) * halfSize.x + abs(direction.y) * halfSize.y;
    return direction / max(2 * extent, 1e-5);
}

// ── What every kind passes and does ─────────────────────────────────────────

// Its clips, each its rounded rect in its own space, smoothed over a pixel by its scale.
float ClipCoverage(float4 clips, float2 root)
{
    float coverage = 1;
#if defined(CLIPPED)
    float2 rootPixel = fwidth(root);
    float rootAA = max(max(rootPixel.x, rootPixel.y), 1e-5);
    // Rolled, so the clips share registers: unrolled, four clips' tests held more than half the core's.
    [loop]
    for (int k = 0; k < 4; k++)
    {
        float index = clips[k];
        if (index < 0) break;
        Clip c = _Clips[(uint)index];
        float2 cp = float2(dot(c.inverse0.xy, root) + c.inverse0.z, dot(c.inverse1.xy, root) + c.inverse1.z);
        coverage *= saturate(0.5 + InsideRoundRect(float4(cp, c.size.xy - cp), c.radii) / max(rootAA * c.size.z, 1e-5));
    }
#endif
    return coverage;
}

// The colour as drawn: premultiplied, at its coverage and opacity, through its node's colour filters (towards grey by
// its saturation, then by its brightness).
float4 Drawn(float4 color, float coverage, float opacity, float saturation, float brightness)
{
    float alpha = saturate(color.a) * coverage * opacity;
    float3 rgb = color.rgb * alpha;
    [branch]
    if (saturation != 1 || brightness != 1)
    {
        float luma = dot(rgb, float3(0.2126, 0.7152, 0.0722));
        rgb = lerp(luma.xxx, rgb, saturation) * brightness;
    }
    return float4(rgb, alpha);
}

// Coverage of a box's smoothed edge, and its distance: what a rounded rect's is in its bands, between its corners.
float BoxCoverage(float2 local, float2 halfSize, out float d)
{
    float2 px = fwidth(local);
    float aa = max(max(px.x, px.y), 1e-5);
    float2 q = abs(local) - halfSize;
    d = max(q.x, q.y);
    return saturate(0.5 - d / aa);
}

// Coverage of a rounded rect's smoothed edge, and its distance.
float EdgeCoverage(float2 local, float2 halfSize, float4 radii, out float d)
{
    float2 px = fwidth(local);
    float aa = max(max(px.x, px.y), 1e-5);
    d = SdRoundRect(local, halfSize, radii);
    return saturate(0.5 - d / aa);
}

// Kept inside its own part of the texture: an image's crop or a glyph's cell in its atlas.
float2 ClampUv(float2 uv, float4 uvRect)
{
    float2 uvMin = min(uvRect.xy, uvRect.xy + uvRect.zw);
    float2 uvMax = max(uvRect.xy, uvRect.xy + uvRect.zw);
    return clamp(uv, uvMin, uvMax);
}

#if defined(CLIPPED)
#define CLIP_VARYINGS nointerpolation float4 clips : TEXCOORD12;
#define SET_CLIPS(o, c) (o).clips = (c).clips;
#define COVERAGE_OF(i) ClipCoverage((i).clips, (i).root)
#else
#define CLIP_VARYINGS
#define SET_CLIPS(o, c)
#define COVERAGE_OF(i) 1
#endif

// ── Shape: a rounded rect's fill, with a border inside its edge, or as a ring, or as a gradient ────────────────────

#if defined(KIND_SHAPE)
struct Varyings
{
    float4 position : SV_POSITION;
    float2 local : TEXCOORD0;
    float2 root : TEXCOORD1;
    nointerpolation float4 color : TEXCOORD2;
    nointerpolation float4 color2 : TEXCOORD3;
    nointerpolation float4 radii : TEXCOORD4;
    // Its half size, its opacity, its border width.
    nointerpolation float4 shape : TEXCOORD5;
    // Its gradient's axis, its saturation and brightness.
    nointerpolation float4 extra : TEXCOORD6;
    nointerpolation uint flags : TEXCOORD7;
    CLIP_VARYINGS
};

Varyings Vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
{
    Corner c = CornerOf(vertexID, instanceID);
    float2 halfSize = c.rect.zw * 0.5;
    Varyings o;
    o.position = c.position;
    o.local = c.local;
    o.root = c.root;
    o.color = c.inst.color;
    o.color2 = c.inst.color2;
    o.radii = c.inst.radii;
    o.shape = float4(halfSize, c.slot.sizeOpacity.z, c.inst.params.x);
    o.extra = float4(GradientAxis(c.inst.params.z, halfSize), c.slot.row0.w, c.slot.row1.w);
    o.flags = c.inst.flags;
    SET_CLIPS(o, c)
    return o;
}

float4 Frag(Varyings i) : SV_Target
{
    float d;
#if defined(FLAT_ONLY)
    float coverage = BoxCoverage(i.local, i.shape.xy, d);
#else
    float coverage = EdgeCoverage(i.local, i.shape.xy, i.radii, d);
#endif
    float2 px = fwidth(i.local);
    float aa = max(max(px.x, px.y), 1e-5);
    float4 color = i.color;
    bool gradient = (i.flags & 2u) != 0u;
    if (gradient)
        color = lerp(i.color, i.color2, saturate(dot(i.local, i.extra.xy) + 0.5));
    if ((i.flags & 4u) != 0u)
    {
        // A ring: only its border, shape.w wide inside its edge, in its colour or gradient.
        coverage *= 1 - saturate(0.5 - (d + i.shape.w) / aa);
    }
    else if (!gradient && i.shape.w > 0)
    {
        // A border in the second colour, the fill inside it.
        color = lerp(i.color2, i.color, saturate(0.5 - (d + i.shape.w) / aa));
    }
    coverage *= COVERAGE_OF(i);
    return Drawn(color, coverage, i.shape.z, i.extra.z, i.extra.w);
}
#endif

// ── Shadow: a rounded rect blurred, in a colour or a gradient ────────────────────────────────────────────────────

#if defined(KIND_SHADOW)
struct Varyings
{
    float4 position : SV_POSITION;
    float2 local : TEXCOORD0;
    float2 root : TEXCOORD1;
    nointerpolation float4 color : TEXCOORD2;
    nointerpolation float4 color2 : TEXCOORD3;
    nointerpolation float4 radii : TEXCOORD4;
    // Its half size, its opacity, its blur.
    nointerpolation float4 shape : TEXCOORD5;
    // Its gradient's axis, its saturation and brightness.
    nointerpolation float4 extra : TEXCOORD6;
    nointerpolation uint flags : TEXCOORD7;
    CLIP_VARYINGS
};

Varyings Vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
{
    Corner c = CornerOf(vertexID, instanceID);
    float2 halfSize = c.rect.zw * 0.5;
    Varyings o;
    o.position = c.position;
    o.local = c.local;
    o.root = c.root;
    o.color = c.inst.color;
    o.color2 = c.inst.color2;
    o.radii = c.inst.radii;
    o.shape = float4(halfSize, c.slot.sizeOpacity.z, c.inst.params.y);
    o.extra = float4(GradientAxis(c.inst.params.z, halfSize), c.slot.row0.w, c.slot.row1.w);
    o.flags = c.inst.flags;
    SET_CLIPS(o, c)
    return o;
}

float4 Frag(Varyings i) : SV_Target
{
    float2 px = fwidth(i.local);
    float aa = max(max(px.x, px.y), 1e-5);
    float d = SdRoundRect(i.local, i.shape.xy, i.radii);
    float blur = max(i.shape.w, aa);
    float coverage = 1 - smoothstep(-blur, blur, d);
    float4 color = i.color;
    if ((i.flags & 2u) != 0u)
        color = lerp(i.color, i.color2, saturate(dot(i.local, i.extra.xy) + 0.5));
    coverage *= COVERAGE_OF(i);
    return Drawn(color, coverage, i.shape.z, i.extra.z, i.extra.w);
}
#endif

// ── Image: a texture tinted and cut to a rounded rect (a nine-slice too, a slice at a time) ────────────────────────────────────────────────────────────

#if defined(KIND_IMAGE) || defined(KIND_SLICED)
struct Varyings
{
    float4 position : SV_POSITION;
    float2 local : TEXCOORD0;
    float2 root : TEXCOORD1;
    float2 uv : TEXCOORD2;
    nointerpolation float4 color : TEXCOORD3;
    nointerpolation float4 radii : TEXCOORD4;
    // Its half size, its opacity, its texture.
    nointerpolation float4 shape : TEXCOORD5;
    nointerpolation float4 uvRect : TEXCOORD6;
    // Its saturation and brightness.
    nointerpolation float2 filters : TEXCOORD7;
    CLIP_VARYINGS
};

Varyings Vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
{
    Corner c = CornerOf(vertexID, instanceID);
    Varyings o;
    o.position = c.position;
    o.local = c.local;
    o.root = c.root;
    o.uv = c.uv;
    o.color = c.inst.color;
    o.radii = c.inst.radii;
    o.shape = float4(c.rect.zw * 0.5, c.slot.sizeOpacity.z, c.inst.textureIndex & 15u);
    o.uvRect = c.inst.uv;
    o.filters = float2(c.slot.row0.w, c.slot.row1.w);
    SET_CLIPS(o, c)
    return o;
}

float4 Frag(Varyings i) : SV_Target
{
    float d;
    float coverage = EdgeCoverage(i.local, i.shape.xy, i.radii, d);
    float4 color = SampleTexture((uint)i.shape.w, ClampUv(i.uv, i.uvRect)) * i.color;
    coverage *= COVERAGE_OF(i);
    return Drawn(color, coverage, i.shape.z, i.filters.x, i.filters.y);
}
#endif

// ── Sliced image: a nine-slice, its borders keeping their size while the rest stretches: drawn as an image, a slice at a
// time (PartRect), each sampling its own part of the image at its own rate with the hardware's gradients ─────────

// ── SDF glyph: from a distance field atlas, widened for synthetic bold, with an outline ──────────────────────────

#if defined(KIND_GLYPH)
struct Varyings
{
    float4 position : SV_POSITION;
    float2 uv : TEXCOORD0;
    float2 root : TEXCOORD1;
    nointerpolation float4 color : TEXCOORD2;
    nointerpolation float4 color2 : TEXCOORD3;
    nointerpolation float4 uvRect : TEXCOORD4;
    // Its edge (0.5, less where synthetic bold widens it), its outline's width past that, its opacity, its texture.
    nointerpolation float4 glyph : TEXCOORD5;
    nointerpolation float2 filters : TEXCOORD6;
    CLIP_VARYINGS
};

Varyings Vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
{
    Corner c = CornerOf(vertexID, instanceID);
    Varyings o;
    o.position = c.position;
    o.uv = c.uv;
    o.root = c.root;
    o.color = c.inst.color;
    o.color2 = c.inst.color2;
    o.uvRect = c.inst.uv;
    o.glyph = float4(0.5 - c.inst.params.y, c.inst.params.w, c.slot.sizeOpacity.z, c.inst.textureIndex & 15u);
    o.filters = float2(c.slot.row0.w, c.slot.row1.w);
    SET_CLIPS(o, c)
    return o;
}

float4 Frag(Varyings i) : SV_Target
{
    // The distance field holds 0.5 at the glyph's edge, resolved over about a pixel by how fast the field changes on
    // screen, so edges stay crisp at any size. An outline lies past that edge in the second colour.
    float field = SampleAtlas((uint)i.glyph.w, ClampUv(i.uv, i.uvRect)).a;
    float fieldPixel = max(fwidth(field), 1e-4);
    float fill = saturate((field - i.glyph.x) / fieldPixel + 0.5);
    float coverage = fill;
    float4 color = i.color;
    if (i.glyph.y > 0)
    {
        coverage = saturate((field - (i.glyph.x - i.glyph.y)) / fieldPixel + 0.5);
        color = lerp(i.color2, i.color, fill);
    }
    coverage *= COVERAGE_OF(i);
    return Drawn(color, coverage, i.glyph.z, i.filters.x, i.filters.y);
}
#endif

// ── Bitmap glyph: a colour glyph (emoji) with its own colours, at the tint's alpha ────────────────────────────────

#if defined(KIND_BITMAP)
struct Varyings
{
    float4 position : SV_POSITION;
    float2 uv : TEXCOORD0;
    float2 root : TEXCOORD1;
    nointerpolation float4 color : TEXCOORD2;
    nointerpolation float4 uvRect : TEXCOORD3;
    // Its opacity, its texture, its saturation and brightness.
    nointerpolation float4 glyph : TEXCOORD4;
    CLIP_VARYINGS
};

Varyings Vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
{
    Corner c = CornerOf(vertexID, instanceID);
    Varyings o;
    o.position = c.position;
    o.uv = c.uv;
    o.root = c.root;
    o.color = c.inst.color;
    o.uvRect = c.inst.uv;
    o.glyph = float4(c.slot.sizeOpacity.z, c.inst.textureIndex & 15u, c.slot.row0.w, c.slot.row1.w);
    SET_CLIPS(o, c)
    return o;
}

float4 Frag(Varyings i) : SV_Target
{
    float4 color = SampleTexture((uint)i.glyph.y, ClampUv(i.uv, i.uvRect)) * i.color;
    return Drawn(color, COVERAGE_OF(i), i.glyph.x, i.glyph.z, i.glyph.w);
}
#endif

// ── Group image: what was drawn into a group, premultiplied already and filtered, at the group's opacity ─────────

#if defined(KIND_GROUP)
struct Varyings
{
    float4 position : SV_POSITION;
    float2 uv : TEXCOORD0;
    float2 root : TEXCOORD1;
    // Its opacity (the group's, by its colour's alpha), its texture.
    nointerpolation float2 group : TEXCOORD2;
    CLIP_VARYINGS
};

Varyings Vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
{
    Corner c = CornerOf(vertexID, instanceID);
    Varyings o;
    o.position = c.position;
    o.uv = c.uv;
    o.root = c.root;
    o.group = float2(c.inst.color.a * c.slot.sizeOpacity.z, c.inst.textureIndex & 15u);
    SET_CLIPS(o, c)
    return o;
}

float4 Frag(Varyings i) : SV_Target
{
    return SampleTexture((uint)i.group.y, i.uv) * (i.group.x * COVERAGE_OF(i));
}
#endif

// ── Matched pair: the picture of where a flight came from and the node it goes to, summed as they cross-fade ──────

#if defined(KIND_PAIR)
struct Varyings
{
    float4 position : SV_POSITION;
    float2 uv : TEXCOORD0;
    float2 root : TEXCOORD1;
    // How far the fade has gone (by its colour's alpha), its opacity, its two textures.
    nointerpolation float4 pair : TEXCOORD2;
    CLIP_VARYINGS
};

Varyings Vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
{
    Corner c = CornerOf(vertexID, instanceID);
    Varyings o;
    o.position = c.position;
    o.uv = c.uv;
    o.root = c.root;
    o.pair = float4(c.inst.color.a, c.slot.sizeOpacity.z, c.inst.textureIndex & 15u, (c.inst.textureIndex >> 4) & 15u);
    SET_CLIPS(o, c)
    return o;
}

float4 Frag(Varyings i) : SV_Target
{
    // Each end an image of its own (premultiplied) holding its whole end, cross-faded by summing them, as the web's view
    // transitions blend an image pair (plus-lighter), so where both are opaque it stays opaque all the way.
    float4 from = SampleTexture((uint)i.pair.z, i.uv);
    float4 to = SampleTexture((uint)i.pair.w, i.uv);
    float4 color = min(from * (1 - i.pair.x) + to * i.pair.x, 1);
    return color * (i.pair.y * COVERAGE_OF(i));
}
#endif

// ── The overdraw view: every fragment shaded adds a little heat, whatever it covers ──────────────────────────────

#if defined(KIND_OVERDRAW)
struct Varyings
{
    float4 position : SV_POSITION;
};

Varyings Vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID)
{
    Corner c = CornerOf(vertexID, instanceID);
    Varyings o;
    o.position = c.position;
    return o;
}

float4 Frag(Varyings i) : SV_Target
{
    return float4(0.12, 0.055, 0.02, 0);
}
#endif

// ── The occluders: the depth of every large opaque shape's inside, before anything is drawn ──────────────────────

#if defined(KIND_OCCLUDER)
// What of a primitive is opaque all over, in its node's space: a shape (not a ring) of opaque colours, its node upright
// and fully shown, within its rounded corners (its rect brought in by 0.29 of its largest radius, where the corners'
// circles cross their diagonals) and a pixel and a half in from its smoothed edge, and cut to what its clips leave all
// of; and at least 48 pixels a side, as a smaller one hides little for what its own vertices and fill cost. None (no
// area) for anything else.
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
        && inst.color.a >= 1 && (!twoColours || inst.color2.a >= 1) && all(opaqueMax - opaqueMin > 48 * pixel);
    float4 clips = slot.clips;
    if (!opaque || !CutToClips(slot, 0, -1, opaqueMin, opaqueMax, clips))
        opaqueMax = opaqueMin;
}

float4 Vert(uint vertexID : SV_VertexID, uint instanceID : SV_InstanceID) : SV_POSITION
{
    uint id = _Order[(uint)_InstanceOffset + instanceID];
    Instance inst = _Instances[id];
    Slot slot = _Slots[inst.slot];
    float2 opaqueMin, opaqueMax;
    OpaqueRect(inst, slot, RectOf(inst, slot), opaqueMin, opaqueMax);
    float2 p = opaqueMin + kCorners[vertexID & 3u] * (opaqueMax - opaqueMin);
    float2 root = float2(dot(slot.row0.xy, p) + slot.row0.z, dot(slot.row1.xy, p) + slot.row1.z);
    return mul(_UIMatrix, float4(root, OrderDepth(id), 1));
}

float4 Frag() : SV_Target
{
    return 0;
}
#endif
