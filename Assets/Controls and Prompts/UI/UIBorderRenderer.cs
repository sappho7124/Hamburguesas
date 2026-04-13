using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class UIBorderRenderer : MaskableGraphic
{
    [SerializeField]
    private float m_Thickness = 2f; // Backing field for inspector

    [Header("Border Settings")]
    public bool drawCenter = false;

    // Public Property that triggers update
    public float Thickness
    {
        get { return m_Thickness; }
        set
        {
            if (m_Thickness != value)
            {
                m_Thickness = value;
                SetVerticesDirty(); // <--- Forces a redraw immediately
            }
        }
    }

    // Allow Inspector changes to update in Editor
    #if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            SetVerticesDirty(); 
        }
    #endif

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        float width = rectTransform.rect.width;
        float height = rectTransform.rect.height;

        Vector2 pivot = rectTransform.pivot;
        
        float xMin = -width * pivot.x;
        float xMax = width * (1 - pivot.x);
        float yMin = -height * pivot.y;
        float yMax = height * (1 - pivot.y);

        // Use the property backing field
        float xMinInner = xMin + m_Thickness;
        float xMaxInner = xMax - m_Thickness;
        float yMinInner = yMin + m_Thickness;
        float yMaxInner = yMax - m_Thickness;

        UIVertex vert = UIVertex.simpleVert;
        vert.color = color;

        // 1. Top
        AddQuad(vh, 
            new Vector2(xMin, yMaxInner), new Vector2(xMax, yMaxInner), 
            new Vector2(xMax, yMax),      new Vector2(xMin, yMax),      
            vert);

        // 2. Bottom
        AddQuad(vh, 
            new Vector2(xMin, yMin),      new Vector2(xMax, yMin), 
            new Vector2(xMax, yMinInner), new Vector2(xMin, yMinInner), 
            vert);

        // 3. Left
        AddQuad(vh, 
            new Vector2(xMin, yMinInner),      new Vector2(xMinInner, yMinInner), 
            new Vector2(xMinInner, yMaxInner), new Vector2(xMin, yMaxInner), 
            vert);

        // 4. Right
        AddQuad(vh, 
            new Vector2(xMaxInner, yMinInner), new Vector2(xMax, yMinInner), 
            new Vector2(xMax, yMaxInner),      new Vector2(xMaxInner, yMaxInner), 
            vert);

        if (drawCenter)
        {
            AddQuad(vh, 
                new Vector2(xMinInner, yMinInner), new Vector2(xMaxInner, yMinInner), 
                new Vector2(xMaxInner, yMaxInner), new Vector2(xMinInner, yMaxInner), 
                vert);
        }
    }

    private void AddQuad(VertexHelper vh, Vector2 v1, Vector2 v2, Vector2 v3, Vector2 v4, UIVertex vert)
    {
        int i = vh.currentVertCount;
        vert.position = v1; vh.AddVert(vert);
        vert.position = v2; vh.AddVert(vert);
        vert.position = v3; vh.AddVert(vert);
        vert.position = v4; vh.AddVert(vert);
        vh.AddTriangle(i + 0, i + 1, i + 2);
        vh.AddTriangle(i + 2, i + 3, i + 0);
    }
}