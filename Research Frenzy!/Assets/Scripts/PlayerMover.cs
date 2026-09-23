using UnityEngine;

public class PlayerMover : MonoBehaviour
{
    public float speed = 3f;

    Vector2 target;

    void Start()
    {
        target = transform.position;
    }

    void Update()
    {
        transform.position = Vector2.MoveTowards(transform.position, target, speed * Time.deltaTime);
    }

    public void MoveTo(Vector2 point)
    {
        target = point;
    }
}
