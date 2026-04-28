using UnityEngine;

public class FryerBasketVessel : VesselBase
{
    protected override void CalculateItemPlacement(int itemIndex, out Vector3 localPos, out Quaternion localRot)
    {
        // Calculate fill height based on how many fries are in the basket
        // Ensures the first fries drop to the bottom, and later fries stack on top!
        float heightPct = (float)itemIndex / maxCapacity;
        float yPos = Mathf.Lerp(-volumeSize.y/2, volumeSize.y/2, heightPct);

        localPos = new Vector3(
            Random.Range(-volumeSize.x/2, volumeSize.x/2),
            yPos, 
            Random.Range(-volumeSize.z/2, volumeSize.z/2)
        );

        // 90 degrees on X turns them horizontal, random Y yaw so they form a messy horizontal pile
        localRot = Quaternion.Euler(90f, Random.Range(0, 360f), 0f);
    }
}