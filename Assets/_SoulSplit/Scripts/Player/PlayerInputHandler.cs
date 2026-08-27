using UnityEngine;
using UnityEngine.InputSystem;

namespace SoulSplit.Player
{
    /// <summary>
    /// Girdi okumanin TEK sorumlusu. Input System'i disariya sade property'ler
    /// olarak acar; hicbir fizik veya oyun mantigi icermez.
    /// Girdi Update icinde okunur, fizik baska scriptte FixedUpdate'te uygulanir.
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        [Header("Input Asset")]
        [Tooltip("Assets/_SoulSplit/Input/SoulSplitControls.inputactions dosyasini surukle.")]
        [SerializeField] private InputActionAsset inputActions;

        [Header("Action Map / Action Isimleri")]
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string moveActionName = "Move";
        [SerializeField] private string jumpActionName = "Jump";
        [SerializeField] private string attackActionName = "Attack";
        [SerializeField] private string heavyAttackActionName = "HeavyAttack";
        [SerializeField] private string soulSwitchActionName = "SoulSwitch";
        [SerializeField] private string ultimateActionName = "Ultimate";

        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _attackAction;
        private InputAction _heavyAttackAction;
        private InputAction _soulSwitchAction;
        private InputAction _ultimateAction;

        /// <summary>Ham yon girdisi (-1..1).</summary>
        public Vector2 MoveInput { get; private set; }
        /// <summary>Yatay girdinin -1 / 0 / 1 hali; olu bolge uygulanmis.</summary>
        public float HorizontalInput { get; private set; }
        /// <summary>Ziplama tusuna bu karede basildi mi?</summary>
        public bool JumpPressedThisFrame { get; private set; }
        /// <summary>Ziplama tusu su an basili tutuluyor mu? (degisken ziplama yuksekligi icin)</summary>
        public bool JumpHeld { get; private set; }
        /// <summary>Ziplama tusu bu karede birakildi mi?</summary>
        public bool JumpReleasedThisFrame { get; private set; }
        public bool AttackPressedThisFrame { get; private set; }
        /// <summary>Agir saldiri tusuna bu karede basildi mi?</summary>
        public bool HeavyAttackPressedThisFrame { get; private set; }
        public bool SoulSwitchPressedThisFrame { get; private set; }
        /// <summary>Ultimate gucunu etkinlestirme tusuna bu karede basildi mi?</summary>
        public bool UltimatePressedThisFrame { get; private set; }

        [Header("Olu Bolge")]
        [Range(0f, 0.9f)]
        [SerializeField] private float deadZone = 0.2f;

        private void Awake()
        {
            if (inputActions == null)
            {
                Debug.LogError("[PlayerInputHandler] Input Action Asset atanmamis!", this);
                enabled = false;
                return;
            }

            InputActionMap map = inputActions.FindActionMap(actionMapName, throwIfNotFound: true);
            _moveAction = map.FindAction(moveActionName, throwIfNotFound: true);
            _jumpAction = map.FindAction(jumpActionName, throwIfNotFound: true);
            _attackAction = map.FindAction(attackActionName, throwIfNotFound: true);
            _heavyAttackAction = map.FindAction(heavyAttackActionName, throwIfNotFound: true);
            _soulSwitchAction = map.FindAction(soulSwitchActionName, throwIfNotFound: true);
            string resolvedUltimateName = string.IsNullOrWhiteSpace(ultimateActionName) ? "Ultimate" : ultimateActionName;
            _ultimateAction = map.FindAction(resolvedUltimateName, throwIfNotFound: true);
        }

        private void OnEnable()
        {
            if (inputActions != null) inputActions.FindActionMap(actionMapName)?.Enable();
        }

        private void OnDisable()
        {
            if (inputActions != null) inputActions.FindActionMap(actionMapName)?.Disable();
        }

        private void Update()
        {
            MoveInput = _moveAction.ReadValue<Vector2>();

            // Olu bolge: analog cubukta hafif kaymalar hareket baslatmasin.
            float x = MoveInput.x;
            HorizontalInput = Mathf.Abs(x) < deadZone ? 0f : Mathf.Sign(x) * Mathf.InverseLerp(deadZone, 1f, Mathf.Abs(x));

            JumpPressedThisFrame = _jumpAction.WasPressedThisFrame();
            JumpReleasedThisFrame = _jumpAction.WasReleasedThisFrame();
            JumpHeld = _jumpAction.IsPressed();
            AttackPressedThisFrame = _attackAction.WasPressedThisFrame();
            HeavyAttackPressedThisFrame = _heavyAttackAction.WasPressedThisFrame();
            SoulSwitchPressedThisFrame = _soulSwitchAction.WasPressedThisFrame();
            UltimatePressedThisFrame = _ultimateAction.WasPressedThisFrame();
        }
    }
}
