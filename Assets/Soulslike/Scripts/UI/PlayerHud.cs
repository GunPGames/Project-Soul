using UnityEngine;
using UnityEngine.UI;

namespace Soulslike
{
    /// <summary>
    /// 플레이어 HUD다(이슈 #3, #2 조준점).
    /// - 체력: 빨간 바는 즉시 줄고, 뒤의 흰 바는 잠시 뒤 천천히 따라 내려가 잃은 양을 보여 준다.
    /// - 기력: 노란 바.
    /// - 조준점: 활 조준 중 화면 중앙에 표시한다.
    /// Canvas·이미지·텍스처는 이 컴포넌트가 실행 중에 만들고 파괴할 때 함께 정리한다(별도 UI 에셋 없음).
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public class PlayerHud : MonoBehaviour
    {
        [Header("References (비워 두면 자동으로 찾는다)")]
        [SerializeField] PlayerStats stats;
        [SerializeField] BowAttack bow;

        [Header("Layout (기준 해상도 1920×1080)")]
        [SerializeField] Vector2 margin = new Vector2(40f, 40f);
        [SerializeField] Vector2 healthBarSize = new Vector2(420f, 18f);
        [SerializeField] Vector2 staminaBarSize = new Vector2(320f, 12f);
        [SerializeField] float barSpacing = 8f;

        [Header("Colors")]
        [SerializeField] Color backgroundColor = new Color(0f, 0f, 0f, 0.6f);
        [SerializeField] Color healthColor = new Color(0.78f, 0.1f, 0.1f, 1f);
        [SerializeField] Color healthLagColor = new Color(1f, 1f, 1f, 0.9f);
        [SerializeField] Color staminaColor = new Color(0.95f, 0.8f, 0.15f, 1f);
        [SerializeField] Color crosshairColor = new Color(1f, 1f, 1f, 0.9f);

        [Header("Health Lag (흰 바)")]
        [Tooltip("피해를 받은 뒤 흰 바가 내려가기 시작할 때까지의 시간(초).")]
        [SerializeField] float lagDelay = 0.5f;
        [Tooltip("흰 바가 내려가는 속도(초당 최대 체력 비율). 0.5면 2초에 체력 전체만큼 내려간다.")]
        [SerializeField] float lagSpeed = 0.5f;

        [Header("Crosshair")]
        [SerializeField] float crosshairSize = 6f;

        [Header("Draw Indicator (시위 당김 연출)")]
        [Tooltip("당기기 시작할 때 네 조각이 놓이는 중심으로부터의 거리(px).")]
        [SerializeField] float drawStartRadius = 46f;
        [Tooltip("끝까지 당겼을 때 조각이 모이는 거리(px).")]
        [SerializeField] float drawEndRadius = 9f;
        [SerializeField] Vector2 drawTickSize = new Vector2(2f, 10f);
        [SerializeField] Color drawingColor = new Color(1f, 1f, 1f, 0.85f);
        [Tooltip("끝까지 당겨 쏠 수 있을 때의 색.")]
        [SerializeField] Color fullDrawColor = new Color(1f, 0.78f, 0.25f, 1f);
        [Tooltip("끝까지 당긴 순간 조준점이 커졌다 돌아오는 시간(초).")]
        [SerializeField] float fullDrawPulseTime = 0.18f;
        [SerializeField] float fullDrawPulseScale = 1.8f;

        GameObject _canvasObject;
        RectTransform _healthFill;
        RectTransform _healthLag;
        RectTransform _staminaFill;
        GameObject _crosshair;
        Image _crosshairImage;
        GameObject _drawIndicator;
        // 위·아래·왼쪽·오른쪽 조각. 시위를 당길수록 중심으로 모인다.
        readonly RectTransform[] _drawTicks = new RectTransform[4];
        readonly Image[] _drawTickImages = new Image[4];
        static readonly Vector2[] TickDirections = { Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        bool _wasFullyDrawn;
        float _pulseTimer;
        Texture2D _dotTexture;
        Sprite _dotSprite;

        float _shownHealth = 1f;
        float _lagHealth = 1f;
        float _lagTimer;

        /// <summary>빨간 체력 바가 보여 주는 비율(0~1).</summary>
        public float DisplayedHealthRatio => _shownHealth;
        /// <summary>흰 체력 바가 보여 주는 비율(0~1). 피해 직후에는 빨간 바보다 크다.</summary>
        public float LagHealthRatio => _lagHealth;
        public float DisplayedStaminaRatio { get; private set; } = 1f;
        public bool CrosshairVisible => _crosshair != null && _crosshair.activeSelf;
        /// <summary>당김 연출 조각의 현재 중심 거리(px). 당길수록 줄어든다.</summary>
        public float DrawIndicatorRadius { get; private set; }

        void Awake()
        {
            if (stats == null) stats = GetComponent<PlayerStats>();
            if (bow == null) bow = GetComponent<BowAttack>();
            BuildUI();
        }

        void OnDestroy()
        {
            if (_canvasObject != null) Destroy(_canvasObject);
            if (_dotSprite != null) Destroy(_dotSprite);
            if (_dotTexture != null) Destroy(_dotTexture);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;

            if (stats != null)
            {
                float health = stats.HealthRatio;
                if (health < _shownHealth)
                {
                    // 새로 잃은 만큼 흰 바가 잠시 머물렀다가 따라온다.
                    _lagTimer = lagDelay;
                }
                _shownHealth = health;

                if (_lagHealth < _shownHealth)
                {
                    // 회복하면 흰 바는 바로 맞춘다.
                    _lagHealth = _shownHealth;
                }
                else if (_lagTimer > 0f)
                {
                    _lagTimer -= dt;
                }
                else
                {
                    _lagHealth = Mathf.MoveTowards(_lagHealth, _shownHealth, lagSpeed * dt);
                }

                DisplayedStaminaRatio = stats.StaminaRatio;
            }

            SetFill(_healthFill, _shownHealth);
            SetFill(_healthLag, _lagHealth);
            SetFill(_staminaFill, DisplayedStaminaRatio);

            bool showCrosshair = bow != null && bow.IsAiming;
            if (_crosshair.activeSelf != showCrosshair) _crosshair.SetActive(showCrosshair);
            if (_drawIndicator.activeSelf != showCrosshair) _drawIndicator.SetActive(showCrosshair);
            if (showCrosshair) UpdateDrawIndicator(dt);
            else _wasFullyDrawn = false;
        }

        void UpdateDrawIndicator(float dt)
        {
            float progress = bow.DrawProgress;
            bool full = bow.IsFullyDrawn;

            // 끝에서 감속해 '딱 모이는' 느낌을 준다.
            float eased = 1f - (1f - progress) * (1f - progress);
            DrawIndicatorRadius = Mathf.Lerp(drawStartRadius, drawEndRadius, eased);

            Color tickColor = full ? fullDrawColor : drawingColor;
            // 막 당기기 시작할 때는 흐리게, 모일수록 진하게 보인다.
            if (!full) tickColor.a *= Mathf.Lerp(0.15f, 1f, eased);

            for (int i = 0; i < _drawTicks.Length; i++)
            {
                _drawTicks[i].anchoredPosition = TickDirections[i] * DrawIndicatorRadius;
                _drawTickImages[i].color = tickColor;
            }

            // 끝까지 당긴 순간 한 번 조준점을 키웠다 돌려 '쏠 수 있다'를 알린다.
            if (full && !_wasFullyDrawn) _pulseTimer = fullDrawPulseTime;
            _wasFullyDrawn = full;

            float scale = 1f;
            if (_pulseTimer > 0f)
            {
                _pulseTimer -= dt;
                float t = Mathf.Clamp01(_pulseTimer / fullDrawPulseTime);
                scale = Mathf.Lerp(1f, fullDrawPulseScale, t);
            }
            _crosshairImage.rectTransform.localScale = new Vector3(scale, scale, 1f);
            _crosshairImage.color = full ? fullDrawColor : crosshairColor;
        }

        static void SetFill(RectTransform fill, float ratio)
        {
            // 왼쪽 기준으로 오른쪽 끝(anchorMax.x)만 움직여 바 길이를 바꾼다.
            fill.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
            // 거의 0이면 안쪽 여백 때문에 폭이 음수가 되어 조각이 남으므로 숨긴다(QA B2-09).
            bool visible = ratio > 0.01f;
            if (fill.gameObject.activeSelf != visible) fill.gameObject.SetActive(visible);
        }

        // ───────────────────────── UI 생성 ─────────────────────────

        void BuildUI()
        {
            _canvasObject = new GameObject("PlayerHudCanvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = _canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scaler = _canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform root = (RectTransform)_canvasObject.transform;

            // 왼쪽 위: 체력(배경 → 흰 바 → 빨간 바 순으로 쌓는다), 그 아래 기력.
            RectTransform healthBack = CreateBar(root, "HealthBar", healthBarSize, new Vector2(margin.x, -margin.y), backgroundColor);
            _healthLag = CreateFill(healthBack, "HealthLag", healthLagColor);
            _healthFill = CreateFill(healthBack, "HealthFill", healthColor);

            float staminaY = -margin.y - healthBarSize.y - barSpacing;
            RectTransform staminaBack = CreateBar(root, "StaminaBar", staminaBarSize, new Vector2(margin.x, staminaY), backgroundColor);
            _staminaFill = CreateFill(staminaBack, "StaminaFill", staminaColor);

            // 화면 중앙 조준점.
            _dotTexture = CreateDotTexture(32);
            _dotSprite = Sprite.Create(_dotTexture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 100f);
            _crosshair = new GameObject("Crosshair", typeof(RectTransform), typeof(Image));
            var crossRect = (RectTransform)_crosshair.transform;
            crossRect.SetParent(root, false);
            crossRect.anchorMin = crossRect.anchorMax = new Vector2(0.5f, 0.5f);
            crossRect.sizeDelta = new Vector2(crosshairSize, crosshairSize);
            var crossImage = _crosshair.GetComponent<Image>();
            crossImage.sprite = _dotSprite;
            crossImage.color = crosshairColor;
            crossImage.raycastTarget = false;
            _crosshairImage = crossImage;

            // 당김 연출 조각은 조준점이 커졌다 줄 때 같이 커지지 않도록 조준점의 자식이 아니라 캔버스 중앙에 둔다.
            // 보이고 숨는 것은 LateUpdate에서 조준점과 함께 바꾼다.
            var tickRoot = new GameObject("DrawIndicator", typeof(RectTransform));
            var tickRootRect = (RectTransform)tickRoot.transform;
            tickRootRect.SetParent(root, false);
            tickRootRect.anchorMin = tickRootRect.anchorMax = new Vector2(0.5f, 0.5f);
            tickRootRect.sizeDelta = Vector2.zero;
            for (int i = 0; i < _drawTicks.Length; i++)
            {
                var tick = new GameObject("DrawTick" + i, typeof(RectTransform), typeof(Image));
                var tickRect = (RectTransform)tick.transform;
                tickRect.SetParent(tickRootRect, false);
                tickRect.anchorMin = tickRect.anchorMax = new Vector2(0.5f, 0.5f);
                // 위·아래 조각은 세로로, 왼쪽·오른쪽 조각은 가로로 눕혀 중심을 가리키게 한다.
                bool vertical = TickDirections[i].y != 0f;
                tickRect.sizeDelta = vertical ? drawTickSize : new Vector2(drawTickSize.y, drawTickSize.x);
                var tickImage = tick.GetComponent<Image>();
                tickImage.raycastTarget = false;
                _drawTicks[i] = tickRect;
                _drawTickImages[i] = tickImage;
            }
            _drawIndicator = tickRoot;
            _drawIndicator.SetActive(false);
            _crosshair.SetActive(false);
        }

        static RectTransform CreateBar(RectTransform parent, string name, Vector2 size, Vector2 topLeft, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = topLeft;
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        static RectTransform CreateFill(RectTransform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            // 배경 안쪽 2px 여백을 두고 꽉 채운다. 길이는 anchorMax.x로 바꾼다.
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0f, 0.5f);
            rect.offsetMin = new Vector2(2f, 2f);
            rect.offsetMax = new Vector2(-2f, -2f);
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return rect;
        }

        static Texture2D CreateDotTexture(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "CrosshairDot", wrapMode = TextureWrapMode.Clamp };
            float radius = size * 0.5f;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(radius - d));
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
