using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(AudioSource))]
public class PlayerController : MonoBehaviour
{
    [Header("左右の移動速度")]
    [SerializeField] float moveSpeed = 5f;
    [Header("ジャンプの最高到達点(この高さまで飛ぶ。重力変えても自動で調整される)")]
    [SerializeField] float jumpHeight = 3f;
    [Header("大ジャンプの最高到達点(チャージジャンプ時の高さ)")]
    [SerializeField] float chargeJumpHeight = 8f;
    [Header("チャージが完了するまでにかかる時間(秒)")]
    [SerializeField] float chargeTime = 0.6f;
    [Header("大ジャンプ発動時のスケール(Xを縮めてYを伸ばす。縦長の発射ポーズ)")]
    [SerializeField] Vector3 chargeJumpStartScale = new Vector3(0.6f, 1.6f, 1f);
    [Header("大ジャンプ最高到達点のスケール(ちょっとXが伸びてYが縮む形)")]
    [SerializeField] Vector3 chargeJumpPeakScale = new Vector3(1.15f, 0.85f, 1f);
    [Header("最高到達点までの高さの何分の1から戻り始めるか(2なら1/2地点から、3なら1/3地点から戻り始める。大きいほど早く戻り始める)")]
    [SerializeField] int chargeJumpScaleReturnDivisor = 2;
    [Header("大ジャンプの最高到達点到達後、しゃがみ入力を無視する時間(秒)")]
    [SerializeField] float squashLockAfterPeak = 0.1f;
    [Header("チャージ完了時のプレイヤーの色")]
    [SerializeField] Color chargeReadyColor = Color.yellow;
    [Header("接地判定の下側チェック範囲の厚み")]
    [SerializeField] float groundCheckThickness = 0.05f;
    [Header("接地判定の左右の余白(壁に横から当たってる時に誤判定しないように狭める)")]
    [SerializeField] float groundCheckSideMargin = 0.05f;
    [Header("接地判定で地面とみなすレイヤー")]
    [SerializeField] LayerMask groundLayer;
    [Header("天井判定の上側チェック範囲の厚み")]
    [SerializeField] float ceilingCheckThickness = 0.05f;
    [Header("天井判定の左右の余白")]
    [SerializeField] float ceilingCheckSideMargin = 0.05f;
    [Header("縮みきった時のスケール(見た目の潰れ具合。Yを小さく、Xを大きくすると潰れた感じになる)")]
    [SerializeField] Vector3 squashedScale = new Vector3(1.3f, 0.4f, 1f);
    [Header("縮みきった時のColliderのY縮み率(0.9なら見た目の90%の高さになる。ちょっと小さめにすると天井に引っかかりにくい)")]
    [SerializeField] float squashedColliderYRatio = 0.9f;
    [Header("縮むのにかかる時間(秒)")]
    [SerializeField] float squashDuration = 0.1f;
    [Header("元の大きさに戻るのにかかる時間(秒)")]
    [SerializeField] float stretchDuration = 0.1f;
    [Header("下端を基準に縮むか(OFFで中心基準で縮む)")]
    [SerializeField] bool squashFromBottom = true;

    [Header("ーーーーーーー ここから下は破壊ブロック関連 ーーーーーーー")]
    [Header("破壊可能ブロックのタグ")]
    [SerializeField] string breakableBlockTag = "Block";
    [Header("下から突き上げたと判定する角度の余裕(度)")]
    [SerializeField] float hitFromBelowAngleTolerance = 45f;
    [Header("ブロック破壊時に生成するエフェクトのプレハブ")]
    [SerializeField] GameObject blockBreakParticlePrefab;
    [Header("ブロック破壊時のカメラ揺れ時間(秒)")]
    [SerializeField] float blockBreakShakeDuration = 0.15f;
    [Header("ブロック破壊時のカメラ揺れ幅")]
    [SerializeField] float blockBreakShakeMagnitude = 0.15f;

    [Header("ーーーーーーー ここから下はエフェクト関連 ーーーーーーー")]
    [Header("チャージ完了時に生成するエフェクトのプレハブ")]
    [SerializeField] GameObject chargeReadyParticlePrefab;
    [Header("大ジャンプ発動時に生成するエフェクトのプレハブ")]
    [SerializeField] GameObject chargeJumpParticlePrefab;

    [Header("ーーーーーーー ここから下は音関連 ーーーーーーー")]
    [Header("しゃがみ始めた時に鳴らす音")]
    [SerializeField] AudioClip squashSound;
    [Header("チャージ完了時に鳴らす音")]
    [SerializeField] AudioClip chargeReadySound;
    [Header("大ジャンプ発動時に鳴らす音")]
    [SerializeField] AudioClip chargeJumpSound;
    [Header("ブロック破壊時に鳴らす音")]
    [SerializeField] AudioClip blockBreakSound;

    private BoxCollider2D boxCollider;      // BoxCollider2Dの参照
    private Rigidbody2D rb;                 // Rigidbody2Dの参照
    private SpriteRenderer spriteRenderer;  // SpriteRendererの参照
    private AudioSource audioSource;        // AudioSourceの参照
    private Vector3 normalScale;            // 起動時に記録する通常スケール
    private float normalColliderSizeY;      // 起動時に記録する通常Colliderサイズ Y
    private float normalColliderOffsetY;    // 起動時に記録する通常Colliderオフセット Y
    private Color normalColor;              // 起動時に記録する通常色
    private float squashProgress = 0f;      // 0で通常、1で縮みきった状態
    private float chargeProgress = 0f;      // チャージの進捗(0で未チャージ、1で完了)
    private bool isChargeJumping = false;   // 現在大ジャンプ中かどうか(発動から最高到達点まで)
    private float chargeJumpInitialVelocity = 0f; // 大ジャンプの初速度(スケール補間用に記憶)
    private float squashLockTimer = 0f;     // 大ジャンプ後にしゃがみ入力を無視する残り時間
    private Vector3 peakScaleSnapshot;      // 最高到達点到達時のスケール(そこから通常スケールへ補間するため記録)
    private bool wasSquashingLastFrame = false;    // 前フレームでしゃがみ入力してたかどうか(しゃがみ始めの検出用)
    private bool wasChargeReadyLastFrame = false;  // 前フレームでチャージ完了してたかどうか(チャージ完了瞬間の検出用)
    private Vector2 velocityBeforeCollision;       // 衝突直前のvelocityを記憶(ブロック破壊時の速度復元用)

    // 縮みきっているかを外部から参照できるようにプロパティを追加
    public bool IsFullySquashed => squashProgress >= 1f;

    // 縮み進捗(0〜1)を外部から参照できるようにプロパティを追加
    public float SquashProgress => squashProgress;

    // チャージが完了しているかを外部から参照できるようにプロパティを追加
    public bool IsChargeReady => chargeProgress >= 1f;

    // チャージ進捗(0〜1)を外部から参照できるようにプロパティを追加
    public float ChargeProgress => chargeProgress;

    // 大ジャンプ中かを外部から参照できるようにプロパティを追加
    public bool IsChargeJumping => isChargeJumping;

    // 接地しているかを外部から参照できるようにプロパティを追加
    public bool IsGrounded => CheckGrounded();

    // 天井に当たっているかを外部から参照できるようにプロパティを追加
    public bool HasCeiling => CheckCeiling();

    private void Start()
    {
        // 各コンポーネントを取得
        boxCollider = GetComponent<BoxCollider2D>();
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        audioSource = GetComponent<AudioSource>();

        // 起動時のスケールとColliderサイズと色を通常値として記録する
        normalScale = transform.localScale;
        normalColliderSizeY = boxCollider.size.y;
        normalColliderOffsetY = boxCollider.offset.y;
        normalColor = spriteRenderer.color;
    }

    private void Update()
    {
        // ーーー大ジャンプ後のしゃがみ入力ロックタイマーを減らすーーー
        if (squashLockTimer > 0f)
        {
            squashLockTimer -= Time.deltaTime;
        }

        // ーーーしゃがみ入力判定ーーー
        // SキーまたはDownArrowキーで縮むかどうかを判定する(ただしロック中は入力無視)
        bool isHoldingSquash = (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) && squashLockTimer <= 0f;

        // 上に天井があって伸びきれない時は、キーを離しても縮んだままにする
        bool shouldStaySquashed = isHoldingSquash || CheckCeiling();

        // ーーースケール適用の分岐ーーー
        // 大ジャンプ中・大ジャンプ後の復帰中・通常時でそれぞれ別のスケール計算を行う
        if (isChargeJumping)
        {
            // 大ジャンプ中：発射時スケール→最高到達点スケールに補間する
            UpdateChargeJumpScale();

            // 最高到達点に到達したかどうかをチェックする
            if (rb.linearVelocity.y <= 0f)
            {
                // 最高到達点に到達：大ジャンプ終了、現在のスケールを記録してロック開始
                isChargeJumping = false;
                peakScaleSnapshot = transform.localScale;
                squashLockTimer = squashLockAfterPeak;
            }
        }
        else if (squashLockTimer > 0f)
        {
            // 大ジャンプ後のロック中：最高到達点スケールから通常スケールへ補間する
            UpdatePostJumpScale();
        }
        else
        {
            // 通常時：squashProgressに基づく縮み/伸び処理
            UpdateSquashProgress(shouldStaySquashed);
            ApplySquash();
        }

        // ーーーチャージ処理ーーー
        UpdateCharge(isHoldingSquash);

        // ーーーチャージ完了状態に応じた色を適用するーーー
        ApplyChargeColor();

        // ーーー効果音とエフェクトの再生判定ーーー
        PlaySoundEffects(isHoldingSquash);

        // ーーー左右移動処理ーーー
        HorizontalMove();

        // ーーージャンプ処理ーーー
        if (Input.GetKeyDown(KeyCode.Space) && CheckGrounded())
        {
            // チャージ完了中なら大ジャンプ、そうでなければ通常ジャンプを発動する
            if (IsChargeReady)
            {
                ChargeJump();
            }
            else
            {
                Jump();
            }
        }

        // 次のフレームで「状態が変わった瞬間」を検出するため、現在の状態を記録しておく
        wasSquashingLastFrame = isHoldingSquash;
        wasChargeReadyLastFrame = IsChargeReady;
    }

    // 物理演算タイミングで呼ばれる(衝突前の速度を記録するため)
    private void FixedUpdate()
    {
        // 物理衝突が処理される前の時点でのvelocityを記録しておく
        // (OnCollisionEnter2Dは衝突解決後に呼ばれるので、その時点では既に減速しているため事前に記録が必要)
        velocityBeforeCollision = rb.linearVelocity;
    }

    // 何かと衝突した時に呼ばれる(ブロック破壊判定)
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 大ジャンプ中でなければ何もしない
        if (!isChargeJumping) return;

        // 衝突相手が破壊可能ブロックでなければ何もしない
        if (!collision.collider.CompareTag(breakableBlockTag)) return;

        // 下から突き上げた衝突でなければ何もしない
        if (!IsHitFromBelow(collision)) return;

        // ーーーブロックを破壊するーーー
        BreakBlock(collision.collider.gameObject);
    }

    // 下からの衝突かどうかを判定する
    private bool IsHitFromBelow(Collision2D collision)
    {
        // プレイヤーがブロックの下面に衝突した場合、その接触面の法線は下向き(Vector2.down)になる
        foreach (ContactPoint2D contact in collision.contacts)
        {
            float angleFromDown = Vector2.Angle(contact.normal, Vector2.down);

            // 許容角度内なら下からの衝突とみなす
            if (angleFromDown <= hitFromBelowAngleTolerance)
            {
                return true;
            }
        }

        return false;
    }

    // 指定のブロックを破壊する
    private void BreakBlock(GameObject block)
    {
        // 破壊エフェクトを生成する(ブロックの位置に発生)
        if (blockBreakParticlePrefab != null)
        {
            Instantiate(blockBreakParticlePrefab, block.transform.position, Quaternion.identity);
        }

        // 破壊音を再生する
        if (blockBreakSound != null)
        {
            audioSource.PlayOneShot(blockBreakSound);
        }

        // カメラを一瞬振動させる(ブロック破壊用の揺れパラメータを使う)
        if (CameraShaker.Instance != null)
        {
            CameraShaker.Instance.Shake(blockBreakShakeDuration, blockBreakShakeMagnitude);
        }

        // ブロックを削除する
        Destroy(block);

        // ーーー衝突前の速度を復元する(物理演算による減速をキャンセルして次のブロックへ貫通)ーーー
        rb.linearVelocity = velocityBeforeCollision;
    }

    // 効果音とエフェクトの再生判定処理(状態が変わった瞬間に音を鳴らしエフェクトを生成する)
    private void PlaySoundEffects(bool isHoldingSquash)
    {
        // しゃがみ始めた瞬間(前フレーム未入力→今フレーム入力)にしゃがみ音を鳴らす
        if (isHoldingSquash && !wasSquashingLastFrame && squashSound != null)
        {
            audioSource.PlayOneShot(squashSound);
        }

        // チャージ完了した瞬間(前フレーム未完了→今フレーム完了)に音とエフェクトを発生させる
        if (IsChargeReady && !wasChargeReadyLastFrame)
        {
            // チャージ完了音を鳴らす
            if (chargeReadySound != null)
            {
                audioSource.PlayOneShot(chargeReadySound);
            }

            // チャージ完了エフェクトを生成する(プレイヤー位置に発生)
            if (chargeReadyParticlePrefab != null)
            {
                Instantiate(chargeReadyParticlePrefab, transform.position, Quaternion.identity);
            }
        }
    }

    // 左右移動処理(AD・左右矢印キーに対応)
    private void HorizontalMove()
    {
        // 水平方向の入力を取得する(-1〜1の値、AとLeftArrowで-1、DとRightArrowで+1)
        float x = Input.GetAxisRaw("Horizontal");

        // 現在のY速度(重力の落下とか)は保ったまま、X方向の速度だけ上書きする
        rb.linearVelocity = new Vector2(x * moveSpeed, rb.linearVelocity.y);
    }

    // 通常ジャンプ処理(最高到達点から必要な初速度を逆算する)
    private void Jump()
    {
        // 実効重力を計算する(Project SettingsのGravityとRigidbodyのgravityScaleの両方を考慮)
        float effectiveGravity = Mathf.Abs(Physics2D.gravity.y) * rb.gravityScale;

        // 物理式 v = √(2gh) から、指定の高さに到達するために必要な初速度を求める
        float jumpVelocity = Mathf.Sqrt(2f * effectiveGravity * jumpHeight);

        // X方向の速度はそのままに、Y方向だけジャンプ速度で上書きする
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpVelocity);
    }

    // 大ジャンプ処理(通常ジャンプより高く飛ぶ。チャージ完了時のみ発動可能)
    private void ChargeJump()
    {
        // 実効重力を計算する(Project SettingsのGravityとRigidbodyのgravityScaleの両方を考慮)
        float effectiveGravity = Mathf.Abs(Physics2D.gravity.y) * rb.gravityScale;

        // 物理式 v = √(2gh) から、大ジャンプの高さに必要な初速度を求める
        float jumpVelocity = Mathf.Sqrt(2f * effectiveGravity * chargeJumpHeight);

        // X方向の速度はそのままに、Y方向だけ大ジャンプ速度で上書きする
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpVelocity);

        // 大ジャンプ状態を開始する(スケール補間のために初速度を記憶)
        isChargeJumping = true;
        chargeJumpInitialVelocity = jumpVelocity;

        // チャージを消費する
        chargeProgress = 0f;

        // squashProgressは通常時の縮み計算から切り離すので0にしておく
        squashProgress = 0f;

        // 大ジャンプ音を再生する
        if (chargeJumpSound != null)
        {
            audioSource.PlayOneShot(chargeJumpSound);
        }

        // 大ジャンプエフェクトを生成する(プレイヤー位置に発生)
        if (chargeJumpParticlePrefab != null)
        {
            Instantiate(chargeJumpParticlePrefab, transform.position, Quaternion.identity);
        }
    }

    // 大ジャンプ中のスケール補間処理(発射時スケール→最高到達点スケール)
    private void UpdateChargeJumpScale()
    {
        // 現在のY速度と初速度の比率を求める(発射直後は1、最高到達点では0)
        float velocityRatio = rb.linearVelocity.y / chargeJumpInitialVelocity;
        velocityRatio = Mathf.Clamp01(velocityRatio);

        // 現在の高さが最高到達点のどこまで進んだかを高さの比率で求める
        // 物理的に 現在高さ / 最高到達点 = 1 - velocityRatio^2 の関係が成り立つ
        float heightRatio = 1f - velocityRatio * velocityRatio;

        // 戻り始める高さ比率を計算する(divisor=2なら 1/2地点から、divisor=3なら 1/3地点から戻り始める)
        float returnStartHeightRatio = 1f / chargeJumpScaleReturnDivisor;

        float t;
        if (heightRatio < returnStartHeightRatio)
        {
            // まだ戻り始める地点に到達していない：発射時スケールを維持する
            t = 0f;
        }
        else
        {
            // 戻り始め地点から最高到達点までの区間で0→1に補間する
            t = (heightRatio - returnStartHeightRatio) / (1f - returnStartHeightRatio);
            t = Mathf.Clamp01(t);
        }

        // 発射時スケール→最高到達点スケールを、通常スケール基準でかけ合わせる
        Vector3 fromScale = Vector3.Scale(normalScale, chargeJumpStartScale);
        Vector3 toScale = Vector3.Scale(normalScale, chargeJumpPeakScale);
        transform.localScale = Vector3.Lerp(fromScale, toScale, t);

        // ーーーColliderとposition補正は通常時と同じロジックで最新スケールに合わせるーーー
        ApplyColliderAndPositionForCurrentScale();
    }

    // 大ジャンプ後のスケール補間処理(最高到達点スケール→通常スケール)
    private void UpdatePostJumpScale()
    {
        // 残りロック時間を進行度に変換する(ロック開始時0→終了時1)
        float t = 1f - (squashLockTimer / squashLockAfterPeak);
        t = Mathf.Clamp01(t);

        // 最高到達点スナップショットから通常スケールへ補間する
        transform.localScale = Vector3.Lerp(peakScaleSnapshot, normalScale, t);

        // ーーーColliderとposition補正は通常時と同じロジックで最新スケールに合わせるーーー
        ApplyColliderAndPositionForCurrentScale();
    }

    // 現在のlocalScaleに合わせてColliderとpositionを調整する(UpdateChargeJumpScale/UpdatePostJumpScaleから呼ばれる)
    private void ApplyColliderAndPositionForCurrentScale()
    {
        // 大ジャンプ関連ではsquashProgressを使わず、localScale.yから直接縮み率を割り出す
        // 通常スケールに対する現在のスケール比をそのままColliderサイズに反映する
        float scaleRatioY = transform.localScale.y / normalScale.y;
        float currentSizeY = normalColliderSizeY * scaleRatioY;

        // 下端を固定するためのoffset補正を計算する
        float normalBottomLocalY = normalColliderOffsetY - normalColliderSizeY / 2f;
        float currentOffsetY = normalBottomLocalY + currentSizeY / 2f;

        // Colliderのsizeとoffsetを適用する(sizeのXはそのまま、YとoffsetのYだけ変更)
        // ※localScaleによる自動拡縮があるので、size.yは"ローカル基準"で設定する必要があるためscaleRatioYで割る
        boxCollider.size = new Vector2(boxCollider.size.x, currentSizeY / scaleRatioY);
        boxCollider.offset = new Vector2(boxCollider.offset.x, currentOffsetY / scaleRatioY);
    }

    // チャージ処理(しゃがみ中はチャージが溜まり、しゃがみを解除すると消える)
    private void UpdateCharge(bool isHoldingSquash)
    {
        if (isHoldingSquash && IsFullySquashed)
        {
            // しゃがみキーを押している、かつ完全にしゃがんでいる時にチャージが溜まる
            chargeProgress += Time.deltaTime / chargeTime;
        }
        else if (!IsFullySquashed)
        {
            // 一度でも立ち上がるとチャージは消える
            chargeProgress = 0f;
        }

        // 0〜1の範囲に収める
        chargeProgress = Mathf.Clamp01(chargeProgress);
    }

    // チャージ完了状態に応じて色を切り替える
    private void ApplyChargeColor()
    {
        // チャージ完了なら指定色、それ以外は通常色
        spriteRenderer.color = IsChargeReady ? chargeReadyColor : normalColor;
    }

    // 接地判定(プレイヤーColliderの真下に地面があるかチェック)
    private bool CheckGrounded()
    {
        // プレイヤーColliderの下辺のすぐ下に、薄いチェックボックスを配置する
        Vector2 boxCenter = new Vector2(boxCollider.bounds.center.x, boxCollider.bounds.min.y - groundCheckThickness / 2f);

        // 横幅はプレイヤーColliderより少し狭くする(真横の壁に反応しないように)
        Vector2 boxSize = new Vector2(boxCollider.bounds.size.x - groundCheckSideMargin * 2f, groundCheckThickness);

        // チェックボックスの範囲内に地面レイヤーのColliderがあればtrueを返す
        return Physics2D.OverlapBox(boxCenter, boxSize, 0f, groundLayer) != null;
    }

    // 天井判定(伸びきった時にプレイヤーの頭上に障害物があるかチェック)
    private bool CheckCeiling()
    {
        // 現在の下端から通常スケールの高さ分まで伸びた時の、上端のY座標を計算する
        float bottomY = boxCollider.bounds.min.y;
        float normalTopY = bottomY + normalColliderSizeY * normalScale.y;

        // 伸びきった時の上端のすぐ上に、薄いチェックボックスを配置する
        Vector2 boxCenter = new Vector2(boxCollider.bounds.center.x, normalTopY + ceilingCheckThickness / 2f);

        // 横幅はプレイヤーColliderより少し狭くする(真横の壁に反応しないように)
        Vector2 boxSize = new Vector2(boxCollider.bounds.size.x - ceilingCheckSideMargin * 2f, ceilingCheckThickness);

        // チェックボックスの範囲内に地面レイヤー(=壁)のColliderがあればtrueを返す
        return Physics2D.OverlapBox(boxCenter, boxSize, 0f, groundLayer) != null;
    }

    // 縮み進捗を時間に応じて更新する
    private void UpdateSquashProgress(bool shouldStaySquashed)
    {
        if (shouldStaySquashed)
        {
            // キーを押している間、または天井があって伸びきれない時は縮む方向に進める
            squashProgress += Time.deltaTime / squashDuration;
        }
        else
        {
            // キーを離していて、かつ天井もない時は伸びる方向に戻す
            squashProgress -= Time.deltaTime / stretchDuration;
        }

        // 0〜1の範囲に収める
        squashProgress = Mathf.Clamp01(squashProgress);
    }

    // 現在の進捗に応じてスケールとColliderを適用する(通常時のしゃがみ用)
    private void ApplySquash()
    {
        // ーーー見た目のスケールを補間するーーー
        Vector3 beforeScale = transform.localScale;
        Vector3 afterScale = Vector3.Lerp(normalScale, squashedScale, squashProgress);
        transform.localScale = afterScale;

        // ーーーColliderのY方向を足元基準で縮めるーーー
        // 縮み切った時のsize.yに対する倍率を補間する(伸びきりで1、縮みきりでsquashedColliderYRatio)
        // 実際の当たり判定Y = size.y × localScale.y なので、localScale側でも縮む分だけ自動的に当たり判定も縮む
        // さらにsize.yにsquashedColliderYRatioをかけることで、見た目より少しゆとりのある当たり判定になる
        float ratioToSquashed = Mathf.Lerp(1f, squashedColliderYRatio, squashProgress);
        float currentSizeY = normalColliderSizeY * ratioToSquashed;

        // 下端を固定するためのoffset補正量を計算する
        float normalBottomLocalY = normalColliderOffsetY - normalColliderSizeY / 2f;
        float currentOffsetY = normalBottomLocalY + currentSizeY / 2f;

        // Colliderのsizeとoffsetを適用する(sizeのXはそのまま、YとoffsetのYだけ変更)
        boxCollider.size = new Vector2(boxCollider.size.x, currentSizeY);
        boxCollider.offset = new Vector2(boxCollider.offset.x, currentOffsetY);

        // ーーー下端を基準に縮ませるために位置を補正するーーー
        if (squashFromBottom)
        {
            // スケール変化による見た目の高さの差分を計算する(中心基準で縮んだ分の半分だけ下端が浮く)
            float heightDifference = (beforeScale.y - afterScale.y) * normalColliderSizeY / 2f;

            // その分プレイヤーを下に移動させることで、下端の位置を保つ
            transform.position -= new Vector3(0f, heightDifference, 0f);
        }
    }

    // 接地判定・天井判定・Collider範囲をSceneビューに可視化する(デバッグ用)
    private void OnDrawGizmos()
    {
        // boxColliderがまだ取得されていない場合(エディタ停止中)はGetComponentで取る
        BoxCollider2D box = boxCollider != null ? boxCollider : GetComponent<BoxCollider2D>();
        if (box == null) return;

        // ーーー現在のCollider範囲をマゼンタで描画ーーー
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(box.bounds.center, box.bounds.size);

        // ーーー接地判定ボックスーーー
        Vector2 groundBoxCenter = new Vector2(box.bounds.center.x, box.bounds.min.y - groundCheckThickness / 2f);
        Vector2 groundBoxSize = new Vector2(box.bounds.size.x - groundCheckSideMargin * 2f, groundCheckThickness);

        // Playモード中のみ接地判定の結果で色分けし、エディタ停止中は黄色で表示する
        if (Application.isPlaying)
        {
            Gizmos.color = CheckGrounded() ? Color.green : Color.red;
        }
        else
        {
            Gizmos.color = Color.yellow;
        }
        Gizmos.DrawWireCube(groundBoxCenter, groundBoxSize);

        // ーーー天井判定ボックスーーー
        // 通常スケール基準の高さで天井チェック位置を計算する(停止中は現在の値で代用)
        float referenceScaleY = Application.isPlaying ? normalScale.y : transform.localScale.y;
        float referenceColliderSizeY = Application.isPlaying ? normalColliderSizeY : box.size.y;
        float bottomY = box.bounds.min.y;
        float normalTopY = bottomY + referenceColliderSizeY * referenceScaleY;

        Vector2 ceilingBoxCenter = new Vector2(box.bounds.center.x, normalTopY + ceilingCheckThickness / 2f);
        Vector2 ceilingBoxSize = new Vector2(box.bounds.size.x - ceilingCheckSideMargin * 2f, ceilingCheckThickness);

        // Playモード中のみ天井判定の結果で色分けし、エディタ停止中は黄色で表示する
        if (Application.isPlaying)
        {
            Gizmos.color = CheckCeiling() ? Color.green : Color.red;
        }
        else
        {
            Gizmos.color = Color.yellow;
        }
        Gizmos.DrawWireCube(ceilingBoxCenter, ceilingBoxSize);
    }
}