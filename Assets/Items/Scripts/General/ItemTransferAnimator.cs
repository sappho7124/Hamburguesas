// Location: C:\Games\Unity\Hamburguesas\Assets\Items\Scripts\ItemTransferAnimator.cs
using UnityEngine;
using System.Collections;

public class ItemTransferAnimator : MonoBehaviour
{
    public void StartTransfer(Transform sourceFunnel, Vector2 sourceFunnelSize, 
                              Transform targetFunnel, Vector2 targetFunnelSize, 
                              Transform targetVolume, Vector3 finalLocalPos, Quaternion finalLocalRot)
    {
        StartCoroutine(TransferRoutine(sourceFunnel, sourceFunnelSize, targetFunnel, targetFunnelSize, targetVolume, finalLocalPos, finalLocalRot));
    }

    private IEnumerator TransferRoutine(Transform sourceFunnel, Vector2 sourceFunnelSize, 
                                        Transform targetFunnel, Vector2 targetFunnelSize, 
                                        Transform targetVolume, Vector3 finalLocalPos, Quaternion finalLocalRot)
    {
        Vector3 currentPos = transform.position;
        Quaternion startRot = transform.rotation;
        
        int phases = 1 + (sourceFunnel != null ? 1 : 0) + (targetFunnel != null ? 1 : 0);
        float timePerPhase = 0.2f / phases;

        // PHASE 1: Exit Source Funnel (Path of Least Effort)
        if (sourceFunnel != null)
        {
            Vector3 localDropPos = sourceFunnel.InverseTransformPoint(currentPos);
            localDropPos.x = Mathf.Clamp(localDropPos.x, -sourceFunnelSize.x / 2f, sourceFunnelSize.x / 2f);
            localDropPos.y = 0f; 
            localDropPos.z = Mathf.Clamp(localDropPos.z, -sourceFunnelSize.y / 2f, sourceFunnelSize.y / 2f);

            float elapsed = 0f;
            while (elapsed < timePerPhase)
            {
                elapsed += Time.deltaTime;
                Vector3 dynamicEntryPoint = sourceFunnel.TransformPoint(localDropPos);
                transform.position = Vector3.Lerp(currentPos, dynamicEntryPoint, (elapsed / timePerPhase) * (elapsed / timePerPhase)); // Ease-in
                yield return null;
            }
            currentPos = sourceFunnel.TransformPoint(localDropPos);
        }

        // PHASE 2: Enter Target Funnel (Path of Least Effort)
        if (targetFunnel != null)
        {
            Vector3 localDropPos = targetFunnel.InverseTransformPoint(currentPos);
            localDropPos.x = Mathf.Clamp(localDropPos.x, -targetFunnelSize.x / 2f, targetFunnelSize.x / 2f);
            localDropPos.y = 0f; 
            localDropPos.z = Mathf.Clamp(localDropPos.z, -targetFunnelSize.y / 2f, targetFunnelSize.y / 2f);

            float elapsed = 0f;
            while (elapsed < timePerPhase)
            {
                elapsed += Time.deltaTime;
                Vector3 dynamicEntryPoint = targetFunnel.TransformPoint(localDropPos);
                transform.position = Vector3.Lerp(currentPos, dynamicEntryPoint, elapsed / timePerPhase); // Linear flight
                yield return null;
            }
            currentPos = targetFunnel.TransformPoint(localDropPos);
        }

        // PHASE 3: Drop into Target Belly
        if (targetVolume != null)
        {
            float elapsed = 0f;
            while (elapsed < timePerPhase)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / timePerPhase;
                
                Vector3 finalWorldPos = targetVolume.TransformPoint(finalLocalPos);
                Quaternion finalWorldRot = targetVolume.rotation * finalLocalRot;

                transform.position = Vector3.Lerp(currentPos, finalWorldPos, t);
                transform.rotation = Quaternion.Slerp(startRot, finalWorldRot, t);
                yield return null;
            }

            // SNAP TO FINAL PARENT & POS
            transform.SetParent(targetVolume);
            transform.localPosition = finalLocalPos;
            transform.localRotation = finalLocalRot;
        }

        // Clean up the animator, it's done its job!
        Destroy(this);
    }
}