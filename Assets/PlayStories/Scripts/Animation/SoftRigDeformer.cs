using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PlayStories
{
    /// <summary>
    /// Replaces an Image's quad with a subdivided grid and bends every vertex through
    /// <see cref="CharacterActor.Deform"/>. This is what lets a single flat drawing breathe,
    /// squash, lean, nod its head and wag its tail – no extra art, no skeleton setup.
    /// The body and its blink overlay both use this with the same actor, so they stay glued together.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class SoftRigDeformer : BaseMeshEffect
    {
        public CharacterActor actor;
        [Range(2, 40)] public int columns = 18;
        [Range(2, 40)] public int rows = 18;

        static readonly List<UIVertex> s_Quad = new List<UIVertex>(4);

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || actor == null || vh.currentVertCount != 4) return;

            s_Quad.Clear();
            for (int i = 0; i < 4; i++)
            {
                var v = new UIVertex();
                vh.PopulateUIVertex(ref v, i);
                s_Quad.Add(v);
            }
            // Image.Simple order: 0 = bottom-left, 1 = top-left, 2 = top-right, 3 = bottom-right
            Vector3 pMin = s_Quad[0].position, pMax = s_Quad[2].position;
            Vector4 uvMin = s_Quad[0].uv0, uvMax = s_Quad[2].uv0;
            Color32 col = s_Quad[0].color;
            Rect rect = graphic.rectTransform.rect;

            vh.Clear();
            for (int y = 0; y <= rows; y++)
            {
                float v = y / (float)rows;
                for (int x = 0; x <= columns; x++)
                {
                    float u = x / (float)columns;
                    Vector2 flat = new Vector2(Mathf.Lerp(pMin.x, pMax.x, u), Mathf.Lerp(pMin.y, pMax.y, v));
                    // normalised position inside the rect (sprite space when the image fills its rect)
                    float nu = rect.width > 0 ? (flat.x - rect.xMin) / rect.width : u;
                    float nv = rect.height > 0 ? (flat.y - rect.yMin) / rect.height : v;
                    Vector2 p = actor.Deform(nu, nv, rect);
                    var vert = UIVertex.simpleVert;
                    vert.position = new Vector3(p.x, p.y, 0f);
                    vert.color = col;
                    vert.uv0 = new Vector4(Mathf.Lerp(uvMin.x, uvMax.x, u), Mathf.Lerp(uvMin.y, uvMax.y, v), 0f, 0f);
                    vh.AddVert(vert);
                }
            }
            int stride = columns + 1;
            for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
            {
                int i0 = y * stride + x, i1 = i0 + stride, i2 = i1 + 1, i3 = i0 + 1;
                vh.AddTriangle(i0, i1, i2);
                vh.AddTriangle(i2, i3, i0);
            }
        }
    }
}
