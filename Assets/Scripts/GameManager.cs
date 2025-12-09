using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; } // シングルトンは必ず一つしか生成されないゲームマネージャーと相性が良い

    public enum FieldArea
    {
        Outside,
        FrontArea,
        BackArea,
        FrontCrease,
        BackCrease,
    }

    public GameObject characterPrefab; // 生成するキャラクターのプレハブ
    public GameObject ballPrefab;
    
    private GameObject field;        // フィールドオブジェクト
    private (float x, float z) fieldEdge;
    public PhysicsMaterial outerWallMaterial;
    private GameObject frontGoal;        // 手前のゴールオブジェクト
    private GameObject backGoal;        // 奥のゴールオブジェクト
    
    public float creaseRadius = 10.0f;
    public int creaseSegments = 50;
    public float creaseLineWidth = 0.05f;

    private GameObject frontGoalCrease;
    private GameObject backGoalCrease;

    public float yOffset = 1f;      // 足元を地面から浮かせるオフセット
    public int spawnCount = 3;      // 生成数
    public int maxAttempts = 50;     // 衝突を避けるための再試行回数

    private float playerRadius;
    private float playerHeight;
    private Vector3 fieldMin;        // ステージの左下(x,z)
    private Vector3 fieldMax;        // ステージの右上(x,z)

    private List<GameObject> characterObjList = new List<GameObject>();
    private GameObject ballObj;
    private Vector3 ballPositionOut;

    private int playerIndex = 0;

    public GameObject mainCamera;

    private GameObject[] fieldPrefabs;
    public GameObject goalPrefab;

    public int score = 0;
    public int enemyScore = 0;
    [SerializeField] private TextMeshProUGUI scoreText;
    
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;

        fieldPrefabs = Resources.LoadAll<GameObject>("Fields");
    }

    void Start()
    {
        SpawnField();

        // ステージの Renderer または Collider から範囲を取得
        Renderer fieldRenderer = field.GetComponent<Renderer>();
        if (fieldRenderer != null)
        {
            fieldMin = fieldRenderer.bounds.min;
            fieldMax = fieldRenderer.bounds.max;
        }
        else
        {
            Collider fieldCollider = field.GetComponent<Collider>();
            if (fieldCollider != null)
            {
                fieldMin = fieldCollider.bounds.min;
                fieldMax = fieldCollider.bounds.max;
            }
            else
            {
                Debug.LogError("ステージに Renderer か Collider が必要です");
                return;
            }
        }

        playerRadius = characterPrefab.GetComponent<Collider>().bounds.extents.x;
        playerHeight = characterPrefab.GetComponent<Collider>().bounds.extents.y;

        SpawnCharacter();

        scoreText.text = "0 - 0";
    }

    void Update()
    {
        CheckFieldArea();
        if (Input.GetKeyDown(KeyCode.R))
        {
            SwitchCharacter();
        }
    }

    // Update is called once per frame
    void SpawnCharacter()
    {
        int spawned = 0;
        int attempts = 0;

        float y = fieldMin.y + playerRadius + yOffset;

        while (spawned < spawnCount && attempts < spawnCount * maxAttempts)
        {
            attempts++;

            float randomX = Random.Range(fieldMin.x + playerRadius, fieldMax.x - playerRadius);
            float randomZ = Random.Range(fieldMin.z + playerRadius, fieldMax.z - playerRadius);

            Vector3 spawnPos = new Vector3(randomX, y, randomZ);

            // OverlapSphereで近くに他のキャラクターがいるか確認
            Collider[] colliders = Physics.OverlapSphere(spawnPos, playerRadius * 1.5f);
            if (colliders.Length == 0)
            {
                GameObject characterObj = Instantiate(characterPrefab, spawnPos, Quaternion.identity);
                characterObjList.Add(characterObj);

                Character character = characterObj.GetComponent<Character>();

                if (spawned == 0) {
                    character.isPlayer = true;
                    mainCamera.GetComponent<CameraFollow>().SetTarget(character.transform);
                    // ボールを生成する位置（プレイヤーの少し前）
                    Vector3 ballPos = characterObj.transform.position 
                                    + characterObj.transform.forward * 3f 
                                    + Vector3.up * 1.2f; // 手元の高さに調整

                    // ボールを生成
                    ballObj = Instantiate(ballPrefab, ballPos, Quaternion.identity);
                }
                spawned++;
            }
        }

        if (spawned < spawnCount)
            Debug.LogWarning("指定数を生成できませんでした。ステージが狭すぎるか、キャラクターサイズが大きい可能性があります。");

        CheckFieldArea();
    }

    void SwitchCharacter()
    {
        if ((playerIndex == 0 && characterObjList.Count - 1 == 0) || characterObjList[playerIndex].GetComponent<Character>().heldBall != null) 
            return;

        characterObjList[playerIndex].GetComponent<Character>().isPlayer = false;
        playerIndex = playerIndex == characterObjList.Count - 1 ? 0 : playerIndex + 1;
        characterObjList[playerIndex].GetComponent<Character>().isPlayer = true;
        mainCamera.GetComponent<CameraFollow>().SetTarget(characterObjList[playerIndex].transform);
    }

    void SpawnField()
    {
        Vector3 spawnPos = new Vector3(0, 0, 0);
        field = Instantiate(fieldPrefabs[0], spawnPos, Quaternion.identity);
        Bounds fieldBounds = field.GetComponent<MeshRenderer>().bounds;
        BoxCollider goalBoxCollider = goalPrefab.transform.Find("LeftBar").GetComponent<BoxCollider>();
        Vector3 goalSize = Vector3.Scale(goalBoxCollider.size, goalBoxCollider.transform.lossyScale);
        CreateWalls(0.5f, 5f);

        Vector3 frontGoalPos = new Vector3(
            fieldBounds.center.x,
            fieldBounds.center.y + goalSize.y / 2,
            fieldBounds.center.z - 100.0f
        );

        Vector3 backGoalPos = new Vector3(
            fieldBounds.center.x,
            fieldBounds.center.y + goalSize.y / 2,
            fieldBounds.center.z + 100.0f
        );

        frontGoal = Instantiate(goalPrefab, frontGoalPos, Quaternion.identity);
        frontGoal.GetComponent<Goal>().setTeamID(0);
        backGoal = Instantiate(goalPrefab, backGoalPos, Quaternion.Euler(0, 180, 0));
        frontGoal.GetComponent<Goal>().setTeamID(1);

        DrawCrease(frontGoal.transform);
        DrawCrease(backGoal.transform);
        DrawOutline();
        DrawHalfLine(); 
    }

    void CreateWalls(float wallThickness, float wallHeight)
    {
        Bounds bounds = field.GetComponent<MeshRenderer>().bounds;

        Vector3 center = bounds.center;
        Vector3 size = bounds.size;

        // 横幅と奥行き（地面の大きさ）
        float width = size.x;
        float depth = size.z;

        // 壁プレハブを作らない場合はCubeから生成
        GameObject wallPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallPrefab.GetComponent<MeshRenderer>().enabled = false; // ★透明にする

        // ---- 左壁 ----
        GameObject leftWall = Instantiate(wallPrefab);
        leftWall.transform.position = new Vector3(
            center.x - width / 2 - wallThickness / 2,
            center.y + wallHeight / 2,
            center.z
        );
        leftWall.transform.localScale = new Vector3(
            wallThickness, wallHeight, depth
        );
        leftWall.GetComponent<BoxCollider>().material = outerWallMaterial;

        // ---- 右壁 ----
        GameObject rightWall = Instantiate(wallPrefab);
        rightWall.transform.position = new Vector3(
            center.x + width / 2 + wallThickness / 2,
            center.y + wallHeight / 2,
            center.z
        );
        rightWall.transform.localScale = new Vector3(
            wallThickness, wallHeight, depth
        );
        rightWall.GetComponent<BoxCollider>().material = outerWallMaterial;

        // ---- 前壁 ----
        GameObject frontWall = Instantiate(wallPrefab);
        frontWall.transform.position = new Vector3(
            center.x,
            center.y + wallHeight / 2,
            center.z + depth / 2 + wallThickness / 2
        );
        frontWall.transform.localScale = new Vector3(
            width, wallHeight, wallThickness
        );
        frontWall.GetComponent<BoxCollider>().material = outerWallMaterial;

        // ---- 後壁 ----
        GameObject backWall = Instantiate(wallPrefab);
        backWall.transform.position = new Vector3(
            center.x,
            center.y + wallHeight / 2,
            center.z - depth / 2 - wallThickness / 2
        );
        backWall.transform.localScale = new Vector3(
            width, wallHeight, wallThickness
        );
        backWall.GetComponent<BoxCollider>().material = outerWallMaterial;
    }

    private (GameObject obj, LineRenderer lr) CreateLine(string name, Transform parentTransform, float width = 0.2f)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parentTransform);

        var lr = obj.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.startWidth = lr.endWidth = width;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.positionCount = 0;

        return (obj, lr);
    }

    void DrawCrease(Transform goalTransform)
    {
        float bottomY = goalTransform.Find("LeftBar").GetComponent<Collider>().bounds.min.y;

        (GameObject circleObj, LineRenderer lr) = CreateLine("Crease", goalTransform);

        Vector3 worldPos = new Vector3(
            goalTransform.position.x,
            bottomY,
            goalTransform.position.z
        );

        circleObj.transform.localPosition = goalTransform.InverseTransformPoint(worldPos);

        lr.positionCount = creaseSegments + 1;
        for (int i = 0; i <= creaseSegments; i++)
        {
            float angle = 2 * Mathf.PI * i / creaseSegments;
            float x = Mathf.Cos(angle) * creaseRadius;
            float z = Mathf.Sin(angle) * creaseRadius;

            lr.SetPosition(i, new Vector3(x, 0f, z));
        }
    }

    private void DrawOutline()
    {
        (GameObject obj, LineRenderer lr) = CreateLine("Outline", field.transform);

        BoxCollider box = field.GetComponent<BoxCollider>();
        Vector3 size = Vector3.Scale(box.size, box.transform.lossyScale);
        float w = size.x / 3;
        float h = size.z / 3;

        fieldEdge = (w, h);

        Vector3[] p =
        {
            new Vector3(-w, 0, -h),
            new Vector3(-w, 0, h),
            new Vector3( w, 0, h),
            new Vector3( w, 0, -h),
            new Vector3(-w, 0, -h)
        };

        lr.positionCount = p.Length;
        lr.SetPositions(p);
    }

    private void DrawHalfLine()
    {
        (GameObject obj, LineRenderer lr) = CreateLine("HalfLine", field.transform);

        BoxCollider box = field.GetComponent<BoxCollider>();
        Vector3 size = Vector3.Scale(box.size, box.transform.lossyScale);
        float w = size.x / 3;

        Vector3[] p =
        {
            new Vector3(w, 0, 0),
            new Vector3(-w, 0, 0),
        };

        lr.positionCount = p.Length;
        lr.SetPositions(p);
    }

    private void CheckFieldArea()
    {
        foreach (GameObject characterObj in characterObjList)
        {
            Character character = characterObj.GetComponent<Character>();
            if (character.isGrounded == false)
                continue;

            if (isInCrease(frontGoal.transform.position, characterObj.transform.position))
            {
                if (character.fieldArea != FieldArea.FrontCrease && character.heldBall != null)
                {
                    frontGoal.transform.Find("Crease").GetComponent<LineRenderer>().material.color = Color.red;
                }
                character.fieldArea = FieldArea.FrontCrease;
            }
            else if (isInCrease(backGoal.transform.position, characterObj.transform.position))
            {
                if (character.fieldArea != FieldArea.BackCrease && character.heldBall != null)
                {
                    backGoal.transform.Find("Crease").GetComponent<LineRenderer>().material.color = Color.red;
                }
                character.fieldArea = FieldArea.BackCrease;
            }
            else
            {
                FieldArea crntFieldArea = isInsideOfLine(characterObj.transform.position);
                if (character.heldBall != null)
                {
                    if (character.fieldArea == FieldArea.FrontCrease && crntFieldArea == FieldArea.FrontArea)
                    {
                        frontGoal.transform.Find("Crease").GetComponent<LineRenderer>().material.color = Color.white;
                    }
                    else if (character.fieldArea == FieldArea.BackCrease && crntFieldArea == FieldArea.BackArea)
                    {
                        backGoal.transform.Find("Crease").GetComponent<LineRenderer>().material.color = Color.white;
                    }
                }
                character.fieldArea = crntFieldArea;
            }
        }

        Ball ball = ballObj.GetComponent<Ball>();
        if (ball.isGrounded == false)
            return;

        if (isInCrease(frontGoal.transform.position, ballObj.transform.position))
        {
            ball.fieldArea = FieldArea.FrontCrease;
        }
        else if (isInCrease(backGoal.transform.position, ballObj.transform.position))
        {
            ball.fieldArea = FieldArea.BackCrease;
        }
        else
        {
            FieldArea previousBallFieldArea = ball.fieldArea;
            ball.fieldArea = isInsideOfLine(ballObj.transform.position);

            if (ballObj.transform.parent == null)
            {
                if (ball.fieldArea == FieldArea.FrontArea)
                {
                    frontGoal.transform.Find("Crease").GetComponent<LineRenderer>().material.color = Color.white;
                }
                else if (ball.fieldArea == FieldArea.BackArea)
                {
                    backGoal.transform.Find("Crease").GetComponent<LineRenderer>().material.color = Color.white;
                }
            }


            if (ball.fieldArea == FieldArea.Outside && ball.fieldArea != previousBallFieldArea)
            {
                
            }
        }
        
    }

    private bool isInCrease(Vector3 creasePosition, Vector3 objPosition)
    {
        float dx = objPosition.x - creasePosition.x;
        float dz = objPosition.z - creasePosition.z;
        return dx * dx + dz * dz < creaseRadius * creaseRadius;
    }

    private FieldArea isInsideOfLine(Vector3 objPosition)
    {
        if (-fieldEdge.x < objPosition.x && objPosition.x < fieldEdge.x)
        {
            if (-fieldEdge.z < objPosition.z && objPosition.z < 0)
            {
                return FieldArea.FrontArea;
            }
            else if (0 < objPosition.z && objPosition.z < fieldEdge.z)
            {
                return FieldArea.BackArea;
            }
        }

        return FieldArea.Outside;
    }

    public void AddScore(int teamID, int point)
    {
        if (frontGoal.transform.Find("Crease").GetComponent<LineRenderer>().material.color != Color.white ||
            backGoal.transform.Find("Crease").GetComponent<LineRenderer>().material.color != Color.white)
        {
            return;
        }

        if (teamID == 0)
        {
            score += point;
        }
        else 
        {
            enemyScore += point;
        }

        scoreText.text = $"{score} - {enemyScore}";
    }

    public void ResetScore()
    {
        score = 0;
        enemyScore = 0;
        scoreText.text = "0 - 0";
    }
}
