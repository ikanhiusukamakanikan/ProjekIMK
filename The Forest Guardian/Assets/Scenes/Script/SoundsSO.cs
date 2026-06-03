using UnityEngine;

[CreateAssetMenu(menuName = "Sounds/Sound Library", fileName = "Sounds SO")]
public class SoundsSO : ScriptableObject
{
    public SoundList[] sounds;
    
    #if UNITY_EDITOR
    private void OnValidate()
    {
        if (sounds == null) return;

        int enumLength = System.Enum.GetValues(typeof(SoundType)).Length;

        if (sounds.Length != enumLength)
        {
            Debug.LogWarning($"SoundsSO count ({sounds.Length}) != SoundType count ({enumLength})");
        }

        for (int i = 0; i < sounds.Length && i < enumLength; i++)
        {
            if (sounds[i] != null)
                sounds[i].type = (SoundType)i;
        }
    }
    #endif
}