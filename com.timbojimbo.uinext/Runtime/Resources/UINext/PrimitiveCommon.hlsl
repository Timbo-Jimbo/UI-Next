// What the primitives' shaders share: the records the compositor uploads (an instance a primitive, a slot a node, a
// clip, and the order they are drawn in), and the maths of rounded rects and clips. Lengths are points in a node's
// space, x right and y down.

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

// Vulkan's swapchain pre-rotation (Player Settings: Apply display rotation during rendering): drawing to the screen of
// a device turned from its panel's own orientation, the swapchain stays in the panel's, and positions must be turned a
// quarter turn or more to match. Unity turns its own matrices; the UI draws with _UIMatrix, so it turns its positions
// here, as the render pipelines' ApplyPretransformRotation does. The engine sets the turn per target: 0 in a texture.
#if defined(SHADER_API_VULKAN) && defined(UNITY_PRETRANSFORM_TO_DISPLAY_ORIENTATION)
#   ifdef UNITY_COMPILER_DXC
        [[vk::constant_id(1)]] const int UnityDisplayOrientationPreTransform = 0;
#   else
        cbuffer UnityDisplayOrientationPreTransformData { int UnityDisplayOrientationPreTransform; };
#   endif
#endif

// A clip-space position as the target wants it: turned for a pre-rotated swapchain, else as it is.
float4 ToTarget(float4 position)
{
#if defined(SHADER_API_VULKAN) && defined(UNITY_PRETRANSFORM_TO_DISPLAY_ORIENTATION)
    if (UnityDisplayOrientationPreTransform == 1) position.xy = float2(position.y, -position.x);
    else if (UnityDisplayOrientationPreTransform == 2) position.xy = -position.xy;
    else if (UnityDisplayOrientationPreTransform == 3) position.xy = float2(-position.y, position.x);
#endif
    return position;
}

// How far inside a rounded rect a point is (negative outside), from its distances to the rect's edges (left, top,
// right, bottom) and the corners' radii (top-left, top-right, bottom-right, bottom-left, none more than half its
// shorter side): within a corner's radius of both its edges, from the corner's circle; elsewhere from its nearest edge.
float InsideRoundRect(float4 edges, float4 radii)
{
    float2 near = float2(min(edges.x, edges.z), min(edges.y, edges.w));
    float2 rx = edges.x < edges.z ? radii.xw : radii.yz;
    float r = edges.y < edges.w ? rx.x : rx.y;
    float2 q = r - near;
    return q.x > 0 && q.y > 0 ? r - length(q) : min(near.x, near.y);
}

// Signed distance to a rounded rect centred on the origin (y down), each corner its own radius: top-left, top-right,
// bottom-right, bottom-left.
float SdRoundRect(float2 p, float2 halfSize, float4 radii)
{
    float2 rx = p.x < 0 ? radii.xw : radii.yz;
    float r = p.y < 0 ? rx.x : rx.y;
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

