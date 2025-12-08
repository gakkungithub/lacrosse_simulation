using UnityEngine;

public class Ball : MonoBehaviour
{
    private Rigidbody rb;

    public GameManager.FieldArea fieldArea;

    public bool isGrounded = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            isGrounded = true;
        } 
    }
}
