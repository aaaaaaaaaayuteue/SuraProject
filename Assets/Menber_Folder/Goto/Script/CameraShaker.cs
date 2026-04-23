using UnityEngine;

public class CameraShaker : MonoBehaviour
{
    [Header("シェイク時間(秒)のデフォルト値")]
    [SerializeField] float defaultDuration = 0.15f;
    [Header("シェイクの揺れ幅のデフォルト値")]
    [SerializeField] float defaultMagnitude = 0.15f;

    // シングルトンパターン：他のスクリプトから簡単に呼び出せるように、静的なインスタンス参照を持つ
    public static CameraShaker Instance { get; private set; }

    private Vector3 originalLocalPosition;  // 起動時の元のローカル位置(揺れ終わった後に戻すため)
    private float shakeRemainingTime = 0f;  // 残りシェイク時間
    private float shakeTotalDuration = 0f;  // シェイク開始時の総時間(減衰率の計算用)
    private float currentMagnitude = 0f;    // 現在のシェイクの揺れ幅

    private void Awake()
    {
        // シングルトンのインスタンスを自分に設定する(他のスクリプトから CameraShaker.Instance.Shake() で呼べる)
        Instance = this;

        // 起動時の元のローカル位置を記録する
        originalLocalPosition = transform.localPosition;
    }

    private void LateUpdate()
    {
        // LateUpdateでシェイクを処理する(カメラ追従スクリプトがあった場合、その後にシェイクを上乗せするため)
        if (shakeRemainingTime > 0f)
        {
            // 残り時間の割合を計算する(1.0で開始、0で終了)
            float remainingRatio = shakeRemainingTime / shakeTotalDuration;

            // 揺れ幅を時間経過で減衰させる(開始時が最大、終了時がゼロになる)
            float decayedMagnitude = currentMagnitude * remainingRatio;

            // 減衰後の揺れ幅でランダムなオフセットを計算する(2D用にXYのみ揺らし、Zは0)
            Vector3 shakeOffset = new Vector3(
                Random.Range(-1f, 1f) * decayedMagnitude,
                Random.Range(-1f, 1f) * decayedMagnitude,
                0f
            );

            // 元の位置にオフセットを加えて揺れを表現する
            transform.localPosition = originalLocalPosition + shakeOffset;

            // 残り時間を減らす
            shakeRemainingTime -= Time.deltaTime;
        }
        else
        {
            // 揺れ終わったら元の位置に戻す
            transform.localPosition = originalLocalPosition;
        }
    }

    // 外部から呼ぶ用：デフォルトの強さと時間でシェイクを開始する
    public void Shake()
    {
        Shake(defaultDuration, defaultMagnitude);
    }

    // 外部から呼ぶ用：指定の強さと時間でシェイクを開始する
    public void Shake(float duration, float magnitude)
    {
        shakeRemainingTime = duration;
        shakeTotalDuration = duration;
        currentMagnitude = magnitude;
    }
}