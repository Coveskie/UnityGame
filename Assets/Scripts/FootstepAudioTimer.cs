using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(AudioSource))]
public class FootstepAudioTimer : MonoBehaviour
{
    [Header("Link (optional, tunes thresholds to your speeds)")]
    public PlayerMovement playerMovement; // drag if available

    [Header("Clips")]
    public AudioClip[] defaultSteps;      // 3–6 single-hit clips
    public AudioClip landClip;            // optional

    [Header("Meters per step (cadence)")]
    public float crouchStepDistance = 0.95f;
    public float walkStepDistance = 0.75f;
    public float runStepDistance = 0.60f;

    [Header("Speed thresholds (m/s)")]
    [Tooltip("Below this we consider you idle (enter idle)")]
    public float idleEnterSpeed = 0.8f;
    [Tooltip("Above this we consider you moving (exit idle)")]
    public float idleExitSpeed = 1.2f;
    [Tooltip("At/above this, use run cadence")]
    public float runSpeedThreshold = 6.5f;

    [Header("Timing caps (seconds)")]
    [Tooltip("Hard minimum time between steps")]
    public float minStepInterval = 0.22f;
    [Tooltip("Hard maximum time between steps (very slow walk)")]
    public float maxStepInterval = 0.6f;
    [Tooltip("Delay before the very first step when you start moving")]
    public float firstStepDelay = 0.18f;

    [Header("Smoothing")]
    [Tooltip("Smooths speed to avoid jittery cadence (seconds)")]
    public float speedSmoothing = 0.15f;
    [Tooltip("Faster smoothing when slowing down (seconds)")]
    public float stopSmoothing = 0.05f;
    [Tooltip("Grounded buffer so 1-frame airborne doesn’t break cadence")]
    public float groundedCoyoteTime = 0.08f;

    [Header("Idle snap")]
    [Tooltip("Extra boost added to idleEnterSpeed internally for quicker stop feel")]
    public float idleEnterSpeedBoost = 0.15f;

    [Header("Audio")]
    [Range(0f, 1f)] public float volume = 1f;
    [Range(0f, 0.2f)] public float pitchJitter = 0.05f;

    // --- internals ---
    CharacterController _cc;
    AudioSource _src;

    float _smoothedSpeed;
    float _timeToNextStep;
    float _groundedTimer;
    bool _wasGrounded;
    bool _moving;           // debounced moving state
    bool _wasMoving;        // previous frame
    bool _wasCrouched;

    void Awake()
    {
        _cc = GetComponent<CharacterController>();
        _src = GetComponent<AudioSource>();
        _src.playOnAwake = false;
        _src.loop = false;
        _src.clip = null;
        _src.spatialBlend = 1f; // 3D
        _src.dopplerLevel = 0f;

        if (playerMovement)
        {
            // Sensible auto thresholds from your movement speeds (5/9/3 defaults)
            idleEnterSpeed = Mathf.Max(0.3f, playerMovement.walkSpeed * 0.10f);
            idleExitSpeed = Mathf.Max(idleEnterSpeed + 0.2f, playerMovement.walkSpeed * 0.24f);
            runSpeedThreshold = Mathf.Max(playerMovement.walkSpeed * 0.7f, playerMovement.runSpeed * 0.7f);
        }

        _timeToNextStep = firstStepDelay;
    }

    void Update()
    {
        // --- speed + grounded with smoothing/buffering ---
        Vector3 v = _cc.velocity;
        float horizSpeed = new Vector2(v.x, v.z).magnitude;

        // Asymmetric exponential smoothing: faster when decelerating
        float target = horizSpeed;
        bool decelerating = target < _smoothedSpeed;
        float tau = decelerating ? Mathf.Max(0.01f, stopSmoothing) : Mathf.Max(0.01f, speedSmoothing);
        float a = 1f - Mathf.Exp(-Time.deltaTime / tau);
        _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, target, a);

        bool grounded = _cc.isGrounded;
        if (grounded) _groundedTimer = groundedCoyoteTime;
        else _groundedTimer -= Time.deltaTime;
        bool bufferedGrounded = _groundedTimer > 0f;

        // Debounced moving state (hysteresis) with a slight boosted stop threshold
        float enterIdle = idleEnterSpeed + idleEnterSpeedBoost; // stop sooner
        if (_moving)
            _moving = _smoothedSpeed > enterIdle;  // drop out once we go below the (slightly higher) lower bar
        else
            _moving = _smoothedSpeed >= idleExitSpeed; // only start once we exceed the higher bar

        bool isCrouched = playerMovement && Mathf.Abs(_cc.height - playerMovement.crouchHeight) < 0.01f;

        // Recompute cadence interval from smoothed speed, bounded
        float stepMeters = isCrouched ? crouchStepDistance :
                           (_smoothedSpeed >= runSpeedThreshold ? runStepDistance : walkStepDistance);

        float interval = stepMeters / Mathf.Max(_smoothedSpeed, 0.01f);
        interval = Mathf.Clamp(interval, minStepInterval, maxStepInterval);

        // If speed state changes (e.g., start moving, change crouch/run), reset timing gently
        if (!_wasMoving && _moving) _timeToNextStep = Mathf.Max(_timeToNextStep, firstStepDelay);
        if (isCrouched != _wasCrouched) _timeToNextStep = Mathf.Min(_timeToNextStep, interval * 0.6f);

        // --- hard cancel on stop (prevents "one extra step") ---
        bool justStopped = _wasMoving && !_moving;
        if (justStopped)
        {
            // Cancel any pending fire this frame and reset the timer so no phantom step plays
            _timeToNextStep = firstStepDelay;
        }

        // --- cadence timer ---
        if (_moving && bufferedGrounded && defaultSteps != null && defaultSteps.Length > 0 && !justStopped)
        {
            _timeToNextStep -= Time.deltaTime;
            if (_timeToNextStep <= 0f)
            {
                PlayStep();
                // carry remainder but never fire more than once per frame
                _timeToNextStep += interval;
                if (_timeToNextStep < minStepInterval * 0.5f)
                    _timeToNextStep = minStepInterval * 0.5f; // extra safety
            }
        }
        else
        {
            // While idle or airborne, keep timer positive to avoid immediate fire on next frame
            _timeToNextStep = Mathf.Max(_timeToNextStep, firstStepDelay);
        }

        // Landing thud (optional) – not tied to cadence
        if (!_wasGrounded && grounded && landClip != null)
            PlayOne(landClip);

        _wasGrounded = grounded;
        _wasMoving = _moving;
        _wasCrouched = isCrouched;
    }

    void PlayStep()
    {
        int i = Random.Range(0, defaultSteps.Length);
        PlayOne(defaultSteps[i]);
    }

    void PlayOne(AudioClip clip)
    {
        if (!clip) return;
        _src.volume = volume;
        _src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        _src.PlayOneShot(clip);
    }
}
