using UnityEngine;

/// <summary>
/// One police officer. Distinct from LaneObstacle: officers move, pursue, intercept, react to the
/// Distraction Pulse and grab characters (distance checks, no physics). Spawned and owned by PoliceDirector.
/// </summary>
public class PoliceOfficer : MonoBehaviour
{
    public enum Kind { Chaser, Interceptor, Sidewalk, Guard, Pursuer, Dog }
    public enum State { Approaching, InLane, Stunned, Leaving }

    public Kind kind;
    public State state = State.Approaching;
    public int lane = -1;                 // current/target lane (-1 = on the sidewalk)

    /// <summary>True when AJ's dodging AI should treat this officer like a blocked lane.</summary>
    public bool BlocksLaneForAI =>
        state != State.Stunned && state != State.Leaving && (kind == Kind.Interceptor || kind == Kind.Sidewalk) && lane >= 0;

    public bool IsDangerous => state == State.Approaching || state == State.InLane;

    /// <summary>True while this officer is coming up behind Claire (the case the HUD warns about).</summary>
    public bool IsBehindClaireThreat => IsDangerous && (kind == Kind.Chaser || kind == Kind.Dog || (kind == Kind.Pursuer && !_chasingAJ));

    private static readonly int RunState = Animator.StringToHash("Run");
    private static readonly int IdleState = Animator.StringToHash("Idle");
    private static readonly int KneelState = Animator.StringToHash("Kneel");
    private static readonly int FireState = Animator.StringToHash("Fire");

    private int _restState = IdleState;
    private bool _burstDone;
    private float _burstEndsAt;
    private float _nextShotAt;
    private Renderer _muzzleFlash;

    private PoliceDirector _director;
    private Animator _animator;
    private bool _hasIdle;
    private float _postX;
    private bool _chasingAJ;
    private bool _ajReacted;
    private float _stunUntil;
    private float _leaveSideX;
    private bool _retargeted;
    private float _sidewalkX;
    private bool _running;

    public void Init(PoliceDirector director, Kind k, int startLane, float sidewalkX)
    {
        _director = director;
        kind = k;
        lane = startLane;
        _sidewalkX = sidewalkX;
        _animator = GetComponentInChildren<Animator>();
        if (_animator != null)
        {
            _animator.applyRootMotion = false;
            _hasIdle = _animator.HasState(0, IdleState) && _animator.HasState(0, RunState);
            if (kind == Kind.Guard && _animator.HasState(0, KneelState) && Random.value < 0.5f)
                _restState = KneelState;
        }
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            if (r.name == PoliceDirector.MuzzleFlashName) _muzzleFlash = r;
        _postX = transform.position.x;
        state = State.Approaching;
        Face(kind == Kind.Interceptor || kind == Kind.Guard ? 180f : kind == Kind.Sidewalk ? (sidewalkX < 0f ? 90f : -90f) : 0f);
        SetRunning(kind != Kind.Sidewalk && kind != Kind.Guard);
    }

    private void Update()
    {
        if (!GameStartFlow.IsGameplayActive || _director == null)
        {
            if (_animator != null) _animator.speed = 0f; // freeze pose while paused / after the run
            return;
        }
        if (_animator != null) _animator.speed = _running || _hasIdle ? 1f : 0f;

        var claire = _director.Claire;
        var aj = _director.AJ;
        if (claire == null) return;
        float dt = Time.deltaTime;
        Vector3 p = transform.position;

        switch (state)
        {
            case State.Approaching:
            case State.InLane:
                Behave(ref p, claire, dt);
                break;
            case State.Stunned:
                if (Time.time >= _stunUntil) BeginLeaving();
                break;
            case State.Leaving:
                // Walk off to the sidewalk and fall behind.
                p.x = Mathf.MoveTowards(p.x, _leaveSideX, 4f * dt);
                p.z += _director.leaveSpeed * dt;
                Face(_leaveSideX < p.x ? -90f : 90f);
                break;
        }
        transform.position = p;

        // Grabs: only while dangerous and only in normal play. During the rescue window (AJCaught) police
        // contact is suspended, so the rescue can never be interrupted or re-triggered by another grab.
        if (IsDangerous && GameStartFlow.Instance != null && GameStartFlow.Instance.State == GameStartFlow.RunState.Playing)
        {
            if (Reaches(claire.transform.position, p))
            {
                _director.OnOfficerGrabbedClaire(this);
                Stun(_director.afterGrabStunSeconds);
            }
            else if (aj != null && !aj.IsCaught && !aj.IsInvulnerable && CanCatchAJ && Reaches(aj.transform.position, p))
            {
                _director.OnOfficerGrabbedAJ(this);
                state = State.InLane;
            }
        }

        // Despawn far behind / far ahead.
        float cz = claire.transform.position.z;
        // (Guards are placed with their roadblock, which can be further ahead than despawnAhead.)
        if (p.z < cz - _director.despawnBehind || (kind != Kind.Guard && p.z > cz + _director.despawnAhead))
            _director.Despawn(this);
    }

    private bool CanCatchAJ => kind != Kind.Chaser && kind != Kind.Dog && (kind != Kind.Pursuer || _chasingAJ);

    private void Behave(ref Vector3 p, PlayerLaneMovement claire, float dt)
    {
        float cz = claire.transform.position.z;
        switch (kind)
        {
            case Kind.Chaser:
                // Run up behind Claire. Locks onto her lane once, when close enough to be warned about.
                if (!_retargeted && cz - p.z < _director.chaserRetargetDistance)
                {
                    _retargeted = true;
                    lane = claire.CurrentLane;
                }
                p.z += _director.chaserSpeed * dt;
                p.x = Mathf.MoveTowards(p.x, LaneMath.SharedLaneX(lane), 3.5f * dt);
                if (p.z > cz + 1.5f) BeginLeaving(); // missed her: veer off
                break;

            case Kind.Pursuer:
                BehavePursuer(ref p, claire, dt);
                break;

            case Kind.Guard:
                BehaveGuard(ref p, claire, dt);
                break;

            case Kind.Dog:
                // Hunts Claire from behind, committing to her lane once it is close enough to be warned about.
                if (!_retargeted && cz - p.z < _director.dogRetargetDistance)
                {
                    _retargeted = true;
                    lane = claire.CurrentLane;
                }
                p.z += _director.dogSpeed * dt;
                p.x = Mathf.MoveTowards(p.x, LaneMath.SharedLaneX(lane), 5f * dt);
                if (p.z > cz + 1.5f) BeginLeaving(); // she dodged: it runs off
                break;

            case Kind.Interceptor:
                // Jog towards the runners down the middle of a lane.
                p.z -= _director.interceptorSpeed * dt;
                p.x = Mathf.MoveTowards(p.x, LaneMath.SharedLaneX(lane), 3f * dt);
                state = State.InLane;
                if (!_director.LaneStaysFair(this, lane, p.z)) BeginLeaving();
                break;

            case Kind.Sidewalk:
                if (state == State.Approaching)
                {
                    // Wait on the curb until the runners are close, then step into the outer lane.
                    float ajZ = _director.AJ != null ? _director.AJ.transform.position.z : cz;
                    float trigger = Mathf.Min(cz, ajZ);
                    if (p.z - Mathf.Max(cz, ajZ) < _director.sidewalkStepDistance && p.z > trigger)
                    {
                        int outer = _sidewalkX < 0f ? 0 : LaneMath.SharedLaneCount - 1;
                        if (_director.LaneStaysFair(this, outer, p.z))
                        {
                            lane = outer;
                            state = State.InLane;
                            SetRunning(true);
                        }
                        else BeginLeaving();
                    }
                }
                else
                {
                    p.x = Mathf.MoveTowards(p.x, LaneMath.SharedLaneX(lane), _director.sidewalkStepSpeed * dt);
                    p.z -= 1f * dt;
                    Face(180f);
                }
                break;
        }
    }

    /// <summary>Runs Claire down from behind; if she sidesteps him he carries on after AJ, then gives up.</summary>
    private void BehavePursuer(ref Vector3 p, PlayerLaneMovement claire, float dt)
    {
        float cz = claire.transform.position.z;
        var aj = _director.AJ;
        p.z += _director.pursuerSpeed * dt;

        if (!_chasingAJ)
        {
            if (!_retargeted && cz - p.z < _director.chaserRetargetDistance)
            {
                _retargeted = true;
                lane = claire.CurrentLane;
            }
            if (p.z > cz + 1.5f)
            {
                _chasingAJ = true;
                if (aj != null) lane = aj.CurrentLane; // commits to one lane, so AJ can shake him off
            }
        }
        else
        {
            if (aj == null || aj.IsCaught || p.z > aj.transform.position.z + 1.5f)
            {
                BeginLeaving();
                return;
            }
            if (!_ajReacted && aj.transform.position.z - p.z < _director.pursuerDodgeDistance)
            {
                _ajReacted = true;
                if (Random.value >= _director.pursuerCatchChance) aj.DodgePursuer(lane);
            }
        }
        p.x = Mathf.MoveTowards(p.x, LaneMath.SharedLaneX(lane), 3.5f * dt);
    }

    /// <summary>
    /// Holds a post at a roadblock and sidesteps at whoever comes past. The sidestep stops short of the
    /// neighbouring lane's centre, so a runner already centred in the open gap is safe and a late one is not.
    /// </summary>
    private void BehaveGuard(ref Vector3 p, PlayerLaneMovement claire, float dt)
    {
        state = State.InLane;
        if (UpdateBurst(claire, p)) return;

        float targetX = _postX;
        float nearest = _director.guardLungeDistance;
        ConsiderLungeTarget(claire.transform, p, ref nearest, ref targetX);
        var aj = _director.AJ;
        if (aj != null && !aj.IsCaught) ConsiderLungeTarget(aj.transform, p, ref nearest, ref targetX);

        bool moving = Mathf.Abs(p.x - targetX) > 0.05f;
        if (moving != _running) SetRunning(moving);
        p.x = Mathf.MoveTowards(p.x, targetX, _director.guardLungeSpeed * dt);
    }

    /// <summary>
    /// One burst of fire down the guard's own lane as Claire approaches. It only hits her if she is still in
    /// that lane when the burst ends. Returns true while firing.
    /// </summary>
    private bool UpdateBurst(PlayerLaneMovement claire, Vector3 p)
    {
        if (_burstEndsAt > 0f)
        {
            bool shot = Time.time >= _nextShotAt;
            if (shot)
            {
                _nextShotAt = Time.time + 0.2f;
                ProceduralSfx.Play(ProceduralSfx.Gunshot, 0.5f);
            }
            if (_muzzleFlash != null) _muzzleFlash.enabled = Time.time < _nextShotAt - 0.13f;

            if (Time.time < _burstEndsAt) return true;

            _burstEndsAt = 0f;
            if (_muzzleFlash != null) _muzzleFlash.enabled = false;
            SetRunning(false);
            if (Mathf.Abs(claire.transform.position.x - p.x) < _director.grabRadiusX)
                _director.OnGuardShotClaire();
            return false;
        }

        float ahead = p.z - claire.transform.position.z;
        if (_burstDone || !_hasIdle || !_animator.HasState(0, FireState)
            || ahead > _director.guardFireRange || ahead < _director.guardLungeDistance)
            return false;

        _burstDone = true;
        _burstEndsAt = Time.time + _director.guardBurstSeconds;
        _nextShotAt = 0f;
        _animator.CrossFadeInFixedTime(FireState, 0.1f);
        return true;
    }

    private void ConsiderLungeTarget(Transform runner, Vector3 p, ref float nearest, ref float targetX)
    {
        float ahead = p.z - runner.position.z;
        if (ahead < -1f || ahead >= nearest) return;
        nearest = ahead;
        float reach = _director.guardLungeReach;
        targetX = Mathf.Clamp(runner.position.x, _postX - reach, _postX + reach);
    }

    private bool Reaches(Vector3 target, Vector3 p)
    {
        return Mathf.Abs(target.x - p.x) < _director.grabRadiusX && Mathf.Abs(target.z - p.z) < _director.grabRadiusZ;
    }

    /// <summary>Distraction Pulse / after a grab: stop, no grabbing, then walk off.</summary>
    public void Stun(float seconds)
    {
        if (state == State.Leaving) return;
        _burstEndsAt = 0f;
        if (_muzzleFlash != null) _muzzleFlash.enabled = false;
        state = State.Stunned;
        _stunUntil = Time.time + Mathf.Max(0.1f, seconds);
        SetRunning(false);
    }

    public void BeginLeaving()
    {
        state = State.Leaving;
        _burstEndsAt = 0f;
        if (_muzzleFlash != null) _muzzleFlash.enabled = false;
        lane = -1;
        _leaveSideX = transform.position.x < 0f ? -_director.SidewalkX : _director.SidewalkX;
        SetRunning(true);
    }

    private void SetRunning(bool running)
    {
        _running = running;
        if (_animator == null) return;
        _animator.speed = running || _hasIdle ? 1f : 0f;
        if (_hasIdle) _animator.CrossFadeInFixedTime(running ? RunState : _restState, 0.15f);
    }

    private void Face(float yaw)
    {
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }
}
