using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Profiling;

public class DebugStats : MonoBehaviour
{
    public Text fpsText;
    public Text ramText;

    private float deltaSum = 0f;
    private int frameCount = 0;
    private float updateInterval = 0.5f;
    private float nextUpdate = 0f;

    void Update()
    {
        deltaSum += Time.unscaledDeltaTime;
        frameCount++;

        if (Time.unscaledTime >= nextUpdate)
        {
            float fps = frameCount / deltaSum;
            if (fpsText != null) fpsText.text = fps.ToString("F2");

            if (ramText != null)
            {
                float managedMB = System.GC.GetTotalMemory(false) / 1048576f;
                ramText.text = managedMB.ToString("F2") + " MB";
            }

            deltaSum = 0f;
            frameCount = 0;
            nextUpdate = Time.unscaledTime + updateInterval;
        }
    }
}
