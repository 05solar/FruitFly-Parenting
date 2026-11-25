using FruitFlyParenting.Data;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning;

namespace FruitFlyParenting.Wheelchair
{
    [DefaultExecutionOrder(-220)] // XRI providers(-210)와 BodyTransformer(-205)보다 먼저 입력 처리
    public sealed class WheelchairLocomotion : MonoBehaviour, IXRBodyPositionEvaluator
    {
        [SerializeField] GameBalanceConfig config;
        [SerializeField] InputActionReference leftStick;
        [SerializeField] ContinuousMoveProvider move;
        [SerializeField] ContinuousTurnProvider turn;
        WheelchairMotionModel motion;
        Vector3 lastPhysicsPosition;
        public Vector3 ActualVelocity { get; private set; }
        public InputActionReference LeftStick => leftStick;
        public bool ComfortInputBlocked { get; set; }
        public float ComfortTurnSpeed { get; set; } = 30f;
        public float CommandedTurnSpeed => motion?.TurnSpeed ?? 0;
        DesktopControls desktop;

        void Awake()
        {
            if (GetComponent<PlayerEquipmentArt>() == null) gameObject.AddComponent<PlayerEquipmentArt>();
#if UNITY_EDITOR || UNITY_STANDALONE
            desktop = GetComponent<DesktopControls>() ?? gameObject.AddComponent<DesktopControls>();
#endif
        }

        public void Configure(GameBalanceConfig balance, InputActionReference input, ContinuousMoveProvider moveProvider, ContinuousTurnProvider turnProvider)
        {
            config = balance; leftStick = input; move = moveProvider; turn = turnProvider;
        }

        void OnEnable()
        {
            if (config == null || move == null || turn == null) return;
            motion = new WheelchairMotionModel(config);
            lastPhysicsPosition = transform.position;
            move.moveSpeed = config.wheelchairMaxSpeed;
            move.enableStrafe = false;
            move.useGravity = false; // Grounded chair: no gravity-driven motion with a neutral stick.
            move.forwardSource = transform; // 머리나 오른손 방향에 따라 휠체어가 움직이지 않는다.
            move.inAirControlModifier = 1f;
            turn.turnSpeed = config.wheelchairTurnSpeed;
            turn.enableTurnAround = false;
            move.leftHandMoveInput.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;
            turn.leftHandTurnInput.inputSourceMode = XRInputValueReader.InputSourceMode.ManualValue;
            move.rightHandMoveInput.inputSourceMode = XRInputValueReader.InputSourceMode.Unused;
            turn.rightHandTurnInput.inputSourceMode = XRInputValueReader.InputSourceMode.Unused;
        }

        void Update()
        {
            if (motion == null) return;
            // Keyboard travel is faster; keep the same acceleration profile and collision provider.
            bool desktopMode = desktop != null && desktop.IsDesktopMode;
            move.moveSpeed = config.wheelchairMaxSpeed * (desktopMode ? 4f : 1f);
            float turnLimit = Mathf.Clamp(ComfortTurnSpeed, 10f, 45f);
            turn.turnSpeed = turnLimit;
            Vector2 input = desktopMode ? desktop.MoveInput :
                (leftStick != null ? leftStick.action.ReadValue<Vector2>() : Vector2.zero);
            motion.Step(!ComfortInputBlocked ? input : Vector2.zero, Time.deltaTime, turnLimit);
            move.leftHandMoveInput.manualValue = new Vector2(0f, config.wheelchairMaxSpeed > 0f ? motion.Speed / config.wheelchairMaxSpeed : 0f);
            turn.leftHandTurnInput.manualValue = new Vector2(motion.TurnSpeed / turnLimit, 0f);
        }

        void FixedUpdate()
        {
            ActualVelocity = Vector3.ProjectOnPlane((transform.position - lastPhysicsPosition) / Time.fixedDeltaTime, Vector3.up);
            lastPhysicsPosition = transform.position;
        }

        void OnDisable()
        {
            if (move != null) move.leftHandMoveInput.manualValue = Vector2.zero;
            if (turn != null) turn.leftHandTurnInput.manualValue = Vector2.zero;
            ActualVelocity = Vector3.zero;
        }

        // 휠체어는 고정된 차체 중심으로 회전한다. HMD를 기울여도 충돌체 중심이 이동하지 않는다.
        public Vector3 GetBodyGroundLocalPosition(XROrigin xrOrigin) => Vector3.zero;
    }
}
