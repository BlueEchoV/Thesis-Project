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
    public Color nightColor = new Color(0.5f, 0.5f, 1.0f); // Bright moonlight blue
    // 90.0f is straight down
    public float angle = 0.0f;

    void Start()
    {
        // Find the OpenAIController script in the scene
        timeManager = FindObjectOfType<OpenAIController>();
    }

    void Update()
    {
        if (timeManager == null) return; // Prevent errors if the script isn't found

        int time = timeManager.time_of_day; // Get the current time from OpenAIController

        angle = 90.0f;
        sunLight.transform.rotation = Quaternion.Euler(angle, 0, 0);
        // RotateSun(time);
        AdjustLighting(time);
    }

    void RotateSun(int time)
    {
        float normalizedTime = (time % 2400) / 2400f; // Convert time to 0-1 range
        float sunAngle;

        if (time < 600 || time > 1800) // Nighttime (Sun acts as the Moon)
        {
            sunAngle = 45f; // Keep light at 45 degrees during night
        }
        else // Daytime (Sun Movement)
        {
            sunAngle = Mathf.Lerp(45f, -45f, Mathf.InverseLerp(600, 1800, time)); // Moves from 45° to -45°
        }

        sunLight.transform.rotation = Quaternion.Euler(sunAngle, 0, 0);
    }

    void AdjustLighting(int time)
    {
        float normalizedTime = (time % 2400) / 2400f;

        // Adjust lighting for a bright night with moonlight
        if (time < 600 || time > 1800) // Nighttime (Moonlight)
        {
            sunLight.intensity = 1.0f; // Just as bright as daytime
            RenderSettings.ambientIntensity = 1.0f; // Keep ambient light bright
            sunLight.color = nightColor; // Set moonlight color
        }
        else // Daytime (Sunlight)
        {
            sunLight.intensity = 1.0f; // Keep same brightness
            RenderSettings.ambientIntensity = 1.0f;
            
            if (time < 900) // Morning transition
                sunLight.color = Color.Lerp(morningColor, dayColor, Mathf.InverseLerp(600, 900, time));
            else if (time > 1500) // Evening transition
                sunLight.color = Color.Lerp(dayColor, eveningColor, Mathf.InverseLerp(1500, 1800, time));
            else // Full daylight
                sunLight.color = dayColor;
        }
    }
}
