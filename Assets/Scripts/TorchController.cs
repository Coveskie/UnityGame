// Assets/Scripts/VFX/TorchSafeController.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TorchSafeController : MonoBehaviour
{
    [Header("Hooks")]
    [Tooltip("ParticleSystems that represent flame/sparks/smoke")]
    public List<ParticleSystem> particles = new List<ParticleSystem>();
    public Light torchLight;                    // optional
    public Renderer[] flameRenderers;           // optional flame mesh/billboard
    public AudioSource loopAudio;               // optional crackle loop

    [Header("Behavior")]
    [Tooltip("Fast fade for light on/off, uses unscaled time.")]
    public float lightFadeSeconds = 0.1f;
    [Tooltip("Clear particles when turning off (keeps visuals crisp).")]
    public bool clearOnOff = true;

    bool _isOn;
    Coroutine _fade;

    void Reset()
    {
        particles.Clear();
        GetComponentsInChildren(particles); // grab all ParticleSystems under torch
        torchLight = GetComponentInChildren<Light>();
        flameRenderers = GetComponentsInChildren<Renderer>(true);
        loopAudio = GetComponentInChildren<AudioSource>();
    }

    public void TurnOn()
    {
        if (_fade != null) StopCoroutine(_fade);
        _fade = StartCoroutine(FadeOn());
    }

    public void TurnOff()
    {
        if (_fade != null) StopCoroutine(_fade);
        _fade = StartCoroutine(FadeOff());
    }

    IEnumerator FadeOn()
    {
        _isOn = true;

        // visuals on
        if (flameRenderers != null)
            foreach (var r in flameRenderers) if (r) r.enabled = true;

        // particles: enable emission + play
        foreach (var ps in particles)
        {
            if (!ps) continue;
            var em = ps.emission; em.enabled = true;
            ps.Clear(true);
            ps.Play(true);
        }

        // audio
        if (loopAudio)
        {
            if (!loopAudio.isPlaying) loopAudio.Play();
            loopAudio.mute = false;
        }

        // light fade in (fast)
        if (torchLight)
        {
            float t = 0f;
            float dur = Mathf.Max(0.01f, lightFadeSeconds);
            float start = 0f;
            float baseIntensity = torchLight.intensity;

            torchLight.intensity = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                torchLight.intensity = Mathf.Lerp(start, baseIntensity, t / dur);
                yield return null;
            }
            torchLight.intensity = baseIntensity;
        }

        _fade = null;
    }

    IEnumerator FadeOff()
    {
        _isOn = false;

        // particles: stop emitting immediately
        foreach (var ps in particles)
        {
            if (!ps) continue;
            var em = ps.emission; em.enabled = false;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (clearOnOff) ps.Clear(true);
        }

        // light fade out (fast)
        if (torchLight)
        {
            float t = 0f;
            float dur = Mathf.Max(0.01f, lightFadeSeconds);
            float start = torchLight.intensity;

            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                torchLight.intensity = Mathf.Lerp(start, 0f, t / dur);
                yield return null;
            }
            torchLight.intensity = 0f;
        }

        // audio and visuals off
        if (loopAudio) loopAudio.mute = true;
        if (flameRenderers != null)
            foreach (var r in flameRenderers) if (r) r.enabled = false;

        // IMPORTANT: keep the GameObject ACTIVE. Do NOT SetActive(false).
        _fade = null;
    }
}
