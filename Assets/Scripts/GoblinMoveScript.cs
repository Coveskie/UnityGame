using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class GoblinController : MonoBehaviour
{
    public enum GoblinState { Idle, Patrol, Chase, Strafe, Attack, Flee, Return, IdleSit }

    [Header("Refs")]
    public Transform player;
    public Animator animator;
    public AudioSource footstepLoop;
    public Transform faceAim;

    [Header("Animator Names (matching your controller)")]
    public string moveBoolName = "IsMoving";   // your move bool
    public string attackBoolName = "Attack";     // your attack bool
    public string attackStateName = "Attack";     // exact state name for attack
    public string locomotionState = "Locomotion"; // your move/blend-tree state
    public string idleSitStateName = "IdleSit";    // sit state (only when calm/no player)

    [Header("Senses")]
    public float sightRange = 16f;
    public float sightAngle = 120f;
    public float hearRange = 10f;
    public LayerMask lineOfSightMask = ~0;

    [Header("Movement / Patrol")]
    public float patrolRadius = 10f;
    public float patrolWait = 2f;
    public float keepAwayDistance = 0.8f;

    [Header("Strafe/Chase")]
    public float strafeRadius = 1.8f;          // keep <= attackRange
    public float strafeJitter = 0.6f;
    public float strafeDirectionSwapTime = 2.2f;

    [Header("Attack")]
    public float attackRange = 1.7f;
    public float attackFov = 80f;
    public float attackCooldown = 1.1f;
    public float lungePrepTime = 0.18f;
    public float preAttackFaceTime = 0.10f;
    public float lungeDistance = 1.9f;
    public float lungeSpeedBoost = 1.35f;
    public bool requireLineOfSightToAttack = true;

    [Header("Speeds")]
    public float patrolSpeed = 2.0f;
    public float chaseSpeed = 3.4f;
    public float strafeSpeed = 3.0f;

    [Header("Morale / Health")]
    public int maxHP = 30;
    public int currentHP = 30;
    public int fleeAtHP = 8;
    public float fleeTime = 3.0f;

    [Header("Return/IdleSit logic")]
    public float returnHomeAfter = 6.0f;   // lose sight this long => go home
    public float idleSitAfter = 4.0f;      // calm at home this long => IdleSit
    public float idleSitMinTime = 2.0f;    // stay seated at least this long

    [Header("Debug")]
    public bool drawGizmos = true;

    // ---- internals ----
    NavMeshAgent agent;
    Vector3 homePos;
    GoblinState state = GoblinState.Patrol;

    float lastSeenTimer = 0f;
    float attackCDTimer = 0f;
    float strafeSwapTimer = 0f;
    float strafeDir = 1f;
    float idleSitTimer = 0f;
    Coroutine lungeCo;

    // Animator param hashes / presence flags
    int moveHash, attackHash;
    bool hasMove, hasAttack;

    // ---------- small helpers ----------
    bool IsInState(int layer, string stateName)
    {
        if (!animator || string.IsNullOrEmpty(stateName)) return false;
        var st = animator.GetCurrentAnimatorStateInfo(layer);
        return st.IsName(stateName);
    }

    void Crossfade(string stateName, float dur = 0.05f)
    {
        if (!animator || string.IsNullOrEmpty(stateName)) return;
        animator.CrossFadeInFixedTime(stateName, dur);
    }

    void SetBoolIfExists(int hash, bool v, ref bool hasFlag)
    {
        if (!animator) return;
        if (!hasFlag)
        {
            foreach (var p in animator.parameters)
            {
                if (p.nameHash == hash && p.type == AnimatorControllerParameterType.Bool)
                {
                    hasFlag = true;
                    break;
                }
            }
        }
        if (hasFlag) animator.SetBool(hash, v);
    }

    // ---------- lifecycle ----------
    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();

        if (!player)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
        if (!animator) animator = GetComponentInChildren<Animator>();

        moveHash = Animator.StringToHash(moveBoolName);
        attackHash = Animator.StringToHash(attackBoolName);

        homePos = transform.position;
        PickNewPatrolPoint(true);

        // We rotate manually for snappier facing + to avoid root-motion Sit pulling us around.
        agent.updateRotation = false;
        if (animator) animator.applyRootMotion = false;
    }

    void OnEnable()
    {
        currentHP = Mathf.Clamp(currentHP, 1, maxHP);
        state = GoblinState.Patrol;
        idleSitTimer = 0f;
    }

    void Update()
    {
        attackCDTimer -= Time.deltaTime;

        UpdateSenses();

        switch (state)
        {
            case GoblinState.Idle:
            case GoblinState.Patrol: UpdatePatrol(); break;
            case GoblinState.Chase: UpdateChase(); break;
            case GoblinState.Strafe: UpdateStrafe(); break;
            case GoblinState.Attack:  /* handled by coroutine */ break;
            case GoblinState.Flee: UpdateFlee(); break;
            case GoblinState.Return: UpdateReturn(); break;
            case GoblinState.IdleSit: UpdateIdleSit(); break;
        }

        UpdateAnimator();
        UpdateFootsteps();
        FaceAimAssist();
    }

    // ---------- senses ----------
    void UpdateSenses()
    {
        bool canSee = false;
        if (player)
        {
            Vector3 eye = transform.position + Vector3.up * 1.2f;
            Vector3 tgt = player.position + Vector3.up * 0.9f;
            Vector3 to = tgt - eye;
            float dist = to.magnitude;

            // Hearing bubble
            if (dist <= hearRange) canSee = true;

            // Vision cone + LOS
            if (!canSee && dist <= sightRange)
            {
                float angle = Vector3.Angle(transform.forward, to);
                if (angle <= sightAngle * 0.5f)
                {
                    if (!Physics.Raycast(eye, to.normalized, dist, lineOfSightMask))
                        canSee = true;
                }
            }
        }

        if (canSee)
        {
            lastSeenTimer = 0f;
            idleSitTimer = 0f;

            if (state == GoblinState.Patrol || state == GoblinState.Return ||
                state == GoblinState.Idle || state == GoblinState.IdleSit)
            {
                EnterChase();
            }
        }
        else
        {
            lastSeenTimer += Time.deltaTime;
            if ((state == GoblinState.Chase || state == GoblinState.Strafe) &&
                lastSeenTimer >= returnHomeAfter)
            {
                EnterReturn();
            }
        }
    }

    // ---------- patrol ----------
    void UpdatePatrol()
    {
        agent.speed = patrolSpeed;
        agent.stoppingDistance = 0f;

        // Consider entering IdleSit if calm at home
        if (!agent.pathPending && agent.remainingDistance <= 0.05f)
        {
            idleSitTimer += Time.deltaTime;
            if (idleSitTimer >= idleSitAfter)
                EnterIdleSit();
        }
        else
        {
            idleSitTimer = 0f;
        }

        // Walk around
        if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.05f)
            StartCoroutine(PatrolWaitAndPick());
    }

    IEnumerator PatrolWaitAndPick()
    {
        state = GoblinState.Idle;
        yield return new WaitForSeconds(Random.Range(patrolWait * 0.5f, patrolWait * 1.5f));
        PickNewPatrolPoint();
        state = GoblinState.Patrol;
    }

    void PickNewPatrolPoint(bool immediate = false)
    {
        Vector3 random = homePos + Random.insideUnitSphere * patrolRadius;
        if (NavMesh.SamplePosition(random, out var hit, 3f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }
        else if (!immediate)
        {
            agent.SetDestination(homePos + (Random.onUnitSphere * 2f));
        }
        else
        {
            agent.SetDestination(transform.position);
        }
    }

    // ---------- chase ----------
    void EnterChase()
    {
        state = GoblinState.Chase;
        agent.isStopped = false;

        if (lungeCo != null) StopCoroutine(lungeCo);

        // yank from sit if needed
        if (IsInState(0, idleSitStateName)) Crossfade(locomotionState);

        // set animator flags
        SetBoolIfExists(moveHash, true, ref hasMove);
        SetBoolIfExists(attackHash, false, ref hasAttack);
    }

    void UpdateChase()
    {
        if (!player) { EnterReturn(); return; }

        agent.speed = chaseSpeed;
        agent.stoppingDistance = Mathf.Max(keepAwayDistance, attackRange * 0.6f);
        agent.SetDestination(player.position);

        float dist = Vector3.Distance(transform.position, player.position);
        if (dist <= Mathf.Max(strafeRadius + 0.5f, attackRange + 0.4f))
        {
            EnterStrafe();
        }
    }

    // ---------- strafe ----------
    void EnterStrafe()
    {
        state = GoblinState.Strafe;
        strafeSwapTimer = 0f;
        strafeDir = Random.value < 0.5f ? -1f : 1f;

        if (IsInState(0, idleSitStateName)) Crossfade(locomotionState);
        SetBoolIfExists(moveHash, true, ref hasMove);
    }

    void UpdateStrafe()
    {
        if (!player) { EnterReturn(); return; }

        agent.speed = strafeSpeed;

        // keep orbit inside/near attack range so we can commit
        float desiredOrbit = Mathf.Max(0.15f, Mathf.Min(strafeRadius, attackRange - 0.1f));
        agent.stoppingDistance = Mathf.Clamp(attackRange * 0.5f, 0f, desiredOrbit - 0.05f);

        Vector3 toPlayer = player.position - transform.position;
        float dist = toPlayer.magnitude;

        // orbit target with wobble
        Vector3 tangent = Vector3.Cross(Vector3.up, toPlayer.normalized) * strafeDir;
        float wobble = Mathf.Sin(Time.time * 2.6f) * strafeJitter;
        Vector3 desired = player.position + (toPlayer.normalized * (desiredOrbit + wobble)) + tangent * 1.25f;

        if (NavMesh.SamplePosition(desired, out var hit, 1.5f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);

        // flip orbit sometimes
        strafeSwapTimer += Time.deltaTime;
        if (strafeSwapTimer >= strafeDirectionSwapTime)
        {
            strafeSwapTimer = 0f;
            strafeDir *= -1f;
        }

        // LOS gate
        if (requireLineOfSightToAttack)
        {
            Vector3 eye = transform.position + Vector3.up * 1.2f;
            Vector3 tgt = player.position + Vector3.up * 0.9f;
            if (Physics.Raycast(eye, (tgt - eye).normalized, out var block, Mathf.Max(dist, 0.01f), lineOfSightMask))
                return;
        }

        // ready to attack?
        bool canAttack = attackCDTimer <= 0f && dist <= (attackRange + 0.35f);
        if (canAttack)
        {
            EnterAttack();
        }
        else if (dist > sightRange * 1.2f)
        {
            EnterReturn();
        }
        else if (dist > desiredOrbit + 2.0f)
        {
            EnterChase();
        }
    }

    // ---------- attack ----------
    void EnterAttack()
    {
        state = GoblinState.Attack;
        attackCDTimer = attackCooldown;

        if (lungeCo != null) StopCoroutine(lungeCo);
        lungeCo = StartCoroutine(DoLungeAndAttack());
    }

    IEnumerator DoLungeAndAttack()
    {
        if (!player) { EnterReturn(); yield break; }

        // Prep
        agent.isStopped = true;
        SetBoolIfExists(attackHash, true, ref hasAttack);
        SetBoolIfExists(moveHash, false, ref hasMove);

        if (!string.IsNullOrEmpty(attackStateName))
            Crossfade(attackStateName);

        // short face lock before moving
        float faceT = 0f;
        while (faceT < preAttackFaceTime)
        {
            if (player)
            {
                Vector3 dir = player.position - transform.position; dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation,
                        Quaternion.LookRotation(dir),
                        Time.deltaTime * 14f
                    );
            }
            faceT += Time.deltaTime;
            yield return null;
        }

        // Lunge move (via agent) toward predicted point
        agent.isStopped = false;

        Vector3 start = transform.position;
        Vector3 toward = (player.position - start).normalized;
        Vector3 target = start + toward * lungeDistance;
        if (NavMesh.SamplePosition(target, out var hit, 1.0f, NavMesh.AllAreas))
            target = hit.position;

        float originalSpeed = agent.speed;
        float originalAccel = agent.acceleration;
        agent.speed = originalSpeed * lungeSpeedBoost;
        agent.acceleration = originalAccel * 1.25f;
        agent.stoppingDistance = keepAwayDistance;
        agent.SetDestination(target);

        float timeout = 0.5f + (lungeDistance / Mathf.Max(agent.speed, 0.01f));
        float t = 0f;
        while (t < timeout && !agent.pathPending && agent.remainingDistance > agent.stoppingDistance + 0.05f)
        {
            if (player)
            {
                Vector3 dir = (player.position - transform.position).normalized;
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(dir),
                    Time.deltaTime * 10f
                );
            }
            t += Time.deltaTime;
            yield return null;
        }

        // Conclude
        agent.speed = originalSpeed;
        agent.acceleration = originalAccel;
        agent.isStopped = true;

        // TODO: apply damage here (animation event or overlap check)

        yield return new WaitForSeconds(0.25f);

        SetBoolIfExists(attackHash, false, ref hasAttack);
        agent.isStopped = false;

        // back to strafe to feel "pesky"
        EnterStrafe();
    }

    // ---------- flee ----------
    void EnterFlee()
    {
        state = GoblinState.Flee;
        if (lungeCo != null) StopCoroutine(lungeCo);
        StartCoroutine(DoFlee());
    }

    void UpdateFlee() { /* handled by DoFlee */ }

    IEnumerator DoFlee()
    {
        float timer = 0f;
        while (timer < fleeTime)
        {
            if (player)
            {
                Vector3 away = (transform.position - player.position).normalized;
                Vector3 dest = transform.position + away * 6f;
                if (NavMesh.SamplePosition(dest, out var hit, 2f, NavMesh.AllAreas))
                    agent.SetDestination(hit.position);
            }
            agent.speed = chaseSpeed;
            timer += Time.deltaTime;
            yield return null;
        }
        EnterReturn();
    }

    // ---------- return ----------
    void EnterReturn()
    {
        state = GoblinState.Return;
        agent.isStopped = false;
        agent.SetDestination(homePos);
        idleSitTimer = 0f;
    }

    void UpdateReturn()
    {
        agent.speed = patrolSpeed;
        agent.stoppingDistance = 0f;

        // consider seating if calm at home
        if (!agent.pathPending && agent.remainingDistance <= 0.05f)
        {
            idleSitTimer += Time.deltaTime;
            if (idleSitTimer >= idleSitAfter) EnterIdleSit();
        }
        else idleSitTimer = 0f;

        // if we reached home, resume patrol after a beat
        if (!agent.pathPending && agent.remainingDistance <= 0.1f)
        {
            PickNewPatrolPoint(true);
            state = GoblinState.Patrol;
        }
    }

    // ---------- idle sit ----------
    void EnterIdleSit()
    {
        state = GoblinState.IdleSit;
        idleSitTimer = 0f;
        agent.isStopped = true;

        SetBoolIfExists(moveHash, false, ref hasMove);
        if (!string.IsNullOrEmpty(idleSitStateName))
            Crossfade(idleSitStateName);

        StartCoroutine(IdleSitHold());
    }

    void UpdateIdleSit()
    {
        // just chill until min time passes or senses wake us up
    }

    IEnumerator IdleSitHold()
    {
        float t = 0f;
        while (t < idleSitMinTime && state == GoblinState.IdleSit)
        {
            t += Time.deltaTime;
            yield return null;
        }

        // After min time, if still seated, gently return to patrol
        if (state == GoblinState.IdleSit)
        {
            agent.isStopped = false;
            state = GoblinState.Patrol;
            PickNewPatrolPoint(true);
        }
    }

    // ---------- animator / sfx / facing ----------
    void UpdateAnimator()
    {
        if (!animator) return;

        float planarSpeed = new Vector3(agent.velocity.x, 0, agent.velocity.z).magnitude;

        bool movingStates =
            state == GoblinState.Patrol ||
            state == GoblinState.Chase ||
            state == GoblinState.Strafe ||
            state == GoblinState.Return;

        // drive IsMoving
        SetBoolIfExists(moveHash, movingStates && planarSpeed > 0.05f, ref hasMove);

        // if we are in moving/combat states but Animator somehow slipped into IdleSit, yank it back
        if ((movingStates || state == GoblinState.Attack) && IsInState(0, idleSitStateName))
            Crossfade(locomotionState);
    }

    void UpdateFootsteps()
    {
        if (!footstepLoop) return;

        bool shouldPlay =
            state == GoblinState.Patrol ||
            state == GoblinState.Chase ||
            state == GoblinState.Strafe ||
            state == GoblinState.Return;

        if (!shouldPlay || state == GoblinState.Attack || state == GoblinState.IdleSit)
        {
            if (footstepLoop.isPlaying) footstepLoop.Pause();
            return;
        }

        if (!footstepLoop.isPlaying) footstepLoop.UnPause();

        float moveFactor = Mathf.Clamp01(agent.velocity.magnitude / chaseSpeed);
        footstepLoop.volume = 0.25f + 0.5f * moveFactor;
        footstepLoop.pitch = 0.95f + 0.10f * moveFactor;
    }

    void FaceAimAssist()
    {
        if (!player) return;

        Vector3 dir = agent.hasPath ? (agent.steeringTarget - transform.position)
                                    : (player.position - transform.position);

        dir.y = 0f;
        if (dir.sqrMagnitude > 0.0001f)
        {
            float speed =
                state == GoblinState.Attack ? 10f :
                state == GoblinState.Strafe ? 7.5f : 6f;

            Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * speed);
        }

        if (faceAim)
        {
            Vector3 lookAt = Vector3.Lerp(faceAim.forward, (player.position - faceAim.position).normalized, 0.5f);
            faceAim.rotation = Quaternion.Slerp(faceAim.rotation, Quaternion.LookRotation(lookAt), Time.deltaTime * 5f);
        }
    }

    // ---------- combat hooks ----------
    public void TakeDamage(int amount)
    {
        currentHP = Mathf.Max(0, currentHP - amount);
        if (currentHP <= 0) { Die(); return; }

        if (currentHP <= fleeAtHP && state != GoblinState.Flee && state != GoblinState.Attack)
            EnterFlee();

        if (animator && hasAttack) animator.SetTrigger("Hit"); // optional if you have "Hit" trigger
    }

    void Die()
    {
        if (animator) animator.SetTrigger("Die"); // optional if you have "Die" trigger
        agent.isStopped = true;
        enabled = false;
        // Destroy(gameObject, 5f); // optional cleanup
    }

    // ---------- gizmos ----------
    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        Vector3 center = Application.isPlaying ? homePos : transform.position;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, patrolRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, sightRange);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, hearRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // vision cone lines
        Vector3 f = transform.forward;
        Quaternion l = Quaternion.AngleAxis(-sightAngle * 0.5f, Vector3.up);
        Quaternion r = Quaternion.AngleAxis(sightAngle * 0.5f, Vector3.up);
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, l * f * (sightRange * 0.8f));
        Gizmos.DrawRay(transform.position, r * f * (sightRange * 0.8f));
    }
}
