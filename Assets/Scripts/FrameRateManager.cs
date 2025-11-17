using UnityEngine;

public class FrameRateManager : MonoBehaviour
{
    [Header("Target Frame Rate")]
    public int targetFPS = 60;

    void Start()
    {
        Application.targetFrameRate = targetFPS;
        QualitySettings.vSyncCount = 0;   // Must disable VSync on mobile
    }

    void Update()
    {
        if (Application.targetFrameRate != targetFPS)
            Application.targetFrameRate = targetFPS;
    }
}
