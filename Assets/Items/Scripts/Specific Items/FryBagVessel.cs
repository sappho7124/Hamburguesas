using UnityEngine;

public class FryBagVessel : VesselBase
{
    [Header("Bag Visuals")]
    public SkinnedMeshRenderer bagMesh;
    public int shrinkBlendShapeIndex = 0;
    public int openBlendShapeIndex = 1;
    public float blendShapeSpeed = 10f;

    private float targetOpenWeight = 0f;
    private float currentOpenWeight = 0f;

    protected override void Update()
    {
        base.Update(); 

        if (bagMesh != null)
        {
            float targetShrink = (1f - ((float)currentAmount / maxCapacity)) * 100f;
            float currentShrink = bagMesh.GetBlendShapeWeight(shrinkBlendShapeIndex);
            bagMesh.SetBlendShapeWeight(shrinkBlendShapeIndex, Mathf.Lerp(currentShrink, targetShrink, Time.deltaTime * blendShapeSpeed));

            currentOpenWeight = Mathf.Lerp(currentOpenWeight, targetOpenWeight, Time.deltaTime * blendShapeSpeed);
            bagMesh.SetBlendShapeWeight(openBlendShapeIndex, currentOpenWeight);
        }
    }

    public override void SetOpenState(bool isOpen) => targetOpenWeight = isOpen ? 100f : 0f;

    protected override void CalculateItemPlacement(int itemIndex, out Vector3 localPos, out Quaternion localRot)
    {
        // Store vertically (mostly standing up), slight random tilt so they don't look perfectly identical
        localPos = new Vector3(
            Random.Range(-volumeSize.x/2, volumeSize.x/2),
            Random.Range(-volumeSize.y/2, volumeSize.y/2),
            Random.Range(-volumeSize.z/2, volumeSize.z/2)
        );

        // Assume default fry orientation is Y-up. Add tiny 5-degree sway.
        localRot = Quaternion.Euler(Random.Range(-5f, 5f), Random.Range(0, 360f), Random.Range(-5f, 5f));
    }
}