using UnityEngine;
using UnityEngine.UI;

namespace PlayStories
{
    /// <summary>
    /// Bends a full-screen graphic around a virtual cylinder, like a sheet of paper being peeled off.
    /// Three instances share the same curl state:
    ///   Front  – the page snapshot (part still facing the viewer)
    ///   Back   – the paper's reverse side (part that has flipped over), drawn on top
    ///   Shadow – soft shadow cast on the page underneath
    ///
    /// Maths: the curl axis is the line dot(P, n) = c. Points with d = dot(P,n) - c &lt;= 0 lie flat.
    /// Points with 0 &lt; d &lt; πr wrap around the cylinder (projected: d' = r·sin(d/r)),
    /// points beyond πr have flipped over and lie on top at d' = -(d - πr).
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class PageCurlMesh : BaseMeshEffect
    {
        public enum Part { Front, Back, Shadow }

        public Part part = Part.Front;
        [System.NonSerialized] public PageCurlState state;
        public int columns = 48, rows = 30;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || state == null) return;
            Rect rect = graphic.rectTransform.rect;
            Color32 baseColor = graphic.color;

            // grab UV corners from the original quad (RawImage uses uvRect)
            Vector4 uvMin = Vector4.zero, uvMax = Vector4.one;
            if (vh.currentVertCount >= 4)
            {
                var v0 = new UIVertex(); var v2 = new UIVertex();
                vh.PopulateUIVertex(ref v0, 0);
                vh.PopulateUIVertex(ref v2, 2);
                uvMin = v0.uv0; uvMax = v2.uv0;
            }
            vh.Clear();

            if (part == Part.Shadow) { BuildShadow(vh, rect); return; }

            Vector2 n = state.normal;
            float c = state.position, r = Mathf.Max(1f, state.radius);
            float halfPi = Mathf.PI * 0.5f;
            int stride = columns + 1;

            // vertices
            for (int y = 0; y <= rows; y++)
            {
                float v = y / (float)rows;
                for (int x = 0; x <= columns; x++)
                {
                    float u = x / (float)columns;
                    Vector2 p = new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, u), Mathf.Lerp(rect.yMin, rect.yMax, v));
                    float d = Vector2.Dot(p, n) - c;
                    Vector2 q = p;
                    float shade = 1f;
                    if (d > 0f)
                    {
                        float theta = d / r;
                        float dNew;
                        if (theta < Mathf.PI)
                        {
                            dNew = r * Mathf.Sin(theta);
                            // front side darkens towards the top of the roll, back side lightens away from it
                            shade = theta < halfPi ? Mathf.Lerp(1f, 0.72f, theta / halfPi) : Mathf.Lerp(0.8f, 1f, (theta - halfPi) / halfPi);
                        }
                        else
                        {
                            dNew = -(d - Mathf.PI * r);
                            shade = 1f;
                        }
                        q = p - n * d + n * dNew;
                    }
                    var vert = UIVertex.simpleVert;
                    vert.position = q;
                    byte s = (byte)(255 * Mathf.Clamp01(shade));
                    vert.color = new Color32((byte)(baseColor.r * s / 255), (byte)(baseColor.g * s / 255), (byte)(baseColor.b * s / 255), baseColor.a);
                    vert.uv0 = new Vector4(Mathf.Lerp(uvMin.x, uvMax.x, u), Mathf.Lerp(uvMin.y, uvMax.y, v), 0, 0);
                    vh.AddVert(vert);
                }
            }

            // triangles: Front = quads not yet past the top of the roll, Back = quads past it.
            // Emitted in order of increasing d so later (more curled) quads draw on top.
            float split = halfPi * r;
            for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
            {
                float u = (x + 0.5f) / columns, v = (y + 0.5f) / rows;
                Vector2 p = new Vector2(Mathf.Lerp(rect.xMin, rect.xMax, u), Mathf.Lerp(rect.yMin, rect.yMax, v));
                float d = Vector2.Dot(p, n) - c;
                bool isBack = d > split;
                if ((part == Part.Back) != isBack) continue;
                int i0 = y * stride + x, i1 = i0 + stride, i2 = i1 + 1, i3 = i0 + 1;
                vh.AddTriangle(i0, i1, i2);
                vh.AddTriangle(i2, i3, i0);
            }
        }

        void BuildShadow(VertexHelper vh, Rect rect)
        {
            // A long strip along the curl axis, fading out over the revealed (next) page.
            Vector2 n = state.normal, t = new Vector2(-n.y, n.x);
            float L = (rect.width + rect.height) * 2f;
            Vector2 baseP = n * state.position;
            float w0 = 0f, w1 = state.radius * 1.15f + 90f;
            float alpha = state.shadowStrength;
            if (alpha <= 0.001f) return;
            var c0 = new Color32(0, 0, 0, (byte)(255 * alpha));
            var c1 = new Color32(0, 0, 0, 0);
            AddV(vh, baseP - t * L + n * w0, c0);
            AddV(vh, baseP + t * L + n * w0, c0);
            AddV(vh, baseP + t * L + n * w1, c1);
            AddV(vh, baseP - t * L + n * w1, c1);
            vh.AddTriangle(0, 1, 2);
            vh.AddTriangle(2, 3, 0);
            // a softer, short shadow cast back onto the old page by the flipped-over part
            float w2 = -60f;
            var c2 = new Color32(0, 0, 0, (byte)(120 * alpha));
            Vector2 back = n * (state.position - Mathf.Max(0f, state.flippedLength));
            AddV(vh, back - t * L, c2);
            AddV(vh, back + t * L, c2);
            AddV(vh, back + t * L + n * w2, c1);
            AddV(vh, back - t * L + n * w2, c1);
            vh.AddTriangle(4, 5, 6);
            vh.AddTriangle(6, 7, 4);
        }

        static void AddV(VertexHelper vh, Vector2 p, Color32 c)
        {
            var v = UIVertex.simpleVert;
            v.position = p;
            v.color = c;
            vh.AddVert(v);
        }
    }

    /// <summary>Shared, animatable state of one page curl.</summary>
    public class PageCurlState
    {
        public Vector2 normal = new Vector2(1f, 0f);
        public float position;        // c in dot(P,n) = c
        public float radius = 120f;
        public float shadowStrength = 0.35f;
        public float flippedLength;   // how much paper lies flipped over (for the back shadow)
    }
}
