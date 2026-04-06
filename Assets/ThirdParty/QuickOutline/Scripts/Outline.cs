using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public class Outline : MonoBehaviour {
  private static HashSet<Mesh> registeredMeshes = new HashSet<Mesh>();

  public enum Mode {
    OutlineAll,
    OutlineVisible,
    OutlineHidden,
    OutlineAndSilhouette,
    SilhouetteOnly
  }

  public Mode OutlineMode {
    get { return outlineMode; }
    set {
      outlineMode = value;
      needsUpdate = true;
    }
  }

  public Color OutlineColor {
    get { return outlineColor; }
    set {
      outlineColor = value;
      needsUpdate = true;
    }
  }

  public float OutlineWidth {
    get { return outlineWidth; }
    set {
      outlineWidth = value;
      needsUpdate = true;
    }
  }

  public int CurrentStencilID { get; private set; }

  [Serializable]
  private class ListVector3 {
    public List<Vector3> data;
  }

  [SerializeField] private Mode outlineMode;
  [SerializeField] private Color outlineColor = Color.white;
  [SerializeField, Range(0f, 10f)] private float outlineWidth = 2f;

  [Header("Optional")]
  [SerializeField, Tooltip("Precompute enabled: Per-vertex calculations are performed in the editor...")]
  private bool precomputeOutline;

  [SerializeField, HideInInspector] private List<Mesh> bakeKeys = new List<Mesh>();
  [SerializeField, HideInInspector] private List<ListVector3> bakeValues = new List<ListVector3>();

  private Renderer[] renderers;
  private Material outlineMaskMaterial;
  private Material outlineFillMaterial;
  private bool needsUpdate;

  void Awake() {
    renderers = GetComponentsInChildren<Renderer>()
        .Where(r => !(r is ParticleSystemRenderer))
        .ToArray();

    // --- ROBUST MATERIAL LOADER ---
    // 1. Try Loading from Resources
    Material loadedMask = Resources.Load<Material>(@"Materials/OutlineMask");
    Material loadedFill = Resources.Load<Material>(@"Materials/OutlineFill");

    if (loadedMask == null || loadedFill == null)
    {
        // 2. Fallback: Create from Shaders directly
        Shader maskShader = Shader.Find("Custom/Outline Mask");
        Shader fillShader = Shader.Find("Custom/Outline Fill");

        if (maskShader == null || fillShader == null)
        {
            Debug.LogError("[Outline] CRITICAL: Could not find 'Custom/Outline Mask' or 'Fill' shaders. Make sure the shader files are in the project!");
            enabled = false;
            return;
        }

        outlineMaskMaterial = new Material(maskShader);
        outlineFillMaterial = new Material(fillShader);
        
        // Debug.Log("[Outline] Materials created from shaders (Resources load skipped).");
    }
    else
    {
        outlineMaskMaterial = Instantiate(loadedMask);
        outlineFillMaterial = Instantiate(loadedFill);
    }

    outlineMaskMaterial.name = "OutlineMask (Instance)";
    outlineFillMaterial.name = "OutlineFill (Instance)";

    // Generate ID
    int randomStencilId = UnityEngine.Random.Range(1, 255);
    CurrentStencilID = randomStencilId;

    outlineMaskMaterial.SetInt("_StencilRef", randomStencilId);
    outlineFillMaterial.SetInt("_StencilRef", randomStencilId);

    LoadSmoothNormals();
    needsUpdate = true;
  }

  void OnEnable() {
    foreach (var renderer in renderers) {
      var materials = renderer.sharedMaterials.ToList();
      materials.Add(outlineMaskMaterial);
      materials.Add(outlineFillMaterial);
      renderer.materials = materials.ToArray();
    }
  }

  void OnValidate() {
    needsUpdate = true;
    if (!precomputeOutline && bakeKeys.Count != 0 || bakeKeys.Count != bakeValues.Count) {
      bakeKeys.Clear();
      bakeValues.Clear();
    }
    if (precomputeOutline && bakeKeys.Count == 0) {
      Bake();
    }
  }

  void Update() {
    if (needsUpdate) {
      needsUpdate = false;
      UpdateMaterialProperties();
    }
  }

  void OnDisable() {
    foreach (var renderer in renderers) {
      var materials = renderer.sharedMaterials.ToList();
      materials.Remove(outlineMaskMaterial);
      materials.Remove(outlineFillMaterial);
      renderer.materials = materials.ToArray();
    }
  }

  void OnDestroy() {
    if (outlineMaskMaterial != null) Destroy(outlineMaskMaterial);
    if (outlineFillMaterial != null) Destroy(outlineFillMaterial);
  }

  void Bake() {
    var bakedMeshes = new HashSet<Mesh>();
    foreach (var meshFilter in GetComponentsInChildren<MeshFilter>()) {
      if (!bakedMeshes.Add(meshFilter.sharedMesh)) continue;
      var smoothNormals = SmoothNormals(meshFilter.sharedMesh);
      bakeKeys.Add(meshFilter.sharedMesh);
      bakeValues.Add(new ListVector3() { data = smoothNormals });
    }
  }

  void LoadSmoothNormals() {
    foreach (var meshFilter in GetComponentsInChildren<MeshFilter>()) {
      if (!registeredMeshes.Add(meshFilter.sharedMesh)) continue;
      var index = bakeKeys.IndexOf(meshFilter.sharedMesh);
      var smoothNormals = (index >= 0) ? bakeValues[index].data : SmoothNormals(meshFilter.sharedMesh);
      meshFilter.sharedMesh.SetUVs(3, smoothNormals);
      var renderer = meshFilter.GetComponent<Renderer>();
      if (renderer != null) CombineSubmeshes(meshFilter.sharedMesh, renderer.sharedMaterials);
    }
    foreach (var skinnedMeshRenderer in GetComponentsInChildren<SkinnedMeshRenderer>()) {
      if (!registeredMeshes.Add(skinnedMeshRenderer.sharedMesh)) continue;
      skinnedMeshRenderer.sharedMesh.uv4 = new Vector2[skinnedMeshRenderer.sharedMesh.vertexCount];
      CombineSubmeshes(skinnedMeshRenderer.sharedMesh, skinnedMeshRenderer.sharedMaterials);
    }
  }

  List<Vector3> SmoothNormals(Mesh mesh) {
    var groups = mesh.vertices.Select((vertex, index) => new KeyValuePair<Vector3, int>(vertex, index)).GroupBy(pair => pair.Key);
    var smoothNormals = new List<Vector3>(mesh.normals);
    foreach (var group in groups) {
      if (group.Count() == 1) continue;
      var smoothNormal = Vector3.zero;
      foreach (var pair in group) smoothNormal += smoothNormals[pair.Value];
      smoothNormal.Normalize();
      foreach (var pair in group) smoothNormals[pair.Value] = smoothNormal;
    }
    return smoothNormals;
  }

  void CombineSubmeshes(Mesh mesh, Material[] materials) {
    if (mesh.subMeshCount == 1) return;
    if (mesh.subMeshCount > materials.Length) return;
    mesh.subMeshCount++;
    mesh.SetTriangles(mesh.triangles, mesh.subMeshCount - 1);
  }

  void UpdateMaterialProperties() {
    if(outlineFillMaterial == null) return;
    outlineFillMaterial.SetColor("_OutlineColor", outlineColor);

    switch (outlineMode) {
      case Mode.OutlineAll:
        outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);
        break;
      case Mode.OutlineVisible:
        outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
        outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);
        break;
      case Mode.OutlineHidden:
        outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Greater);
        outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);
        break;
      case Mode.OutlineAndSilhouette:
        outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
        outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
        outlineFillMaterial.SetFloat("_OutlineWidth", outlineWidth);
        break;
      case Mode.SilhouetteOnly:
        outlineMaskMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.LessEqual);
        outlineFillMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Greater);
        outlineFillMaterial.SetFloat("_OutlineWidth", 0f);
        break;
    }
  }

// Changed parameter name to 'value' to indicate it's the exact queue, not an offset
// Add this variable right above the method
  private static int renderOrderOffset = 0;

  public void SetRenderQueue(int value) {
    if (outlineMaskMaterial != null && outlineFillMaterial != null) {
      
      // Increment by 2 for every new object highlighted.
      // This ensures Object A (3100, 3101) finishes completely before Object B (3102, 3103) starts!
      renderOrderOffset += 2;
      
      // Reset before we hit the UI render queues (which start at 3500)
      if (renderOrderOffset > 300) renderOrderOffset = 0; 

      outlineMaskMaterial.renderQueue = value + renderOrderOffset;
      outlineFillMaterial.renderQueue = value + renderOrderOffset + 1; 
    }
  }
}