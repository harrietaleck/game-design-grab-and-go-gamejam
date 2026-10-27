using UnityEngine;

/// <summary>
/// AJ's voice and dialogue limits (Task 4.2). Create one via Assets > Create > Grab & Go > AJ Personality,
/// or leave unassigned to use these defaults.
/// </summary>
[CreateAssetMenu(menuName = "Grab & Go/AJ Personality", fileName = "AJPersonality")]
public class AJPersonalityProfile : ScriptableObject
{
    public string characterName = "AJ";

    [TextArea(4, 10)]
    public string personality =
        "AJ is the thief who just pulled the heist and is sprinting through the city. " +
        "Confident, cocky, street-smart and quick. Funny and sarcastic, a bit dramatic. " +
        "Gets audibly nervous when danger is real. Thinks he's the star of the operation, " +
        "but relies completely on Claire, his getaway partner, and secretly knows it. " +
        "Hides gratitude behind jokes.";

    [TextArea(3, 8)]
    public string speakingStyle =
        "Short radio lines, one or two sentences. Casual street slang, no swearing. " +
        "Talks to Claire directly. Never narrates, never explains game rules, no catchphrase repetition.";

    [Tooltip("Lines longer than this are rejected (LLM) or never written (fallback).")]
    public int maxLineCharacters = 120;

    [Tooltip("Maximum characters of persona text sent to the dialogue backend.")]
    public int maxPersonaCharacters = 600;

    public string PersonaForRequest()
    {
        string text = (personality + " " + speakingStyle).Trim();
        return text.Length <= maxPersonaCharacters ? text : text.Substring(0, maxPersonaCharacters);
    }

    private static AJPersonalityProfile s_default;
    public static AJPersonalityProfile Default
    {
        get
        {
            if (s_default == null)
            {
                s_default = CreateInstance<AJPersonalityProfile>();
                s_default.name = "AJPersonality (default)";
                s_default.hideFlags = HideFlags.DontSave;
            }
            return s_default;
        }
    }
}
