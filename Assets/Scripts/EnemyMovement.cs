using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyRoamer : MonoBehaviour
{
    public Transform player;
    public Camera playerCamera;
    public Transform enemyFaceTarget; // assign a child on the head/face

    [Header("Roaming")]
    public Transform roamCenter;
    public float roamRadius = 15f;
    public float roamPauseMin = 0.5f;
    public float roamPauseMax = 2.0f;

    [Header("Perception & Combat")]
    public float sightRange = 18f;       // 3D
    public float attackRange = 2.25f;    // planar
    public float minKeepDistance = 1.25f;// planar
    public float approachBuffer = 0.60f; // stop BEFORE minKeepDistance by this much
    public float giveUpTime = 4f;
    public LayerMask losMask = ~0;
    public float faceTurnSpeed = 10f;

    [Header("Attack Flow")]
    public float attackWindup = 0.15f;
    public float attackAnimDuration = 1.1f;
    public float postAttackHold = 0.25f;
    public float retreatDuration = 3.5f;
    public float attackCooldown = 1.2f;

    [Header("Camera Focus")]
    public float cameraEaseIn = 0.35f;   // time to ease to the face
    public float cameraTrackLerp = 8f;   // soft-track strength during hold

    [Header("Animation")]
    public Animator animator;            // uses BOOL "attack"
    public bool lockRootMotionDuringAttack = true;

    [Header("Audio (Crawl Loop)")]
    public AudioSource crawlSource;      // assign an AudioSource on the enemy
    public AudioClip crawlLoop;          // assign your crawling loop clip
    public float crawlFadeIn = 0.25f;
    public float crawlFadeOut = 0.25f;
    public float crawlMoveSpeedThreshold = 0.05f;   // sqrMagnitude threshold to consider "moving"
    [Range(0f, 1f)] public float crawlMaxVolume = 0.85f;
    public bool scaleVolumeWithSpeed = true;      // optional speed->volume scaling
    public float speedForMaxVolume = 3.0f;         // agent.velocity.magnitude at which volume hits max

    [Header("Audio Proximity Fade")]
    public float hearNearDistance = 2.5f;           // full volume inside this planar distance
    public float hearFarDistance = 12f;            // fades to 0 by this planar distance
    [Range(0.1f, 4f)] public float hearCurvePower = 1.0f; // 1=linear, >1 slower rise near far edge

    [Header("Debug")]
    public bool debugLogs = false;

    private NavMeshAgent agent;
    private float lastSeenPlayerTime = -999f;
    private float lastAttackTime = -999f;
    private bool isAttacking;

    private enum State { Roam, Chase, CooldownRetreat }
    private State state = State.Roam;

    // Arrival tolerances
    const float ARRIVE_DIST_EPS = 0.12f;
    const float ARRIVE_SPEED_EPS = 0.04f;

    // Audio fade state
    private Coroutine crawlFadeRoutine;
    private float crawlTargetVolume = 0f;
    private bool crawlIsPaused = false;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false;   // manual facing so we never glide backwards
        agent.autoBraking = true;

        if (!roamCenter) roamCenter = transform;
        PickNewRoamDestination();

        if (crawlSource)
        {
            crawlSource.loop = true;
            crawlSource.clip = crawlLoop ? crawlLoop : crawlSource.clip;
            crawlSource.playOnAwake = false;
            crawlSource.volume = 0f;
        }
    }

    void OnDisable()
    {
        if (crawlSource)
        {
            if (crawlFadeRoutine != null) StopCoroutine(crawlFadeRoutine);
            crawlSource.Stop();
            crawlSource.volume = 0f;
            crawlIsPaused = false;
        }
    }

    void Update()
    {
        if (isAttacking)
        {
            AlignToMotionOrPlayer();
            UpdateCrawlAudio(moving: false, planarDist: PlanarDistance(transform.position, player.position));
            return;
        }

        bool canSee = CanSeePlayer(out _);
        if (canSee) lastSeenPlayerTime = Time.time;

        switch (state)
        {
            case State.Roam:
                HandleRoam(canSee);
                break;
            case State.Chase:
                HandleChase(canSee);
                break;
            case State.CooldownRetreat:
                break;
        }

        AlignToMotionOrPlayer();
        ClampSeparation();

        // 🔉 Crawl audio: play while actually moving (chase/retreat paths cause velocity)
        bool moving = agent != null && agent.velocity.sqrMagnitude > crawlMoveSpeedThreshold && !agent.isStopped;
        float planarDist = PlanarDistance(transform.position, player.position);
        UpdateCrawlAudio(moving, planarDist);
    }

    void HandleRoam(bool canSee)
    {
        if (canSee && Time.time - lastAttackTime > attackCooldown)
        {
            state = State.Chase;
            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            StartCoroutine(RoamPauseThenPick());
    }

    void HandleChase(bool canSee)
    {
        if (!canSee && Time.time - lastSeenPlayerTime > giveUpTime)
        {
            state = State.Roam;
            PickNewRoamDestination();
            return;
        }

        float standoff = Mathf.Max(minKeepDistance, attackRange * 0.65f);
        float earlyStop = Mathf.Max(0.05f, standoff - approachBuffer);
        agent.stoppingDistance = earlyStop;

        if (canSee)
        {
            Vector3 standPos = GetStandPosition(earlyStop);
            agent.isStopped = false;
            agent.SetDestination(standPos);

            bool arrived =
                !agent.pathPending &&
                agent.remainingDistance <= agent.stoppingDistance + ARRIVE_DIST_EPS &&
                agent.velocity.sqrMagnitude <= ARRIVE_SPEED_EPS;

            float planarDist = PlanarDistance(transform.position, player.position);
            bool closeEnough = planarDist <= attackRange;

            if (debugLogs && arrived && closeEnough) Debug.Log("[EnemyRoamer] Attack: arrived & in range");

            if (arrived && closeEnough && Time.time - lastAttackTime > attackCooldown)
                StartCoroutine(AttackSequence());
        }
    }

    Vector3 GetStandPosition(float stopDist)
    {
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        Vector3 dir = toPlayer.sqrMagnitude < 0.0001f ? transform.forward : toPlayer.normalized;

        Vector3 desired = player.position - dir * Mathf.Max(stopDist, 0.01f);
        if (NavMesh.SamplePosition(desired, out NavMeshHit hit, 1.25f, NavMesh.AllAreas))
            return hit.position;
        return desired;
    }

    IEnumerator RoamPauseThenPick()
    {
        yield return new WaitForSeconds(Random.Range(roamPauseMin, roamPauseMax));
        PickNewRoamDestination();
    }

    void PickNewRoamDestination()
    {
        Vector3 random = Random.insideUnitSphere * roamRadius + roamCenter.position;
        if (NavMesh.SamplePosition(random, out NavMeshHit hit, roamRadius, NavMesh.AllAreas))
        {
            agent.stoppingDistance = 0f;
            agent.isStopped = false;
            agent.SetDestination(hit.position);
        }
    }

    bool CanSeePlayer(out float dist3D)
    {
        dist3D = Vector3.Distance(transform.position, player.position);
        if (dist3D > sightRange) return false;

        Vector3 eye = enemyFaceTarget ? enemyFaceTarget.position : transform.position + Vector3.up * 1.6f;
        Vector3 toPlayer = (PlayerCapsuleCenter() - eye);
        float len = toPlayer.magnitude;

        if (Physics.Raycast(eye, toPlayer.normalized, out RaycastHit hit, len, losMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform != player && hit.transform.root != player) return false;
        }
        return true;
    }

    Vector3 PlayerCapsuleCenter() => player.position + Vector3.up * 1.5f;

    void AlignToMotionOrPlayer()
    {
        Vector3 faceDir = Vector3.zero;

        if (agent != null && agent.hasPath && agent.velocity.sqrMagnitude > 0.01f)
            faceDir = agent.velocity;
        else if (player != null)
            faceDir = player.position - transform.position;

        faceDir.y = 0f;
        if (faceDir.sqrMagnitude < 0.0001f) return;

        Quaternion target = Quaternion.LookRotation(faceDir.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * faceTurnSpeed);
    }

    void ClampSeparation()
    {
        float d = PlanarDistance(transform.position, player.position);
        if (d < minKeepDistance && d > 0.001f)
        {
            Vector3 delta = transform.position - player.position; delta.y = 0f;
            Vector3 pushOut = delta.normalized * (minKeepDistance - d + 0.01f);
            Vector3 target = transform.position + pushOut;

            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 0.75f, NavMesh.AllAreas))
                agent.Warp(hit.position);
            else
                transform.position += pushOut;
        }
    }

    IEnumerator AttackSequence()
    {
        isAttacking = true;
        lastAttackTime = Time.time;

        // Stop & face player
        agent.isStopped = true;
        agent.ResetPath();
        yield return SmoothFaceToPlayer(0.10f);

        var pm = player ? player.GetComponent<PlayerMovement>() : null;
        bool froze = false;
        bool prevRootMotion = false;

        float hold = attackWindup + attackAnimDuration + postAttackHold;
        Coroutine focusRoutine = null;
        if (playerCamera && enemyFaceTarget)
            focusRoutine = StartCoroutine(FocusCamera(playerCamera, enemyFaceTarget, cameraEaseIn, hold, cameraTrackLerp));

        try
        {
            if (pm != null) { pm.Freeze(true); froze = true; }

            if (animator && lockRootMotionDuringAttack)
            {
                prevRootMotion = animator.applyRootMotion;
                animator.applyRootMotion = false; // prevent forward creep
            }

            if (attackWindup > 0f) yield return new WaitForSeconds(attackWindup);

            // Animator BOOL "attack"
            if (animator) animator.SetBool("attack", true);
            yield return new WaitForSeconds(attackAnimDuration);
            if (animator) animator.SetBool("attack", false);

            if (postAttackHold > 0f) yield return new WaitForSeconds(postAttackHold);
        }
        finally
        {
            if (focusRoutine != null) StopCoroutine(focusRoutine); // stop controlling camera; no restore
            if (animator && lockRootMotionDuringAttack) animator.applyRootMotion = prevRootMotion;
            if (froze && pm != null) pm.Freeze(false);
        }

        // Retreat and cooldown
        StartCoroutine(RetreatFromPlayer(retreatDuration));
        yield return new WaitForSeconds(Mathf.Max(0f, attackCooldown * 0.5f));

        isAttacking = false;
    }

    IEnumerator SmoothFaceToPlayer(float duration)
    {
        float t = 0f;
        Quaternion start = transform.rotation;
        Vector3 dir = (player.position - transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) yield break;
        Quaternion target = Quaternion.LookRotation(dir, Vector3.up);

        float dur = Mathf.Max(0.0001f, duration);
        while (t < dur)
        {
            t += Time.deltaTime;
            float a = Mathf.SmoothStep(0f, 1f, t / dur);
            transform.rotation = Quaternion.Slerp(start, target, a);
            yield return null;
        }
        transform.rotation = target;
    }

    IEnumerator RetreatFromPlayer(float duration)
    {
        state = State.CooldownRetreat;

        float tEnd = Time.time + duration;
        agent.stoppingDistance = 0f;
        agent.isStopped = false;

        while (Time.time < tEnd)
        {
            Vector3 awayDir = (transform.position - player.position).normalized;
            Vector3 target = transform.position + awayDir * 6f;
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                agent.SetDestination(hit.position);

            AlignToMotionOrPlayer();
            yield return new WaitForSeconds(0.2f);
        }

        state = State.Roam;
        PickNewRoamDestination();
    }

    // === Camera focus: ease to face, then soft-track; does NOT restore previous rotation ===
    IEnumerator FocusCamera(Camera cam, Transform target, float easeIn, float holdSeconds, float trackLerp)
    {
        if (cam == null || target == null) yield break;

        Transform ct = cam.transform;

        // Ease-in
        Quaternion startRot = ct.rotation;
        Vector3 dir = (target.position - ct.position).normalized;
        if (dir.sqrMagnitude < 0.0001f) yield break;
        Quaternion endRot = Quaternion.LookRotation(dir, Vector3.up);

        float t = 0f;
        float dur = Mathf.Max(0.0001f, easeIn);
        while (t < dur)
        {
            t += Time.deltaTime;
            float a = Mathf.SmoothStep(0f, 1f, t / dur);
            ct.rotation = Quaternion.Slerp(startRot, endRot, a);
            yield return null;
        }
        ct.rotation = endRot;

        // Hold with soft-tracking
        float h = 0f;
        while (h < holdSeconds)
        {
            dir = (target.position - ct.position).normalized;
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion track = Quaternion.LookRotation(dir, Vector3.up);
                ct.rotation = Quaternion.Slerp(ct.rotation, track, Time.deltaTime * Mathf.Max(0.01f, trackLerp));
            }
            h += Time.deltaTime;
            yield return null;
        }
        // No restore — leave camera where it ended.
    }

    float PlanarDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // ===== Crawl loop audio control (movement + proximity + attack pause) =====
    void UpdateCrawlAudio(bool moving, float planarDist)
    {
        if (!crawlSource) return;

        // If within attack range, PAUSE the loop (and keep it paused)
        if (planarDist <= attackRange)
        {
            // Fade to 0 then pause (if playing)
            SetCrawlTargetVolume(0f, crawlFadeOut, pauseAtEnd: true);
            return;
        }

        // Outside attack range: ensure we’re unpaused so we can fade in if needed
        if (crawlIsPaused && moving)
        {
            crawlSource.UnPause();
            crawlIsPaused = false;
        }

        // Base target from movement
        float target = moving ? crawlMaxVolume : 0f;

        // Scale with speed (optional)
        if (moving && scaleVolumeWithSpeed && agent != null)
        {
            float spd = agent.velocity.magnitude;
            float t = Mathf.Clamp01(spd / Mathf.Max(0.001f, speedForMaxVolume));
            target = Mathf.Lerp(0.25f * crawlMaxVolume, crawlMaxVolume, t);
        }

        // Proximity fade (planar)
        float prox = ProximityFactor(planarDist, hearNearDistance, hearFarDistance, hearCurvePower);
        target *= prox;

        // Fade toward target (fade in when target>0, fade out otherwise)
        SetCrawlTargetVolume(target, target > 0f ? crawlFadeIn : crawlFadeOut, pauseAtEnd: false);
    }

    void SetCrawlTargetVolume(float target, float fadeTime, bool pauseAtEnd)
    {
        // Only kick a fade if target changed meaningfully
        if (Mathf.Abs(target - crawlTargetVolume) < 0.01f && !pauseAtEnd) return;

        crawlTargetVolume = Mathf.Clamp01(target);

        if (crawlFadeRoutine != null) StopCoroutine(crawlFadeRoutine);
        crawlFadeRoutine = StartCoroutine(CrawlFadeTo(crawlTargetVolume, fadeTime, pauseAtEnd));
    }

    IEnumerator CrawlFadeTo(float targetVol, float fadeTime, bool pauseAtEnd)
    {
        if (!crawlSource) yield break;

        if (targetVol > 0f)
        {
            // ensure clip and playback
            if (crawlLoop && crawlSource.clip != crawlLoop) crawlSource.clip = crawlLoop;
            if (!crawlSource.isPlaying || crawlIsPaused)
            {
                crawlSource.UnPause();
                if (!crawlSource.isPlaying) crawlSource.Play();
                crawlIsPaused = false;
            }
        }

        float start = crawlSource.volume;
        float t = 0f;
        float dur = Mathf.Max(0.0001f, fadeTime);

        while (t < dur)
        {
            t += Time.deltaTime;
            float a = t / dur;
            crawlSource.volume = Mathf.Lerp(start, targetVol, a);
            yield return null;
        }

        crawlSource.volume = targetVol;

        if (Mathf.Approximately(targetVol, 0f))
        {
            if (pauseAtEnd)
            {
                crawlSource.Pause();
                crawlIsPaused = true;
            }
            else
            {
                crawlSource.Stop();
                crawlIsPaused = false;
            }
        }

        crawlFadeRoutine = null;
    }

    static float ProximityFactor(float dist, float near, float far, float power)
    {
        if (far <= near) return dist <= near ? 1f : 0f;
        if (dist <= near) return 1f;
        if (dist >= far) return 0f;
        float t = 1f - ((dist - near) / (far - near)); // 1 near, 0 far
        return Mathf.Pow(Mathf.Clamp01(t), Mathf.Max(0.1f, power));
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(roamCenter ? roamCenter.position : transform.position, roamRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, sightRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, minKeepDistance);

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, hearNearDistance);
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, hearFarDistance);
    }
}
