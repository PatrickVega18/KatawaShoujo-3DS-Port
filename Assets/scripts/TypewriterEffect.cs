using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class TypewriterEffect : BaseMeshEffect
{
    private int _visibleCount = int.MaxValue;
    private int _totalCount = 0;

    public int visibleCount
    {
        get { return _visibleCount; }
        set
        {
            if (_visibleCount != value)
            {
                _visibleCount = value;
                if (graphic != null) graphic.SetVerticesDirty();
            }
        }
    }

    public int totalCount { get { return _totalCount; } }

    public void ShowAll()
    {
        _visibleCount = int.MaxValue;
        if (graphic != null) graphic.SetVerticesDirty();
    }

    public void HideAll()
    {
        _visibleCount = 0;
        if (graphic != null) graphic.SetVerticesDirty();
    }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive()) return;

        List<UIVertex> verts = new List<UIVertex>();
        vh.GetUIVertexStream(verts);

        // Cada carácter visible = 6 vértices (2 triángulos = 1 quad)
        _totalCount = verts.Count / 6;

        int safeVisible = (_visibleCount > _totalCount) ? _totalCount : _visibleCount;
        int hideFrom = safeVisible * 6;

        for (int i = hideFrom; i < verts.Count; i++)
        {
            UIVertex v = verts[i];
            Color32 c = v.color;
            c.a = 0;
            v.color = c;
            verts[i] = v;
        }

        vh.Clear();
        vh.AddUIVertexTriangleStream(verts);
    }
}
