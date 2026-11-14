using UnityEngine;
using UnityEngine.UI;

namespace Soulslike
{
    /// <summary>
    /// 록온 중 대상의 조준 지점(LockOnTarget.AimPoint)에 화면 위 점을 그린다.
    /// 표시용 Canvas·Image·Sprite는 이 컴포넌트가 실행 중에 만들고 파괴할 때 함께 정리한다.
    /// 카메라 위치가 확정된 뒤 그려야 하므로 ThirdPersonCamera보다 늦게 실행한다.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public class LockOnIndicator : MonoBehaviour
    {
        [Tooltip("비워 두면 같은 오브젝트에서 찾는다.")]
        [SerializeField] LockOnController lockOn;
        [Tooltip("비워 두면 Camera.main을 쓴다.")]
        [SerializeField] Camera viewCamera;

        [SerializeField] float dotSize = 12f;
        [SerializeField] Color dotColor = new Color(1f, 1f, 1f, 0.95f);
        [Tooltip("점 테두리 색. 밝은 배경에서도 보이게 한다.")]
        [SerializeField] Color outlineColor = new Color(0f, 0f, 0f, 0.6f);

        GameObject _canvasObject;
        RectTransform _dot;
        Texture2D _texture;
        Sprite _sprite;

        void Awake()
        {
            if (lockOn == null) lockOn = GetComponent<LockOnController>();
            BuildUI();
        }

        void OnDestroy()
        {
            if (_canvasObject != null) Destroy(_canvasObject);
            if (_sprite != null) Destroy(_sprite);
            if (_texture != null) Destroy(_texture);
        }

        void OnDisable()
        {
            if (_dot != null) _dot.gameObject.SetActive(false);
        }

        void LateUpdate()
        {
            if (viewCamera == null) viewCamera = Camera.main;

            bool visible = false;
            if (lockOn != null && lockOn.IsLockedOn && viewCamera != null)
            {
                Vector3 screen = viewCamera.WorldToScreenPoint(lockOn.Current.AimPoint);
                // z가 음수면 카메라 뒤쪽이므로 그리지 않는다.
                if (screen.z > 0f)
                {
                    _dot.position = new Vector3(screen.x, screen.y, 0f);
                    visible = true;
                }
            }

            if (_dot.gameObject.activeSelf != visible) _dot.gameObject.SetActive(visible);
        }

        void BuildUI()
        {
            _canvasObject = new GameObject("LockOnIndicatorCanvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = _canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            _sprite = CreateDotSprite(64, out _texture);

            var dotObject = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            _dot = (RectTransform)dotObject.transform;
            _dot.SetParent(_canvasObject.transform, false);
            _dot.sizeDelta = new Vector2(dotSize, dotSize);

            var image = dotObject.GetComponent<Image>();
            image.sprite = _sprite;
            image.raycastTarget = false;
            dotObject.SetActive(false);
        }

        /// <summary>테두리가 있는 원 스프라이트를 코드로 만든다. 별도 이미지 에셋이 필요 없다.</summary>
        Sprite CreateDotSprite(int size, out Texture2D texture)
        {
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "LockOnDot",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            float radius = size * 0.5f;
            float innerRadius = radius * 0.7f;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                    // 가장자리를 1픽셀 정도 부드럽게 해서 계단 현상을 줄인다.
                    float outerAlpha = Mathf.Clamp01(radius - d);
                    float innerMix = Mathf.Clamp01(innerRadius - d);
                    Color c = Color.Lerp(outlineColor, dotColor, innerMix);
                    c.a *= outerAlpha;
                    pixels[y * size + x] = c;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();

            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
