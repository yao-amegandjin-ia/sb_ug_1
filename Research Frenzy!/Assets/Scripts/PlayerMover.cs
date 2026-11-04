using System;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMover : MonoBehaviour
{
    public float speed = 3f;
    public float stopDistance = 0.05f;

    Rigidbody2D rb;
    Vector2 target;
    Vector2 lastPos;
    bool moving;
    float stuckTimer;

    // for walking up to something
    Transform follow;
    bool following;
    float arriveDistance;
    Action onArrive;

    // NEW: animation
    Animator anim;
    float facingAngle;   // remembered so we keep facing the same way when we stop
    static readonly int FacingAngleHash = Animator.StringToHash("FacingAngle");
    static readonly int SpeedHash = Animator.StringToHash("Speed");

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        anim = GetComponentInChildren<Animator>();   // NEW (works if Animator is on a child sprite object too)
    }

    void Start()
    {
        target = rb.position;
        lastPos = rb.position;
    }

    void FixedUpdate()
    {
        if (!moving)
        {
            Debug.Log($"moving pos={rb.position} target={target}", this);
            rb.linearVelocity = Vector2.zero;
            UpdateAnimator(Vector2.zero);   // NEW
            return;
        }

        if (following)
        {
            if (!follow) { Stop(); return; }
            target = follow.position;
        }

        Vector2 toTarget = target - rb.position;

        if (toTarget.magnitude <= arriveDistance)
        {
            Action done = onArrive;
            Face(toTarget);                 // NEW: turn toward what we walked up to
            Stop();
            done?.Invoke();
            return;
        }

        float step = Mathf.Min(speed, toTarget.magnitude / Time.fixedDeltaTime);
        Vector2 dir = toTarget.normalized;
        rb.linearVelocity = dir * step;
        Debug.Log($"vel {rb.linearVelocity} moving {moving}");

        UpdateAnimator(dir);                // NEW

        if ((rb.position - lastPos).sqrMagnitude < 0.00001f)
        {
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer > 0.3f) Stop();
        }
        else
        {
            stuckTimer = 0f;
        }

        lastPos = rb.position;
    }

    public void MoveTo(Vector2 point)
    {
        Debug.Log("MoveTo called: " + point, this);
        following = false;
        follow = null;
        onArrive = null;
        arriveDistance = stopDistance;

        target = point;
        moving = true;
        stuckTimer = 0f;
    }

    public void WalkUpTo(Transform thing, float distance, Action arrived)
    {
        following = true;
        follow = thing;
        arriveDistance = distance;
        onArrive = arrived;

        moving = true;
        stuckTimer = 0f;
    }

    void Stop()
    {
        moving = false;
        following = false;
        follow = null;
        onArrive = null;
        rb.linearVelocity = Vector2.zero;
        UpdateAnimator(Vector2.zero);       // NEW
    }

    // NEW: remember which way we're facing (front = down on screen, clockwise)
    void Face(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.0001f) return;
        facingAngle = Mathf.Atan2(dir.x, -dir.y) * Mathf.Rad2Deg;
        if (facingAngle < 0f) facingAngle += 360f;
    }

    // NEW: push values to the Animator
    void UpdateAnimator(Vector2 dir)
    {
        if (!anim) return;
        Face(dir);
        anim.SetFloat(FacingAngleHash, facingAngle);
        anim.SetFloat(SpeedHash, dir.magnitude);
        anim.speed = dir.sqrMagnitude > 0.0001f ? 1f : 0f;   // freeze when not moving
    }
}