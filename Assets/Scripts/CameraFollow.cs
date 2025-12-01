using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target;        // 追従対象（プレイヤー）
    public Vector3 offset = new Vector3(0f, 50f, -60f); // 上・後ろから俯瞰する位置
    public float followSpeed = 5f;
    public float rotateSpeed = 5f;

    void LateUpdate()
    {
        if (target == null) return;

        // ① ターゲットの「後ろ上」の位置へ移動
        Vector3 desiredPosition = target.position + target.transform.TransformDirection(offset);
        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            Time.deltaTime * followSpeed
        );

        // ② ターゲットを見る
        Quaternion targetRotation = Quaternion.LookRotation(target.position - transform.position);
        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            targetRotation,
            Time.deltaTime * rotateSpeed
        );
    }

    // プレイヤー変更時に外部から呼び出す
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
