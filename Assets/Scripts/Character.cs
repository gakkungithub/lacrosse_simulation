using UnityEngine;

public class Character : MonoBehaviour
{
    public bool isPlayer;
    public float pickupRange = 5.0f;            // 拾える距離
    public float pickupAngle = 60f;             // 前方何度まで拾えるか
    public float moveSpeed = 1.0f;
    public float rotateSpeed = 100f; 
    public float throwPower = 50.0f;
    public float jumpPower = 20.0f;
    public GameObject heldBall = null;    // 持っているボール
    public Material defaultMaterial;        // 普段のマテリアル
    public Material playerMaterial;        // プレイヤーになったときのマテリアル
    public LayerMask ballLayer;               // ボール用レイヤー
    public LayerMask groundLayer;           // 地面用レイヤー

    private Transform rightHand;
    private Transform leftHand;
    public float swingAmplitude = 1.0f; // 前後に揺れる距離
    public float swingFrequency = 0.4f;   // 揺れる速さ
    private Vector3 rightHandStartPos;
    private Vector3 leftHandStartPos;

    private Rigidbody rb;
    public bool isGrounded;

    public GameManager.FieldArea fieldArea;
    private MeshRenderer[] childRenderers;      // 子オブジェクトの Renderer 配列

    public enum HandType {
        Right,
        Left
    }

    public HandType currentHand = HandType.Right;

    private Animator charaAnim;
    private Animator rightHandAnim;
    private Animator leftHandAnim;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void Start()
    {
        rightHand = transform.Find("RightHand");
        rightHandStartPos = rightHand.localPosition;
        leftHand  = transform.Find("LeftHand");
        leftHandStartPos = leftHand.localPosition;
        // 子オブジェクトの Renderer をすべて取得
        childRenderers = GetComponentsInChildren<MeshRenderer>();

        // 初期マテリアルを設定
        foreach (var r in childRenderers)
        {
            r.material = defaultMaterial;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        charaAnim = this.GetComponent<Animator>();
        rightHandAnim = transform.Find("RightHand").GetComponent<Animator>();
        leftHandAnim  = transform.Find("LeftHand").GetComponent<Animator>();
    }

    void Update()
    {
        // ボールを持っているかで Material を切り替え
        foreach (var r in childRenderers)
        {
            r.material = isPlayer ? playerMaterial : defaultMaterial;
        }

        if (!isPlayer) return;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // 回転
        transform.Rotate(0, h * rotateSpeed * Time.deltaTime, 0);

        // 移動
        Vector3 forwardMove = transform.forward * v;
        Vector3 horizontalVelocity = forwardMove * moveSpeed;
        horizontalVelocity.y = rb.linearVelocity.y; // 落下速度を残す
        rb.linearVelocity = horizontalVelocity;

        // ---- ジャンプ ----
        if (Input.GetKeyDown(KeyCode.J) && isGrounded)
        {
            isGrounded = false;
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z); // 初速リセット
            rb.AddForce(Vector3.up * jumpPower, ForceMode.VelocityChange);
            charaAnim.SetTrigger("Jump");
        }

        // ---- 拾う ----
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TryPickupBallWithRaycast();
        }
        else if ((Input.GetKeyDown(KeyCode.Q) && currentHand == HandType.Right) ||
                (Input.GetKeyDown(KeyCode.E) && currentHand == HandType.Left))
        {
            SwitchHand();
        }
    }

    void TryPickupBallWithRaycast()
    {
        if (heldBall != null) {
            if (currentHand == HandType.Right) {
                rightHandAnim.SetTrigger("RightThrow");
            } else {
                leftHandAnim.SetTrigger("LeftThrow");
            }
            heldBall.transform.SetParent(null);
            // 物理を再び有効化
            Rigidbody rb_ball = heldBall.GetComponent<Rigidbody>();
            rb_ball.isKinematic = false;
            // 投げる力を加える
            rb_ball.AddForce(transform.forward * throwPower + Vector3.up * 4.0f, ForceMode.Impulse);
            heldBall = null;
        } 
        else 
        {
            // 足元と頭上の位置
            Vector3 bottom = transform.position + Vector3.down * 2.0f; // 足元（キャラクターの中心y座標なら高さ調整）
            Vector3 top = transform.position + Vector3.up * 2.0f; // キャラクターの身長分

            // キャラクター周りの一定範囲にあるオブジェクトを取得
            Collider[] colliders = Physics.OverlapCapsule(bottom, top, pickupRange, ballLayer);

            foreach (Collider col in colliders)
            {
                // ボールタグのオブジェクトだけ拾う
                Vector3 dirToBall = col.transform.position - transform.position;

                // Y軸成分を無視してXZ平面の方向だけを見る
                dirToBall.y = 0;

                // forward も Y成分を無視した水平前方向ベクトルに
                Vector3 forwardFlat = transform.forward;
                forwardFlat.y = 0;

                // 正規化して角度計算
                float angle = Vector3.Angle(forwardFlat.normalized, dirToBall.normalized);

                if (angle <= pickupAngle / 2f)
                {
                    Pickup(col.gameObject);
                    break;  // 一度に1つだけ拾う
                }
            }
    
        }
    }

    void Pickup(GameObject ball)
    {
        Rigidbody rb_ball = ball.GetComponent<Rigidbody>();
        if (rb_ball == null) return;

        heldBall = ball;
        rb_ball.isKinematic = true;

        // currentHand に応じて手の Transform を取得
        Transform handPoint = currentHand == HandType.Right ? rightHand : leftHand;

        // 親子関係を設定して手の位置・回転に合わせる
        ball.transform.SetParent(handPoint);
        ball.transform.localPosition = new Vector3(0f, 1.0f, 0f);
        ball.transform.localRotation = Quaternion.identity;
    }

    void SwitchHand()
    {
        // 次に切り替える手
        HandType nextHand =
            (currentHand == HandType.Right)
            ? HandType.Left
            : HandType.Right;

        if (heldBall != null)
        {
            // ボールを次の手へ移す
            Transform nextHandPoint = nextHand == HandType.Right ? rightHand : leftHand;
            // nextHandPoint.y -= 1.0f;
            heldBall.transform.SetParent(nextHandPoint);
            heldBall.transform.localPosition = new Vector3(0f, 1f, 0f);
            heldBall.transform.localRotation = Quaternion.identity;
        }

        // 最後に現在の手を更新
        currentHand = nextHand;
    }

    void OnCollisionEnter(Collision other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            isGrounded = true;
        } 
    }
}
