using UnityEngine;

/// <summary>
/// One police officer. Distinct from LaneObstacle: officers move, pursue, intercept, react to the
/// Distraction Pulse and grab characters (distance checks, no physics). Spawned and owned by PoliceDirector.
/// </summary>
public class PoliceOfficer : MonoBehaviour
{
    public enum Kind { Chaser, Interceptor, Sidewalk }
    public enum State { Approaching, InLane, Stunned, Leaving }

    public Kind kind;
    public State state = State.Approaching;
    public int lane = -1;                 // current/target lane (-1 = on the sidewalk)

    /// <summary>True when AJ's dodging AI should treat this officer like a blocked lane.</summary>
    public bool BlocksLaneForAI =>
        state != State.Stunned && state != State.Leaving && kind != Kind.Chaser && lane >= 0;

    public bool IsDangerous => state == State.Approaching || state == State.InLane;

    private PoliceDirector _director;
    private Animator _animator;
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
        if (_animator != null) _animator.applyRootMotion = false;
        state = State.Approaching;
        Face(kind == Kind.Interceptor ? 180f : kind == Kind.Sidewalk ? (sidewalkX < 0f ? 90f : -90f) : 0f);
        SetRunning(kind != Kind.Sidewalk);
    }

    private void Update()
    {
        if (!GameStartFlow.IsGameplayActive || _director == null)
        {
            if (_animator != null) _animator.speed = 0f; // freeze pose while paused / after the run
            return;
        }
        if (_animator != null) _animator.speed = _running ? 1f : 0f;

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
            else if (aj != null && !aj.IsCaught && !aj.IsInvulnerable && kind != Kind.Chaser && Reaches(aj.transform.position, p))
            {
                _director.OnOfficerGrabbedAJ(this);
                state = State.InLane;
            }
        }

        // Despawn far behind / far ahead.
        float cz = claire.transform.position.z;
        if (p.z < cz - _director.despawnBehind || p.z > cz + _director.despawnAhead)
            _director.Despawn(this);
    }

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

    private bool Reaches(Vector3 target, Vector3 p)
    {
        return Mathf.Abs(target.x - p.x) < _director.grabRadiusX && Mathf.Abs(target.z - p.z) < _director.grabRadiusZ;
    }

    /// <summary>Distraction Pulse / after a grab: stop, no grabbing, then walk off.</summary>
    public void Stun(float seconds)
    {
        if (state == State.Leaving) return;
        state = State.Stunned;
        _stunUntil = Time.time + Mathf.Max(0.1f, seconds);
        SetRunning(false);
    }

    public void BeginLeaving()
    {
        state = State.Leaving;
        lane = -1;
        _leaveSideX = transform.position.x < 0f ? -_director.SidewalkX : _director.SidewalkX;
        SetRunning(true);
    }

    private void SetRunning(bool running)
    {
        _running = running;
        if (_animator != null) _animator.speed = running ? 1f : 0f;
    }

    private void Face(float yaw)
    {
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }
}
