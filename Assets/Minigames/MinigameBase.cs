using UnityEngine;
using System.Collections;

public abstract class MinigameBase : MonoBehaviour
{
    [Header("Minigame Base Settings")]
    public Transform cameraSocket; 
    public float transitionSpeed = 5f;
    
    protected Player_Controls controls;
    protected bool isActive = false;
    protected Camera playerCam;
    protected PlayerController playerController;

    private Vector3 originalCamPos;
    private Quaternion originalCamRot;
    private Transform originalCamParent;

    protected virtual void Awake()
    {
        controls = new Player_Controls();
        playerCam = Camera.main;
        playerController = FindAnyObjectByType<PlayerController>();
    }

    private void OnEnable() => controls.Enable();
    private void OnDisable() => controls.Disable();

    public virtual void StartMinigame()
    {
        if (isActive) return;
        isActive = true;

        if (playerController) playerController.enabled = false;
        
        controls.Normal.Disable();
        controls.ObjectManipulation.Disable();
        controls.Minigames.Enable();

        originalCamPos = playerCam.transform.localPosition;
        originalCamRot = playerCam.transform.localRotation;
        originalCamParent = playerCam.transform.parent;

        StartCoroutine(MoveCamera(cameraSocket.position, cameraSocket.rotation, true));
        
        OnMinigameStarted();
    }

    public virtual void EndMinigame(bool success)
    {
        if (!isActive) return;
        isActive = false;

        if (playerController) playerController.enabled = true;

        controls.Minigames.Disable();
        controls.Normal.Enable();

        StartCoroutine(MoveCamera(originalCamPos, originalCamRot, false));

        OnMinigameEnded(success);
    }

    protected virtual void Update()
    {
        if (!isActive) return;

        if (controls.Minigames.Cancel.triggered)
        {
            EndMinigame(false);
        }
    }

    private IEnumerator MoveCamera(Vector3 targetPos, Quaternion targetRot, bool worldSpace)
    {
        float t = 0;
        Vector3 startPos = playerCam.transform.position;
        Quaternion startRot = playerCam.transform.rotation;

        if (!worldSpace) playerCam.transform.SetParent(originalCamParent);

        while (t < 1.0f)
        {
            t += Time.deltaTime * transitionSpeed;
            if (worldSpace)
            {
                playerCam.transform.position = Vector3.Lerp(startPos, targetPos, t);
                playerCam.transform.rotation = Quaternion.Lerp(startRot, targetRot, t);
            }
            else
            {
                playerCam.transform.localPosition = Vector3.Lerp(playerCam.transform.localPosition, targetPos, t);
                playerCam.transform.localRotation = Quaternion.Lerp(playerCam.transform.localRotation, targetRot, t);
            }
            yield return null;
        }

        if (worldSpace) playerCam.transform.SetParent(cameraSocket);
    }

    protected abstract void OnMinigameStarted();
    protected abstract void OnMinigameEnded(bool success);
}