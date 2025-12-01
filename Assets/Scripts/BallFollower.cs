using UnityEngine;

public class BallFollower : MonoBehaviour
{
    public Transform followTarget;   // ballHoldPoint を渡す
    public bool isHeld = false;      // ボールを持っているかどうか

    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void LateUpdate()
    {
        if (isHeld && followTarget != null)
        {
            // 物理の干渉を防ぐ
            rb.isKinematic = true;

            // 子にしなくても手元に追従できる
            transform.position = followTarget.position;
            transform.rotation = followTarget.rotation;
        }
        else
        {
            rb.isKinematic = false; // 手放した時に物理を復帰
        }
    }
}
