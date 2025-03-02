using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DayNightCycle : MonoBehaviour
{
    public Light sunLight; // Assign your Directional Light in the Inspector
    public OpenAIController timeManager; // Reference to the script managing time

    public Color morningColor = new Color(1.0f, 0.5f, 0.3f); // Warm sunrise color
    public Color dayColor = Color.white; // Bright daylight
    public Color eveningColor = new Color(1.0f, 0.5f, 0.3f); // Sunset color
    public Color nightColor = new Color(0.1f, 0.1f, 0.3f); // Dark blue night

    void Start()
    {
        // Find the OpenAIController script in the scene
        timeManager = FindObjectOfType<OpenAIController>();
    }

    void Update()
    {
        if (timeManager == null) return; // Prevent errors if the script isn't found

        int time = timeManager.time_of_day; // Get the current time from OpenAIController

        RotateSun(time);
        AdjustLighting(time);
    }

    void RotateSun(int time)
    {
        float normalizedTime = (time % 2400) / 2400f; // Convert time to 0-1 range
        float sunAngle = Mathf.Lerp(-90, 270, normalizedTime); // Map to -90° to 270°

        sunLight.transform.rotation = Quaternion.Euler(sunAngle, 0, 0);
    }

    void AdjustLighting(int time)
    {
        float normalizedTime = (time % 2400) / 2400f;

        // Adjust intensity and ambient lighting
        if (time < 600 || time > 1800) // Nighttime
        {
            sunLight.intensity = Mathf.Lerp(0.2f, 0.0f, Mathf.InverseLerp(1800, 2400, time) + Mathf.InverseLerp(0, 600, time));
            RenderSettings.ambientIntensity = 0.2f;
            sunLight.color = Color.Lerp(eveningColor, nightColor, normalizedTime);
        }
        else // Daytime
        {
            sunLight.intensity = Mathf.Lerp(0.2f, 1.0f, Mathf.InverseLerp(600, 1200, time));
            RenderSettings.ambientIntensity = Mathf.Lerp(0.2f, 1.0f, Mathf.InverseLerp(600, 1200, time));

            if (time < 900) // Morning transition
                sunLight.color = Color.Lerp(morningColor, dayColor, Mathf.InverseLerp(600, 900, time));
            else if (time > 1500) // Evening transition
                sunLight.color = Color.Lerp(dayColor, eveningColor, Mathf.InverseLerp(1500, 1800, time));
            else // Full daylight
                sunLight.color = dayColor;
        }
    }
}
