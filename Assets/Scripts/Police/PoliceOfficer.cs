using UnityEngine;

/// <summary>
/// One police officer. Distinct from LaneObstacle: officers move, pursue, intercept, react to the
/// Distraction Pulse and grab characters (distance checks, no physics). Spawned and owned by PoliceDirector.
/// </summary>
public class PoliceOfficer : MonoBehaviour
{
    public enum Kind { Chaser, Interceptor, Sidewalk, Guard, Pursuer, Dog }
    /// <summary>
    /// Police never run from the suspects. Holding = standing ready (at the curb, or waiting out a rescue).
    /// Leaving = the runners have passed, so the officer turns and gives chase until left behind.
    /// </summary>
    public enum State { Approaching, InLane, Stunned, Leaving, Holding }

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
    private enum Shooting { None, Aiming, Firing }
    private Shooting _shooting;
    private float _aimEndsAt;
    private float _nextShotAt;
    private float _flashOffAt;
    private int _shotsLeft;
    private bool _volleyDone;       // guards fire one volley per roadblock
    private float _nextVolleyAt;
    private Renderer _muzzleFlash;
    private LineRenderer _laser;

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
    private float _holdX;
    private bool _atCurb;

    private bool HoldsGround => kind == Kind.Guard || kind == Kind.Interceptor || kind == Kind.Sidewalk;

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
        if (_muzzleFlash != null && director.LaserMaterial != null)
        {
            _laser = gameObject.AddComponent<LineRenderer>();
            _laser.sharedMaterial = director.LaserMaterial;
            _laser.positionCount = 2;
            _laser.startWidth = _laser.endWidth = 0.07f;
            _laser.enabled = false;
        }
        _nextVolleyAt = Time.time + Random.Range(0.5f, 2f);
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
                Vector3 before = p;
                Behave(ref p, claire, dt);
                AvoidCars(before, ref p, dt);
                break;
            case State.Stunned:
                if (Time.time >= _stunUntil) ReturnToDuty();
                break;
            case State.Holding:
                bool moving = Mathf.Abs(p.x - _holdX) > 0.05f;
                if (moving != _running) SetRunning(moving);
                p.x = Mathf.MoveTowards(p.x, _holdX, 3.5f * dt);
                if (_atCurb && GameStartFlow.Instance.State == GameStartFlow.RunState.Playing) UpdateShooting(claire, p);
                break;
            case State.Leaving:
                // Outrun: keeps chasing in the runners' direction, drifting clear of the lanes as it falls behind.
                Vector3 leavingFrom = p;
                p.x = Mathf.MoveTowards(p.x, _leaveSideX, 1.5f * dt);
                p.z += _director.leaveSpeed * dt;
                AvoidCars(leavingFrom, ref p, dt);
                Face(0f);
                break;
        }
        transform.position = p;
        UpdateCameraVisibility(p);

        // Grabs: only while dangerous and only in normal play. During the rescue window (AJCaught) police
        // contact is suspended, so the rescue can never be interrupted or re-triggered by another grab.
        if (IsDangerous && GameStartFlow.Instance != null && GameStartFlow.Instance.State == GameStartFlow.RunState.Playing)
        {
            if (!claire.IsEvading && Reaches(claire.transform.position, p))
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

        // Once both runners are past, a posted officer turns and gives chase.
        float cz = claire.transform.position.z;
        if (HoldsGround && state != State.Leaving)
        {
            float lastRunner = aj != null ? Mathf.Min(cz, aj.transform.position.z) : cz;
            if (p.z < lastRunner - 1.5f) BeginLeaving();
        }

        // Despawn far behind / far ahead. (An officer who has been outrun goes as soon as the camera has left it.)
        // (Guards are placed with their roadblock, which can be further ahead than despawnAhead.)
        float behindLimit = state == State.Leaving ? _director.outrunDespawnBehind : _director.despawnBehind;
        if (p.z < cz - behindLimit || (kind != Kind.Guard && p.z > cz + _director.despawnAhead))
            _director.Despawn(this);
    }

    private const float AvoidSideSpeed = 9f;
    private const float AvoidPastMargin = 1.5f;
    private bool _avoiding;
    private float _avoidX, _avoidEndZ;

    /// <summary>
    /// Police are moved by script, so physics would not stop them at a car. Instead they look ahead and
    /// sidestep into a clear lane, holding it until they are past. With nowhere to go they wait.
    /// </summary>
    private void AvoidCars(Vector3 from, ref Vector3 p, float dt)
    {
        float moved = p.z - from.z;
        if (Mathf.Abs(moved) < 0.0001f) return;
        float dir = Mathf.Sign(moved);

        if (_avoiding && (p.z - _avoidEndZ) * dir >= 0f) _avoiding = false;

        if (_director.FindCarAhead(_avoiding ? _avoidX : p.x, p.z, dir, out float carEndZ))
        {
            if (!_director.FindClearLane(p.x, p.z, dir, out float clearX))
            {
                p = from;
                return;
            }
            _avoiding = true;
            _avoidX = clearX;
            _avoidEndZ = carEndZ + dir * AvoidPastMargin;
        }

        if (_avoiding) p.x = Mathf.MoveTowards(from.x, _avoidX, AvoidSideSpeed * dt);
    }

    private const float CameraClearAhead = 2.2f;
    private const float CameraClearSide = 3.5f;
    private Renderer[] _bodyRenderers;
    private bool _hiddenByCamera;

    /// <summary>Police running past the camera would fill the screen, so they stay hidden until they are clear of the lens.</summary>
    private void UpdateCameraVisibility(Vector3 p)
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 c = cam.transform.position;
        bool hide = p.z < c.z + CameraClearAhead && Mathf.Abs(p.x - c.x) < CameraClearSide;
        if (hide == _hiddenByCamera) return;
        _hiddenByCamera = hide;

        if (_bodyRenderers == null)
        {
            var all = new System.Collections.Generic.List<Renderer>();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                if (r.enabled && r != _muzzleFlash && !(r is LineRenderer)) all.Add(r); // the shooting code owns those; parts hidden on purpose stay hidden
            _bodyRenderers = all.ToArray();
        }
        foreach (var r in _bodyRenderers) if (r != null) r.enabled = !hide;
    }

    private bool CanCatchAJ =>kind != Kind.Chaser && kind != Kind.Dog && (kind != Kind.Pursuer || _chasingAJ);

    private void Behave(ref Vector3 p, PlayerLaneMovement claire, float dt)
    {
        float cz = claire.transform.position.z;
        if ((kind == Kind.Interceptor || (kind == Kind.Sidewalk && state == State.InLane)) && UpdateShooting(claire, p))
            return;

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
                if (!_director.LaneStaysFair(this, lane, p.z)) GoToCurb();
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
                        else GoToCurb();
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
        if (UpdateShooting(claire, p)) return;

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
    /// Laser sight, then a volley of bullets. Guards fire straight down their own lane, so the roadblock's gap stays
    /// safe; other officers aim where Claire was when the laser ended, so changing lane dodges. Returns true while shooting.
    /// </summary>
    private bool UpdateShooting(PlayerLaneMovement claire, Vector3 p)
    {
        if (_muzzleFlash != null && _muzzleFlash.enabled && Time.time >= _flashOffAt) _muzzleFlash.enabled = false;

        Vector3 muzzle = _muzzleFlash != null ? _muzzleFlash.transform.position : p + Vector3.up * 1.3f;
        Vector3 target = AimPoint(claire, muzzle);

        switch (_shooting)
        {
            case Shooting.Aiming:
                _laser.SetPosition(0, muzzle);
                _laser.SetPosition(1, target);
                if (Time.time >= _aimEndsAt)
                {
                    _laser.enabled = false;
                    _shooting = Shooting.Firing;
                    _shotsLeft = _director.volleyShots;
                    _nextShotAt = 0f;
                    _animator.CrossFadeInFixedTime(FireState, 0.08f);
                }
                return true;

            case Shooting.Firing:
                if (Time.time >= _nextShotAt)
                {
                    _nextShotAt = Time.time + _director.shotInterval;
                    _muzzleFlash.enabled = true;
                    _flashOffAt = Time.time + 0.05f;
                    _director.FireBullet(muzzle, target);
                    if (--_shotsLeft <= 0)
                    {
                        EndShooting();
                        SetRunning(kind != Kind.Guard);
                        return false;
                    }
                }
                return true;
        }

        float ahead = p.z - claire.transform.position.z;
        float closest = kind == Kind.Guard ? _director.guardLungeDistance : _director.shootMinDistance;
        if (_volleyDone || Time.time < _nextVolleyAt || _muzzleFlash == null || _laser == null || !_hasIdle
            || !_animator.HasState(0, FireState) || ahead > _director.shootRange || ahead < closest
            || !_director.TryClaimShooter())
            return false;

        _shooting = Shooting.Aiming;
        _aimEndsAt = Time.time + _director.aimSeconds;
        _volleyDone = kind == Kind.Guard;
        _laser.enabled = true;
        Face(180f);
        _animator.CrossFadeInFixedTime(IdleState, 0.1f);
        return true;
    }

    private Vector3 AimPoint(PlayerLaneMovement claire, Vector3 muzzle)
    {
        Vector3 c = claire.transform.position;
        float x = kind == Kind.Guard ? _postX : c.x;
        float flight = Mathf.Max(0f, muzzle.z - c.z) / Mathf.Max(1f, _director.bulletSpeed + claire.forwardSpeed);
        return new Vector3(x, c.y + 1.2f, c.z + claire.forwardSpeed * flight);
    }

    private void EndShooting()
    {
        if (_shooting == Shooting.None) return;
        _shooting = Shooting.None;
        _nextVolleyAt = Time.time + Random.Range(3.5f, 6f);
        if (_laser != null) _laser.enabled = false;
        if (_director != null) _director.ReleaseShooter();
    }

    private void OnDestroy() => EndShooting();

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
        EndShooting();
        if (_muzzleFlash != null) _muzzleFlash.enabled = false;
        state = State.Stunned;
        _stunUntil = Time.time + Mathf.Max(0.1f, seconds);
        SetRunning(false);
    }

    /// <summary>The lane must stay open (fairness): take a stand at the curb, facing the runners, gun ready.</summary>
    public void GoToCurb()
    {
        EndShooting();
        state = State.Holding;
        _atCurb = true;
        lane = -1;
        _holdX = transform.position.x < 0f ? -_director.SidewalkX : _director.SidewalkX;
        Face(180f);
    }

    /// <summary>During a rescue posted officers wait, facing the runners, and do not interfere.</summary>
    public void HoldForRescue(bool stepOutOfLane)
    {
        if (state == State.Leaving) return;
        if (!HoldsGround)
        {
            BeginLeaving();
            return;
        }
        if (state == State.Holding && !stepOutOfLane) return;

        EndShooting();
        if (_muzzleFlash != null) _muzzleFlash.enabled = false;
        state = State.Holding;
        float x = transform.position.x;
        _holdX = stepOutOfLane ? x + (x <= 0f ? -1.8f : 1.8f) : kind == Kind.Guard ? _postX : x;
        Face(180f);
        SetRunning(false);
    }

    /// <summary>Back to work after a rescue. Officers at the curb stay there.</summary>
    public void ResumeAfterRescue()
    {
        if (state == State.Holding && !_atCurb) ReturnToDuty();
    }

    private void ReturnToDuty()
    {
        if (!HoldsGround)
        {
            BeginLeaving();
            return;
        }
        if (_atCurb || (kind != Kind.Guard && lane < 0))
        {
            GoToCurb();
            return;
        }
        state = State.InLane;
        Face(180f);
        SetRunning(kind != Kind.Guard);
    }

    public void BeginLeaving()
    {
        state = State.Leaving;
        EndShooting();
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
