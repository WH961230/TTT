using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Collections;
using SFB;

public class MousePendant2D : MonoBehaviour {
    [Header("UI 挂件")] public RectTransform bobRect; // 挂坠 UI 节点
    public RectTransform segmentPrefab; // 链条段 prefab (一个细长矩形 Image)
    public Image bobImage; // 挂坠的 Image 组件，允许用户自定义 Sprite
    public Sprite defaultSprite; // 默认挂坠图像，用于恢复默认
    [Header("链条参数")] public int segmentCount = 24; // 链节数量
    public float segmentLength = 10f; // 每节像素长度
    public int constraintIterations = 12; // 约束迭代次数
    public float damping = 0.03f; // 阻尼 (0~0.1)
    public float gravity = 3000f; // 重力 像素/s^2
    [Header("停止参数")] public float velocityThreshold = 0.05f; // 速度阈值，当速度小于此值时停止挂坠
    [Header("旋转控制")] public float rotationDamping = 0.1f; // 旋转的阻尼值，控制旋转的平滑度
    public float maxRotationSpeed = 10f; // 最大旋转速度，避免快速旋转
    public float maxRotationChange = 5f; // 每帧旋转角度变化的最大值，避免过大旋转
    [Header("按钮控制")] public Button uploadButton; // 上传图片按钮
    public Button resetButton; // 恢复默认图片按钮

    // 内部状态
    private Vector2[] positions;
    private Vector2[] prevPositions;
    private Vector2 anchorPos;
    private RectTransform[] segments;
    
    private string imageFolderPath;

    void Start() {
        // 初始化图片存储目录
        imageFolderPath = Path.Combine(Application.persistentDataPath, "UserImages");

        // 确保目录存在
        Directory.CreateDirectory(imageFolderPath);  // 确保文件夹存在
        
        InitChain();

        // 绑定按钮事件
        uploadButton.onClick.AddListener(UploadImage);
        resetButton.onClick.AddListener(ResetToDefaultSprite);
    }

    void InitChain() {
        positions = new Vector2[segmentCount];
        prevPositions = new Vector2[segmentCount];
        segments = new RectTransform[segmentCount - 1];
        anchorPos = Input.mousePosition;

        // 初始链条竖直向下
        for (int i = 0; i < segmentCount; i++) {
            positions[i] = anchorPos + Vector2.down * (segmentLength * i);
            prevPositions[i] = positions[i];
        }

        // 实例化 UI 链段
        for (int i = 0; i < segments.Length; i++) {
            segments[i] = Instantiate(segmentPrefab, bobRect.parent);
            segments[i].gameObject.SetActive(true);
        }
    }

    void Update() {
        if (positions == null || positions.Length != segmentCount)
            InitChain();
        float dt = Time.deltaTime;

        // 1. 锚点 = 鼠标位置
        anchorPos = Input.mousePosition;

        // 2. Verlet 积分更新
        for (int i = 1; i < segmentCount; i++) {
            Vector2 velocity = positions[i] - prevPositions[i];
            prevPositions[i] = positions[i];
            positions[i] += velocity * (1f - damping);
            positions[i] += Vector2.down * gravity * dt * dt;
        }

        positions[0] = anchorPos; // 固定锚点

        // 3. 距离约束迭代
        for (int k = 0; k < constraintIterations; k++) {
            for (int i = 0; i < segmentCount - 1; i++) {
                Vector2 delta = positions[i + 1] - positions[i];
                float dist = delta.magnitude;
                float diff = (dist - segmentLength) / dist;
                Vector2 correction = delta * 0.5f * diff;
                if (i != 0) positions[i] += correction;
                positions[i + 1] -= correction;
            }

            positions[0] = anchorPos;
        }

        // 4. 检查物体速度，若低于阈值则停下
        Vector2 lastVelocity = positions[segmentCount - 1] - prevPositions[segmentCount - 1];

        // 如果速度小于阈值，就把速度和位置设为0
        if (lastVelocity.magnitude < velocityThreshold) {
            // 强制停止挂坠末端
            prevPositions[segmentCount - 1] = positions[segmentCount - 1]; // 停止末端速度
            positions[segmentCount - 1] = prevPositions[segmentCount - 1]; // 停止位置
        }

        // 5. 更新 UI 挂坠位置
        if (bobRect != null) {
            Vector2 end = positions[segmentCount - 1];
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                bobRect.parent as RectTransform,
                end,
                null,
                out Vector2 localEnd
            );
            bobRect.anchoredPosition = localEnd;
        }

        // 6. 更新 UI 链条段
        for (int i = 0; i < segments.Length; i++) {
            Vector2 a = positions[i];
            Vector2 b = positions[i + 1];
            Vector2 dir = b - a;
            float dist = dir.magnitude;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                bobRect.parent as RectTransform, a, null, out Vector2 localA);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                bobRect.parent as RectTransform, b, null, out Vector2 localB);
            Vector2 mid = (localA + localB) / 2f;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

            // 7. 限制旋转：平滑过渡
            float currentRotation = segments[i].rotation.eulerAngles.z;
            float rotationDiff = Mathf.DeltaAngle(currentRotation, angle);

            // 旋转速率限制：防止快速旋转
            rotationDiff = Mathf.Clamp(rotationDiff, -maxRotationChange, maxRotationChange);
            segments[i].rotation = Quaternion.Euler(0, 0, currentRotation + rotationDiff * rotationDamping);
            segments[i].anchoredPosition = mid;
            segments[i].sizeDelta = new Vector2(dist, 4f); // 4f = 粗细
        }
    }

    /// <summary>
    /// 上传图片
    /// </summary>
    public void UploadImage() {
        var extensions = new[] {
            new ExtensionFilter("图片文件", "png", "jpg", "jpeg")
        };

        // 打开文件选择对话框
        var paths = StandaloneFileBrowser.OpenFilePanel("选择图片", "", extensions, false);
        if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0])) {
            StartCoroutine(HandleImageUpload(paths[0]));
        }
    }

    // 处理上传图片
    IEnumerator HandleImageUpload(string sourcePath) {
        // 生成唯一文件名并保存到本地
        string fileName = GenerateUniqueFileName(Path.GetFileName(sourcePath));
        string targetPath = Path.Combine(imageFolderPath, fileName);

        // 确保目标文件夹存在
        Directory.CreateDirectory(imageFolderPath);  // 额外确保目标文件夹存在

        // 保存图片到目标路径
        byte[] fileData = File.ReadAllBytes(sourcePath);
        File.WriteAllBytes(targetPath, fileData);
        Debug.Log($"图片保存成功: {targetPath}");

        // 加载图片
        yield return StartCoroutine(LoadImageSprite(targetPath));
    }

    // 加载图片并设置为Sprite
    IEnumerator LoadImageSprite(string imagePath) {
        string fileUrl = "file:///" + imagePath;
        using (WWW www = new WWW(fileUrl)) {
            yield return www;

            if (string.IsNullOrEmpty(www.error)) {
                // 创建Texture2D并转换为Sprite
                Texture2D texture = new Texture2D(2, 2);
                www.LoadImageIntoTexture(texture);
                Sprite sprite = Sprite.Create(
                    texture,
                    new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f)
                );

                // 设置到Image组件
                if (bobImage != null) {
                    bobImage.gameObject.SetActive(true);
                    bobImage.sprite = sprite;
                    Debug.Log($"图片加载成功: {Path.GetFileName(imagePath)}");
                }
            } else {
                Debug.LogError($"图片加载失败: {www.error}");
            }
        }
    }


    // 恢复默认挂坠的 Image Sprite
    public void ResetToDefaultSprite() {
        if (bobImage != null && defaultSprite != null) {
            bobImage.sprite = defaultSprite;
        } else {
            Debug.LogError("Bob Image component or default sprite is not assigned.");
        }
    }

    // 生成唯一文件名
    private string GenerateUniqueFileName(string originalName) {
        string baseName = Path.GetFileNameWithoutExtension(originalName);
        string extension = Path.GetExtension(originalName);
        int counter = 1;

        string newName = originalName;
        while (File.Exists(Path.Combine(imageFolderPath, newName))) {
            newName = $"{baseName}_{counter++}{extension}";
        }

        return newName;
    }
}