using UnityEngine;

public class SmallBoxVessel : VesselBase
{
    protected override void CalculateItemPlacement(int itemIndex, out Vector3 localPos, out Quaternion localRot)
    {
        // Tight clustering
        localPos = new Vector3(
            Random.Range(-volumeSize.x/2, volumeSize.x/2),
            Random.Range(-volumeSize.y/2, volumeSize.y/2),
            Random.Range(-volumeSize.z/2, volumeSize.z/2)
        );

        // Extremely strict vertical alignment (Only 2 degrees of sway)
        localRot = Quaternion.Euler(Random.Range(-2f, 2f), Random.Range(0, 360f), Random.Range(-2f, 2f));
    }
}