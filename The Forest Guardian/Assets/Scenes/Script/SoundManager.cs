using System;
using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance;

    [SerializeField] private SoundsSO soundsSO;
    [SerializeField] private AudioMixerGroup masterMixer;
    [SerializeField] private float sameSoundWindow = 0.08f;
    [SerializeField] private int maxSameSoundInWindow = 3;
    [SerializeField, Range(0f, 1f)] private float stackedSoundVolumeMultiplier = 0.7f;

    private AudioSource audioSource;

    private Dictionary<SoundType, SoundList> soundDict;
    private readonly Dictionary<SoundType, List<float>> recentSoundTimes = new();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();
        audioSource.outputAudioMixerGroup = masterMixer;

        soundDict = new Dictionary<SoundType, SoundList>();
        if (soundsSO != null)
        {
            foreach (var s in soundsSO.sounds)
                soundDict[s.type] = s;
        }
    }


    public static void PlaySound(SoundType sound, AudioSource source = null, float volume = 1f)
    {
        if (Instance == null || Instance.soundDict == null) return;

        if (!Instance.TryGetPlayableSound(sound, out var data)) return;

        if (data.sounds == null || data.sounds.Length == 0) return;

        if (!Instance.TryRegisterSound(sound, data, out int stackedCount)) return;

        AudioClip clip = data.sounds[UnityEngine.Random.Range(0, data.sounds.Length)];
        float stackedMultiplier = Mathf.Pow(
            Instance.stackedSoundVolumeMultiplier,
            stackedCount);
        float finalVolume = volume * data.volume * stackedMultiplier;

        if (source != null)
        {
            source.pitch = UnityEngine.Random.Range(0.90f, 1.1f);
            source.outputAudioMixerGroup = Instance.masterMixer;
            source.PlayOneShot(clip, finalVolume);
        }
        else
        {
            Instance.audioSource.pitch = UnityEngine.Random.Range(0.90f, 1.1f);
            Instance.audioSource.PlayOneShot(clip, finalVolume);
        }

    }

    private bool TryRegisterSound(
        SoundType sound,
        SoundList data,
        out int stackedCount)
    {
        float now = Time.unscaledTime;
        float window = data.sameSoundWindow > 0f
            ? data.sameSoundWindow
            : sameSoundWindow;
        int maxInWindow = data.maxSameSoundInWindow > 0
            ? data.maxSameSoundInWindow
            : maxSameSoundInWindow;

        if (!recentSoundTimes.TryGetValue(sound, out List<float> times))
        {
            times = new List<float>();
            recentSoundTimes[sound] = times;
        }

        for (int i = times.Count - 1; i >= 0; i--)
        {
            if (now - times[i] > window)
            {
                times.RemoveAt(i);
            }
        }

        stackedCount = times.Count;

        if (times.Count >= Mathf.Max(1, maxInWindow))
        {
            return false;
        }

        times.Add(now);
        return true;
    }

    private bool TryGetPlayableSound(SoundType sound, out SoundList data)
    {
        data = null;

        if (!soundDict.TryGetValue(sound, out SoundList requestedData))
        {
            return false;
        }

        if (requestedData.sounds != null && requestedData.sounds.Length > 0)
        {
            data = requestedData;
            return true;
        }

        if (sound == SoundType.Click &&
            soundDict.TryGetValue(SoundType.Hover, out SoundList hoverData) &&
            hoverData.sounds != null &&
            hoverData.sounds.Length > 0)
        {
            data = hoverData;
            return true;
        }

        return false;
    }
}


[Serializable]
public class SoundList
{
    public SoundType type;
    [Range(0,1)] public float volume = 1f;
    [Min(0f)] public float sameSoundWindow = 0.08f;
    [Min(1)] public int maxSameSoundInWindow = 3;
    public AudioClip[] sounds;
}


public enum SoundType
{
    Hover,
    Click,
    UIPopup,
    UIClose,
    ItemSummon,
    Pickup,
    PlayerStep,
    ScannerBeep,
    ScannerError,
    AxeChop,
    TreeFall,
    ShovelDig,
    Planting,
    FireExtinguish, 
    Win,
    Lose,
    Warning

}