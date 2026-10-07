// PlayerMover - goes on the player.
// walks the player toward a floor click, or up to something (like a client) and then does something.
// uses a Rigidbody2D so anything with a (non-trigger) collider, like the tables, blocks the way.
// no pathfinding yet, so if something's in the way the player just stops against it.

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

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;      // top down, no falling
        rb.freezeRotation = true;  // don't spin when bumping into stuff
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
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (following)
        {
            // the thing we were walking to got removed (client left)
            if (follow == null) { Stop(); return; }
            target = follow.position;   // keep up if it's still moving
        }

        Vector2 toTarget = target - rb.position;

        if (toTarget.magnitude <= arriveDistance)
        {
            Action done = onArrive;
            Stop();
            done?.Invoke();
            return;
        }

        // slow down on the last step so we don't overshoot
        float step = Mathf.Min(speed, toTarget.magnitude / Time.fixedDeltaTime);
        rb.linearVelocity = toTarget.normalized * step;

        // pushing into a table and not going anywhere -> give up
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

    // floor click - just walk there
    public void MoveTo(Vector2 point)
    {
        following = false;
        follow = null;
        onArrive = null;
        arriveDistance = stopDistance;

        target = point;
        moving = true;
        stuckTimer = 0f;
    }

    // walk until we're close enough to something, then call arrived
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
    }
}