using UnityEngine;
using UnityEngine.InputSystem;

namespace Soulslike
{
    /// <summary>
    /// 키보드·마우스·게임패드 입력을 읽어 게임 로직이 쓰기 쉬운 값으로 노출한다.
    /// InputAction은 이 컴포넌트가 생성·소유하고, 파괴될 때 함께 정리한다.
    /// 다른 컴포넌트가 같은 프레임의 입력을 읽도록 먼저 실행한다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class PlayerInputReader : MonoBehaviour
    {
        [Tooltip("패드 B(○)를 이 시간(초)보다 짧게 눌렀다 떼면 구르기, 길게 누르면 질주다.")]
        [SerializeField] float padTapThreshold = 0.25f;

        InputAction _move;
        InputAction _mouseLook;
        InputAction _stickLook;
        InputAction _sprint;
        InputAction _walk;
        InputAction _lockOn;
        InputAction _roll;
        InputAction _padDodge;
        InputAction _jump;
        InputAction _lightAttack;
        InputAction _aim;
        InputAction _slot1;
        InputAction _slot2;
        InputAction _weaponPrev;
        InputAction _weaponNext;

        float _padDodgeHeldTime;
        bool _padDodgeHolding;

        /// <summary>이동 입력. 크기는 0~1이다(스틱은 기울기에 비례, 키보드는 0 또는 1).</summary>
        public Vector2 Move { get; private set; }

        /// <summary>이번 프레임의 마우스 이동량(픽셀). 프레임당 값이므로 deltaTime을 곱하지 않는다.</summary>
        public Vector2 MouseLookDelta { get; private set; }

        /// <summary>우측 스틱 기울기(-1~1). 초당 회전 속도에 곱해서 쓴다.</summary>
        public Vector2 StickLook { get; private set; }

        public bool SprintHeld { get; private set; }
        public bool WalkHeld => _walk.IsPressed();
        public bool LockOnPressedThisFrame => _lockOn.WasPressedThisFrame();
        public bool RollPressedThisFrame { get; private set; }
        public bool JumpPressedThisFrame => _jump.WasPressedThisFrame();
        /// <summary>약공격. 마우스 왼쪽 / 패드 R1.</summary>
        public bool LightAttackPressedThisFrame { get; private set; }
        /// <summary>조준(활). 마우스 오른쪽 / 패드 L2를 누르고 있는 동안 참이다.</summary>
        // 커서가 풀려 있을 때의 마우스 우클릭은 무시한다. 좌클릭 발사와 같은 조건으로 맞춘다(QA B2-10).
        public bool AimHeld => _aim.IsPressed() &&
            (!(_aim.activeControl?.device is Mouse) || Cursor.lockState == CursorLockMode.Locked);
        /// <summary>이번 프레임에 누른 무기 슬롯 번호(0부터). 없으면 -1. 키보드 1·2.</summary>
        public int WeaponSlotPressedThisFrame { get; private set; } = -1;
        /// <summary>패드 십자키 좌우로 무기 순환: -1 이전, +1 다음, 0 없음.</summary>
        public int WeaponCycleThisFrame { get; private set; }

        void Awake()
        {
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _move.AddBinding("<Gamepad>/leftStick");

            _mouseLook = new InputAction("MouseLook", InputActionType.Value, "<Mouse>/delta", expectedControlType: "Vector2");
            _stickLook = new InputAction("StickLook", InputActionType.Value, "<Gamepad>/rightStick", expectedControlType: "Vector2");

            // 키보드: Shift 누르고 있기 = 질주, Ctrl(이슈 #1 지정) 또는 Space = 구르기.
            _sprint = new InputAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            _roll = new InputAction("Roll", InputActionType.Button, "<Keyboard>/leftCtrl");
            _roll.AddBinding("<Keyboard>/space");

            // 약공격: 마우스 왼쪽 / 패드 R1.
            _lightAttack = new InputAction("LightAttack", InputActionType.Button, "<Mouse>/leftButton");
            _lightAttack.AddBinding("<Gamepad>/rightShoulder");

            // 조준(활): 마우스 오른쪽 / 패드 L2 누르고 있기. 조준 중 약공격 버튼이 발사다.
            _aim = new InputAction("Aim", InputActionType.Button, "<Mouse>/rightButton");
            _aim.AddBinding("<Gamepad>/leftTrigger");

            // 무기 교체: 숫자 1(칼)·2(활), 패드 십자키 좌우로 순환.
            _slot1 = new InputAction("WeaponSlot1", InputActionType.Button, "<Keyboard>/1");
            _slot2 = new InputAction("WeaponSlot2", InputActionType.Button, "<Keyboard>/2");
            _weaponPrev = new InputAction("WeaponPrev", InputActionType.Button, "<Gamepad>/dpad/left");
            _weaponNext = new InputAction("WeaponNext", InputActionType.Button, "<Gamepad>/dpad/right");

            // 패드: 소울라이크 관례대로 B(○) 하나로 짧게 누르면 구르기, 길게 누르면 질주다.
            _padDodge = new InputAction("PadDodge", InputActionType.Button, "<Gamepad>/buttonEast");

            // 점프: 키보드 F, 패드 A(×).
            _jump = new InputAction("Jump", InputActionType.Button, "<Keyboard>/f");
            _jump.AddBinding("<Gamepad>/buttonSouth");

            // 키보드는 아날로그 입력이 없으므로 걷기 키를 따로 둔다. 패드는 스틱을 살짝 기울이면 걷는다.
            _walk = new InputAction("Walk", InputActionType.Button, "<Keyboard>/leftAlt");

            // 록온: 키보드 Q, 마우스 휠 클릭, 패드 R3.
            _lockOn = new InputAction("LockOn", InputActionType.Button, "<Keyboard>/q");
            _lockOn.AddBinding("<Mouse>/middleButton");
            _lockOn.AddBinding("<Gamepad>/rightStickPress");
        }

        InputAction[] AllActions => new[]
        {
            _move, _mouseLook, _stickLook, _sprint, _walk, _lockOn, _roll, _padDodge, _jump, _lightAttack,
            _aim, _slot1, _slot2, _weaponPrev, _weaponNext,
        };

        void OnEnable()
        {
            foreach (InputAction action in AllActions) action.Enable();
        }

        void OnDisable()
        {
            foreach (InputAction action in AllActions) action.Disable();
            Move = Vector2.zero;
            MouseLookDelta = Vector2.zero;
            StickLook = Vector2.zero;
            SprintHeld = false;
            RollPressedThisFrame = false;
            LightAttackPressedThisFrame = false;
            WeaponSlotPressedThisFrame = -1;
            WeaponCycleThisFrame = 0;
            _padDodgeHolding = false;
        }

        void OnDestroy()
        {
            foreach (InputAction action in AllActions) action?.Dispose();
        }

        void Update()
        {
            // 대각선 키 입력(W+D)이 √2 크기가 되지 않도록 1로 제한한다.
            Move = Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f);
            MouseLookDelta = _mouseLook.ReadValue<Vector2>();
            StickLook = _stickLook.ReadValue<Vector2>();

            bool padRoll = false;
            bool padSprint = false;
            if (_padDodge.WasPressedThisFrame())
            {
                _padDodgeHolding = true;
                _padDodgeHeldTime = 0f;
            }
            if (_padDodgeHolding)
            {
                if (_padDodge.IsPressed())
                {
                    _padDodgeHeldTime += Time.unscaledDeltaTime;
                    padSprint = _padDodgeHeldTime >= padTapThreshold;
                }
                else
                {
                    // 뗄 때 판정한다. 짧게 눌렀으면 구르기다.
                    padRoll = _padDodgeHeldTime < padTapThreshold;
                    _padDodgeHolding = false;
                }
            }

            SprintHeld = _sprint.IsPressed() || padSprint;
            RollPressedThisFrame = _roll.WasPressedThisFrame() || padRoll;

            // 커서가 풀려 있을 때의 마우스 클릭은 커서 재잠금용이므로 공격으로 보지 않는다.
            LightAttackPressedThisFrame = _lightAttack.WasPressedThisFrame() &&
                (!(_lightAttack.activeControl?.device is Mouse) || Cursor.lockState == CursorLockMode.Locked);

            WeaponSlotPressedThisFrame = _slot1.WasPressedThisFrame() ? 0 : _slot2.WasPressedThisFrame() ? 1 : -1;
            WeaponCycleThisFrame = _weaponNext.WasPressedThisFrame() ? 1 : _weaponPrev.WasPressedThisFrame() ? -1 : 0;
        }
    }
}
