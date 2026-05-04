using TMPro;
using UnityEngine;

public enum ShotResult
{
    None,
    Goal,
    Save,
    Miss
}

public enum PlayerTurnState
{
    Shoot,
    Save
}

public class BallController_Prototype : MonoBehaviour
{
    [Header("Scene Objects")]
    public Collider2D goalCollider;
    public Collider2D goalZoneCollider;
    public Collider2D crossbarCollider;
    public Collider2D leftPostCollider;
    public Collider2D rightPostCollider;
    public Transform goalkeeperTransform;
    public Collider2D goalkeeperCollider;
    public SpriteRenderer goalkeeperRenderer;
    public Transform playerTransform;
    public Collider2D playerCollider;
    public SpriteRenderer playerRenderer;

    [Header("UI")]
    public TextMeshProUGUI timerText;
    public TextMeshProUGUI playerTurnText;

    [Header("Turn")]
    public PlayerTurnState currentTurn = PlayerTurnState.Shoot;
    public float shootTurnSeconds = 10f;
    public float saveTurnSeconds = 5f;
    public Color shooterGoalkeeperColor = Color.yellow;
    public Color playerGoalkeeperColor = Color.blue;

    [Header("Shot")]
    public float minShotSpeed = 3f;
    public float maxShotSpeed = 30f;
    public float chargeSpeed = 5f;
    public float autoShootCharge = 0.35f;
    public float randomShotCharge = 0.55f;
    public float targetReachDistance = 0.03f;
    public float resetDelay = 1.5f;

    [Header("Player Kick Movement")]
    public float playerKickMoveSpeed = 8f;
    public float playerKickReachDistance = 0.03f;

    [Header("Goalkeeper")]
    public float goalkeeperJumpSpeed = 8f;
    public int lastGoalkeeperRow = -1;
    public int lastGoalkeeperCol = -1;

    [Header("Grid Data")]
    public int gridRows = 3;
    public int gridCols = 3;
    public int lastGridRow = -1;
    public int lastGridCol = -1;

    private Rigidbody2D rb;
    private Vector2 ballStartPosition;
    private Vector2 goalkeeperStartPosition;
    private Vector2 goalkeeperTargetPosition;
    private Vector2 playerStartPosition;
    private Vector2 playerKickTargetPosition;
    private RigidbodyType2D ballStartBodyType;

    private bool isCharging;
    private bool isPlayerKicking;
    private bool isShooting;
    private bool resultTriggered;
    private bool playerSelectedSaveGrid;

    private float turnTimeRemaining;
    private float chargeAmount;
    private float shotSpeed;
    private Vector2 targetPosition;
    private ShotResult targetResult = ShotResult.None;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        ballStartPosition = transform.position;

        if (rb != null)
        {
            ballStartBodyType = rb.bodyType;
        }

        FindSceneObjectsIfMissing();

        if (goalkeeperTransform != null)
        {
            goalkeeperStartPosition = goalkeeperTransform.position;
            goalkeeperTargetPosition = goalkeeperStartPosition;
        }

        if (playerTransform != null)
        {
            playerStartPosition = playerTransform.position;
            playerKickTargetPosition = playerStartPosition;
        }
    }

    void Start()
    {
        StartTurn(PlayerTurnState.Shoot);
    }

    void Update()
    {
        if (resultTriggered)
        {
            UpdateUI();
            return;
        }

        if (isPlayerKicking)
        {
            MovePlayerToBall();
            return;
        }

        if (isShooting)
        {
            MoveShot();
            MoveGoalkeeper();
            return;
        }

        turnTimeRemaining -= Time.deltaTime;
        UpdateUI();

        if (currentTurn == PlayerTurnState.Shoot)
        {
            HandleShootTurnInput();
        }
        else
        {
            HandleSaveTurnInput();
        }

        if (turnTimeRemaining <= 0f)
        {
            HandleTurnTimerEnded();
        }
    }

    void StartTurn(PlayerTurnState nextTurn)
    {
        CancelInvoke(nameof(ResetAfterResult));

        currentTurn = nextTurn;
        turnTimeRemaining = currentTurn == PlayerTurnState.Shoot ? shootTurnSeconds : saveTurnSeconds;

        isCharging = false;
        isPlayerKicking = false;
        isShooting = false;
        resultTriggered = false;
        playerSelectedSaveGrid = false;
        chargeAmount = 0f;
        targetResult = ShotResult.None;
        lastGridRow = -1;
        lastGridCol = -1;
        lastGoalkeeperRow = -1;
        lastGoalkeeperCol = -1;

        SetGoalkeeperColor(currentTurn == PlayerTurnState.Shoot ? shooterGoalkeeperColor : playerGoalkeeperColor);
        SetPlayerColor(currentTurn == PlayerTurnState.Shoot ? playerGoalkeeperColor : shooterGoalkeeperColor);
        UpdateUI();
    }

    void HandleShootTurnInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 clickPosition = GetMouseWorldPosition();

            if (IsPointInside(goalCollider, clickPosition))
            {
                isCharging = true;
                chargeAmount = 0f;
                targetPosition = clickPosition;
                StoreGridData(clickPosition);
            }
        }

        if (isCharging)
        {
            chargeAmount += Time.deltaTime * chargeSpeed;
            chargeAmount = Mathf.Clamp01(chargeAmount);
        }

        if (Input.GetMouseButtonUp(0) && isCharging)
        {
            StartPlayerShot();
        }
    }

    void HandleSaveTurnInput()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        Vector2 clickPosition = GetMouseWorldPosition();
        if (!IsPointInside(goalCollider, clickPosition)) return;

        StoreGridData(clickPosition);
        lastGoalkeeperRow = lastGridRow;
        lastGoalkeeperCol = lastGridCol;
        playerSelectedSaveGrid = lastGoalkeeperRow >= 0 && lastGoalkeeperCol >= 0;
    }

    void HandleTurnTimerEnded()
    {
        turnTimeRemaining = 0f;

        if (currentTurn == PlayerTurnState.Shoot)
        {
            AutoShootMiddleCenter();
        }
        else
        {
            StartRandomOpponentShot();
        }
    }

    Vector2 GetMouseWorldPosition()
    {
        Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        return new Vector2(mousePosition.x, mousePosition.y);
    }

    void StartPlayerShot()
    {
        PickRandomGoalkeeperJump();
        StartShot(GetResultForCurrentTarget());
    }

    void AutoShootMiddleCenter()
    {
        int centerRow = gridRows / 2;
        int centerCol = gridCols / 2;

        targetPosition = GetGridCenter(centerRow, centerCol);
        lastGridRow = centerRow;
        lastGridCol = centerCol;
        lastGoalkeeperRow = centerRow;
        lastGoalkeeperCol = centerCol;
        goalkeeperTargetPosition = goalkeeperStartPosition;
        chargeAmount = autoShootCharge;

        StartShot(ShotResult.Save);
    }

    void StartRandomOpponentShot()
    {
        int shotRow = Random.Range(0, gridRows);
        int shotCol = Random.Range(0, gridCols);

        targetPosition = GetGridCenter(shotRow, shotCol);
        lastGridRow = shotRow;
        lastGridCol = shotCol;
        chargeAmount = randomShotCharge;

        if (!playerSelectedSaveGrid)
        {
            lastGoalkeeperRow = gridRows / 2;
            lastGoalkeeperCol = gridCols / 2;
        }

        if (playerSelectedSaveGrid)
        {
            SetGoalkeeperTargetToGrid(lastGoalkeeperRow, lastGoalkeeperCol);
        }
        else
        {
            goalkeeperTargetPosition = goalkeeperStartPosition;
        }

        ShotResult result = DoesGoalkeeperSaveShot(shotRow, shotCol, lastGoalkeeperRow, lastGoalkeeperCol)
                ? ShotResult.Save
                : ShotResult.Goal;

        StartShot(result);
    }

    void StartShot(ShotResult result)
    {
        if (isPlayerKicking || isShooting || resultTriggered) return;

        isCharging = false;
        isPlayerKicking = true;
        isShooting = false;
        resultTriggered = false;
        targetResult = result;
        shotSpeed = Mathf.Lerp(minShotSpeed, maxShotSpeed, Mathf.Clamp01(chargeAmount));
        SetPlayerKickTarget();

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        if (playerTransform == null)
        {
            BeginBallShot();
        }

        UpdateUI();
    }

    void MovePlayerToBall()
    {
        if (playerTransform == null)
        {
            BeginBallShot();
            return;
        }

        playerTransform.position = Vector2.MoveTowards(
            playerTransform.position,
            playerKickTargetPosition,
            playerKickMoveSpeed * Time.deltaTime
        );

        if (Vector2.Distance(playerTransform.position, playerKickTargetPosition) <= playerKickReachDistance)
        {
            BeginBallShot();
        }
    }

    void BeginBallShot()
    {
        isPlayerKicking = false;
        isShooting = true;
    }

    void MoveShot()
    {
        Vector2 nextPosition = Vector2.MoveTowards(
            transform.position,
            targetPosition,
            shotSpeed * Time.deltaTime
        );

        transform.position = nextPosition;

        if (Vector2.Distance(nextPosition, targetPosition) <= targetReachDistance)
        {
            TriggerResult(targetResult);
        }
    }

    void MoveGoalkeeper()
    {
        if (goalkeeperTransform == null) return;

        goalkeeperTransform.position = Vector2.MoveTowards(
            goalkeeperTransform.position,
            goalkeeperTargetPosition,
            goalkeeperJumpSpeed * Time.deltaTime
        );
    }

    ShotResult GetResultForCurrentTarget()
    {
        if (IsPointInside(crossbarCollider, targetPosition) ||
            IsPointInside(leftPostCollider, targetPosition) ||
            IsPointInside(rightPostCollider, targetPosition))
        {
            return ShotResult.Miss;
        }

        if (DoesGoalkeeperSaveShot(lastGridRow, lastGridCol, lastGoalkeeperRow, lastGoalkeeperCol))
        {
            return ShotResult.Save;
        }

        if (IsPointInside(goalZoneCollider, targetPosition) || IsPointInside(goalCollider, targetPosition))
        {
            return ShotResult.Goal;
        }

        return ShotResult.Miss;
    }

    bool IsPointInside(Collider2D targetCollider, Vector2 point)
    {
        return targetCollider != null && targetCollider.OverlapPoint(point);
    }

    bool DoesGoalkeeperSaveShot(int shotRow, int shotCol, int goalkeeperRow, int goalkeeperCol)
    {
        if (shotRow < 0 || shotCol < 0 || goalkeeperCol < 0) return false;

        int centerCol = gridCols / 2;
        bool shotIsCenter = shotCol == centerCol;
        bool goalkeeperIsCenter = goalkeeperCol == centerCol;

        if (shotIsCenter && goalkeeperIsCenter)
        {
            return true;
        }

        return goalkeeperRow >= 0 && shotRow == goalkeeperRow && shotCol == goalkeeperCol;
    }

    void StoreGridData(Vector2 clickPosition)
    {
        lastGridRow = -1;
        lastGridCol = -1;

        if (goalCollider == null || gridRows <= 0 || gridCols <= 0) return;

        Bounds bounds = goalCollider.bounds;
        float cellWidth = bounds.size.x / gridCols;
        float cellHeight = bounds.size.y / gridRows;

        lastGridCol = Mathf.Clamp((int)((clickPosition.x - bounds.min.x) / cellWidth), 0, gridCols - 1);
        lastGridRow = Mathf.Clamp((int)((clickPosition.y - bounds.min.y) / cellHeight), 0, gridRows - 1);
    }

    void PickRandomGoalkeeperJump()
    {
        if (gridRows <= 0 || gridCols <= 0)
        {
            lastGoalkeeperRow = -1;
            lastGoalkeeperCol = -1;
            goalkeeperTargetPosition = goalkeeperStartPosition;
            return;
        }

        lastGoalkeeperRow = Random.Range(0, gridRows);
        lastGoalkeeperCol = Random.Range(0, gridCols);
        SetGoalkeeperTargetToGrid(lastGoalkeeperRow, lastGoalkeeperCol);
    }

    void SetGoalkeeperTargetToGrid(int row, int col)
    {
        if (goalkeeperTransform == null || goalkeeperCollider == null || goalCollider == null) return;

        Vector2 gridCenter = GetGridCenter(row, col);
        Vector2 currentBodyCenter = goalkeeperCollider.bounds.center;
        Vector2 bodyToTargetOffset = gridCenter - currentBodyCenter;

        goalkeeperTargetPosition = (Vector2)goalkeeperTransform.position + bodyToTargetOffset;
    }

    Vector2 GetGridCenter(int row, int col)
    {
        Bounds bounds = goalCollider.bounds;
        float cellWidth = bounds.size.x / gridCols;
        float cellHeight = bounds.size.y / gridRows;

        float x = bounds.min.x + (col + 0.5f) * cellWidth;
        float y = bounds.min.y + (row + 0.5f) * cellHeight;

        return new Vector2(x, y);
    }

    void TriggerResult(ShotResult result)
    {
        if (resultTriggered) return;

        resultTriggered = true;
        isShooting = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        Debug.Log("Result: " + result);

        CancelInvoke(nameof(ResetAfterResult));
        Invoke(nameof(ResetAfterResult), resetDelay);
    }

    void ResetAfterResult()
    {
        ResetBallAndGoalkeeper();
        StartTurn(currentTurn == PlayerTurnState.Shoot ? PlayerTurnState.Save : PlayerTurnState.Shoot);
    }

    void ResetBallAndGoalkeeper()
    {
        transform.position = ballStartPosition;

        if (rb != null)
        {
            rb.bodyType = ballStartBodyType;
            rb.linearVelocity = Vector2.zero;
        }

        if (goalkeeperTransform != null)
        {
            goalkeeperTransform.position = goalkeeperStartPosition;
            goalkeeperTargetPosition = goalkeeperStartPosition;
        }

        if (playerTransform != null)
        {
            playerTransform.position = playerStartPosition;
            playerKickTargetPosition = playerStartPosition;
        }
    }

    void UpdateUI()
    {
        if (timerText != null)
        {
            timerText.text = Mathf.CeilToInt(Mathf.Max(0f, turnTimeRemaining)).ToString();
        }

        if (playerTurnText != null)
        {
            playerTurnText.text = currentTurn == PlayerTurnState.Shoot ? "Shoot the ball" : "Save the ball";
        }
    }

    void SetGoalkeeperColor(Color color)
    {
        if (goalkeeperRenderer != null)
        {
            goalkeeperRenderer.color = color;
        }
    }

    void SetPlayerColor(Color color)
    {
        if (playerRenderer != null)
        {
            playerRenderer.color = color;
        }
    }

    void SetPlayerKickTarget()
    {
        playerKickTargetPosition = playerStartPosition;

        if (playerTransform == null) return;

        Vector2 footPosition = playerCollider != null
            ? new Vector2(playerCollider.bounds.center.x, playerCollider.bounds.min.y)
            : (Vector2)playerTransform.position;

        Vector2 footToBallOffset = ballStartPosition - footPosition;
        playerKickTargetPosition = (Vector2)playerTransform.position + footToBallOffset;
    }

    void FindSceneObjectsIfMissing()
    {
        if (goalCollider == null) goalCollider = FindCollider("Goal");
        if (goalZoneCollider == null) goalZoneCollider = FindCollider("GoalZone");
        if (crossbarCollider == null) crossbarCollider = FindCollider("Crossbar");
        if (leftPostCollider == null) leftPostCollider = FindCollider("LeftPost");
        if (rightPostCollider == null) rightPostCollider = FindCollider("RightPost");

        GameObject goalkeeper = GameObject.Find("Goalkeeper");
        if (goalkeeper != null)
        {
            if (goalkeeperTransform == null) goalkeeperTransform = goalkeeper.transform;
            if (goalkeeperCollider == null) goalkeeperCollider = goalkeeper.GetComponent<Collider2D>();
            if (goalkeeperRenderer == null) goalkeeperRenderer = goalkeeper.GetComponent<SpriteRenderer>();
        }

        if (timerText == null) timerText = FindText("Timer");
        if (playerTurnText == null) playerTurnText = FindText("PlayerTurn");

        GameObject player = GameObject.Find("Player");
        if (player != null)
        {
            if (playerTransform == null) playerTransform = player.transform;
            if (playerCollider == null) playerCollider = player.GetComponent<Collider2D>();
            if (playerRenderer == null) playerRenderer = player.GetComponent<SpriteRenderer>();
        }
    }

    Collider2D FindCollider(string objectName)
    {
        GameObject foundObject = GameObject.Find(objectName);
        return foundObject == null ? null : foundObject.GetComponent<Collider2D>();
    }

    TextMeshProUGUI FindText(string objectName)
    {
        GameObject foundObject = GameObject.Find(objectName);
        return foundObject == null ? null : foundObject.GetComponent<TextMeshProUGUI>();
    }

    void OnDrawGizmos()
    {
        if (goalCollider == null || gridRows <= 0 || gridCols <= 0) return;

        Bounds bounds = goalCollider.bounds;
        float cellWidth = bounds.size.x / gridCols;
        float cellHeight = bounds.size.y / gridRows;

        Gizmos.color = Color.red;

        for (int col = 1; col < gridCols; col++)
        {
            float x = bounds.min.x + col * cellWidth;
            Gizmos.DrawLine(new Vector3(x, bounds.min.y), new Vector3(x, bounds.max.y));
        }

        for (int row = 1; row < gridRows; row++)
        {
            float y = bounds.min.y + row * cellHeight;
            Gizmos.DrawLine(new Vector3(bounds.min.x, y), new Vector3(bounds.max.x, y));
        }
    }
}
