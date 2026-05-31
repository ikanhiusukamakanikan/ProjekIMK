using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public const string MasterVolumePrefKey = "Settings.MasterVolumeStep";

    [Header("Mixer")]
    public AudioMixer audioMixer;
    public string masterVolumeParameter = "MasterVolume";

    [Header("Master Volume")]
    [Range(0, 4)] public int defaultMasterVolumeStep = 4;
    public float mutedDecibel = -80f;
    public float quarterDecibel = -20f;
    public float halfDecibel = -10f;
    public float threeQuarterDecibel = -4f;
    public float maxDecibel = 0f;
    public bool dontDestroyOnLoad = true;

    public int MasterVolumeStep { get; private set; }
    public float MasterVolumeNormalized => MasterVolumeStep / 4f;
    public int MasterVolumePercent => MasterVolumeStep * 25;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        LoadSettings();
    }

    public void LoadSettings()
    {
        int savedStep = PlayerPrefs.GetInt(
            MasterVolumePrefKey,
            Mathf.Clamp(defaultMasterVolumeStep, 0, 4)
        );

        SetMasterVolumeStep(savedStep, false);
    }

    public void SetMasterVolumeStep(float step)
    {
        SetMasterVolumeStep(Mathf.RoundToInt(step), true);
    }

    public void SetMasterVolumeStep(int step, bool save = true)
    {
        MasterVolumeStep = Mathf.Clamp(step, 0, 4);
        ApplyMasterVolume();

        if (save)
        {
            PlayerPrefs.SetInt(MasterVolumePrefKey, MasterVolumeStep);
            PlayerPrefs.Save();
        }
    }

    public void SetMasterVolumeNormalized(float normalizedVolume)
    {
        SetMasterVolumeStep(Mathf.RoundToInt(Mathf.Clamp01(normalizedVolume) * 4f), true);
    }

    private void ApplyMasterVolume()
    {
        float normalizedVolume = MasterVolumeNormalized;
        AudioListener.volume = normalizedVolume;

        if (audioMixer == null || string.IsNullOrWhiteSpace(masterVolumeParameter))
        {
            return;
        }

        float decibel = NormalizedToDecibel(normalizedVolume);
        audioMixer.SetFloat(masterVolumeParameter, decibel);
    }

    private float NormalizedToDecibel(float normalizedVolume)
    {
        if (normalizedVolume <= 0f)
        {
            return mutedDecibel;
        }

        if (normalizedVolume <= 0.25f)
        {
            return Mathf.Lerp(mutedDecibel, quarterDecibel, normalizedVolume / 0.25f);
        }

        if (normalizedVolume <= 0.5f)
        {
            return Mathf.Lerp(quarterDecibel, halfDecibel, (normalizedVolume - 0.25f) / 0.25f);
        }

        if (normalizedVolume <= 0.75f)
        {
            return Mathf.Lerp(halfDecibel, threeQuarterDecibel, (normalizedVolume - 0.5f) / 0.25f);
        }

        return Mathf.Lerp(threeQuarterDecibel, maxDecibel, (normalizedVolume - 0.75f) / 0.25f);
    }
}
