using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class FireLoopFaderPro : MonoBehaviour
{
    [Header("Refs")]
    public Transform player;                // Auto-fills from tag "Player" if null
    public AudioClip fireLoop;

    [Header("Distances")]
    public float fullVolumeRadius = 2.0f;
    public float fadeOutRadius = 16.0f;
    public float stopRadius = 18.0f;

    [Header("Envelope (seconds)")]
    public float attack = 0.25f;
    public float release = 0.35f;
    public float lingerAfterExit = 0.60f;

    [Header("Levels")]
    [Range(0f, 1f)] public float baseVolume = 0.75f;
    [Range(0f, 1f)] public float distanceCurveBias = 0.35f;
    public float minAudible = 0.001f;

    AudioSource src;
    float currentVol = 0f;
    float targetVol = 0f;
    float vel = 0f;
    float lingerTimer = 0f;

    static double s_lastDSP; // shared phase seed

    void Awake()
    {
        src = GetComponent<AudioSource>();
        if (!player)
        {
            var pObj = GameObject.FindGameObjectWithTag("Player");
            if (pObj) player = pObj.transform;
        }

        src.clip = fireLoop ? fireLoop : src.clip;
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 1f;
        src.dopplerLevel = 0f;
        src.rolloffMode = AudioRolloffMode.Custom;
        src.minDistance = Mathf.Max(0.1f, fullVolumeRadius);
        src.maxDistance = Mathf.Max(stopRadius, fadeOutRadius + 0.01f);
        src.priority = 128;
        src.volume = 0f;

        if (s_lastDSP <= 0) s_lastDSP = AudioSettings.dspTime;
    }

    void Update()
    {
        if (!player || (!src.clip)) { HardStop(); return; }

        float d = Vector3.Distance(player.position, transform.position);
        float distT = DistanceToAtten(d);
        float desired = baseVolume * distT;

        if (desired > 0f)
        {
            if (!src.isPlaying) StartAligned();
            lingerTimer = 0f;
            targetVol = desired;
        }
        else
        {
            targetVol = 0f;

            if (src.isPlaying)
            {
                lingerTimer += Time.deltaTime;
                if (lingerTimer >= lingerAfterExit)
                {
                    HardStop();
                    lingerTimer = 0f;
                }
            }
        }

        float smooth = (targetVol > currentVol) ? Mathf.Max(0.01f, attack) : Mathf.Max(0.01f, release);
        currentVol = Mathf.SmoothDamp(currentVol, targetVol, ref vel, smooth);
        if (currentVol < minAudible) currentVol = 0f;

        src.volume = currentVol;
    }

    float DistanceToAtten(float d)
    {
        if (d <= fullVolumeRadius) return 1f;
        if (d >= stopRadius) return 0f;

        float span = Mathf.Max(0.0001f, stopRadius - fullVolumeRadius);
        float x = Mathf.Clamp01((d - fullVolumeRadius) / span);
        float k = Mathf.Lerp(1.0f, 2.5f, distanceCurveBias);
        return 1f - Mathf.Pow(x, k);
    }

    void StartAligned()
    {
        double dsp = AudioSettings.dspTime;
        s_lastDSP = dsp;
        int freq = src.clip.frequency;
        int samples = src.clip.samples;

        long nowSamples = (long)(dsp * freq);
        int startSample = (int)(nowSamples % samples);

        src.timeSamples = Mathf.Clamp(startSample, 0, samples - 1);
        src.volume = 0f;
        currentVol = 0f;
        vel = 0f;

        src.Play();
    }

    void HardStop()
    {
        if (!src.isPlaying) return;
        src.Stop();
        src.volume = 0f;
        currentVol = 0f;
        targetVol = 0f;
        vel = 0f;
    }

    public void IgniteImmediate()
    {
        if (!src.clip) return;
        if (!src.isPlaying) StartAligned();
        targetVol = baseVolume;
    }

    public void ExtinguishImmediate()
    {
        lingerTimer = 0f;
        HardStop();
    }
}
