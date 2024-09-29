using TMPro;
using UnityEngine;
public class CharacterScript : MonoBehaviour
{
    // No need to assign this in the inspector now
    public TMP_Text activityText;

    void Awake()
    {
        // Automatically find the TextMeshPro component within this GameObject or its children
        activityText = GetComponentInChildren<TMP_Text>();

        if (activityText == null)
        {
            Debug.LogError("TextMeshPro component not found!");
        }
        else
        {
            UpdateActivity("Starting...");
        }
    }

    public void UpdateActivity(string newActivity)
    {
        if (activityText != null)
        {
            activityText.text = newActivity;
        }
    }
}