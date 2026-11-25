using System.Collections.Generic;
using FruitFlyParenting.Comfort;
using FruitFlyParenting.Ending;
using FruitFlyParenting.Tool;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;
using UnityEngine.XR;

namespace FruitFlyParenting.Wheelchair
{
    // Desktop input uses the same wheelchair providers, acceleration and collision body as VR.
    [DefaultExecutionOrder(-225)]
    public sealed class DesktopControls : MonoBehaviour
    {
        readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
        WheelchairLocomotion chair;
        Camera viewer;
        Transform hand;
        TrackedPoseDriver headTracking, handTracking;
        Collider tip;
        LoveStickTool stick;
        Vector3 headPosition, handPosition;
        Quaternion headRotation, handRotation;
        bool headWasEnabled, handWasEnabled, tipWasEnabled, stickWasEnabled;
        bool suppressDrag;
        float yaw, pitch, reach;
        Canvas help;
        CursorLockMode previousLock;
        bool previousVisible;
        public static DesktopControls Current { get; private set; }
        public static string MovementLabel => Current != null && Current.IsDesktopMode ? "방향키/WASD" : "왼손 스틱";
        public bool IsDesktopMode { get; private set; }
        public bool Looking { get; private set; }
        public Vector2 MoveInput { get; private set; }
        public bool ToolExtended => tip != null && tip.enabled && IsDesktopMode;

        void Start()
        {
            chair = GetComponent<WheelchairLocomotion>();
            var origin = GetComponentInChildren<XROrigin>();
            if (origin == null || origin.Camera == null) { enabled = false; return; }
            viewer = origin.Camera; headTracking = viewer.GetComponent<TrackedPoseDriver>();
            var tool = GetComponentInChildren<LoveStickTool>(true);
            stick = tool;
            if (tool != null)
            {
                tip = tool.GetComponent<Collider>();
                handTracking = tool.GetComponentInParent<TrackedPoseDriver>();
                if (handTracking != null) hand = handTracking.transform;
            }
            Current = this;
            Debug.Log($"Player input ready: comfort={GetComponent<ComfortController>() != null}, tool={stick != null}.");
        }
        bool HasXR()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return true;
#else
            SubsystemManager.GetSubsystems(displays);
            foreach (var display in displays) if (display.running) return true;
            // Also respect connected/tracked Input System devices while an XR display is starting.
            foreach (var device in InputSystem.devices)
                if (device is XRController || device is XRHMD) return true;
            return false;
#endif
        }
        void Update()
        {
            if (viewer == null) return;
            SetDesktopMode(!HasXR());
            MoveInput = Vector2.zero;
            if (!IsDesktopMode) return;
            bool focused = Application.isFocused || Application.isBatchMode;
            var mouse = Mouse.current; var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) suppressDrag = true;
            if (mouse == null || !mouse.rightButton.isPressed) suppressDrag = false;
            bool looking = focused && mouse != null && mouse.rightButton.isPressed && !suppressDrag;
            if (Looking != looking)
            {
                Looking = looking;
                Cursor.lockState = looking ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !looking;
            }
            if (focused && chair.enabled && !chair.ComfortInputBlocked && Time.timeScale > 0 && keyboard != null)
            {
                MoveInput = new Vector2(
                    (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed ? 1 : 0) - (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed ? 1 : 0),
                    (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed ? 1 : 0) - (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed ? 1 : 0));
            }
            if (Looking && (ComfortController.Current == null || !ComfortController.Current.AdjustingHeight))
            {
                var delta = mouse.delta.ReadValue();
                if (delta.sqrMagnitude > 0)
                {
                    yaw = Mathf.Repeat(yaw + delta.x * .12f + 180, 360) - 180;
                    pitch = Mathf.Clamp(pitch - delta.y * .12f, -80, 80);
                    viewer.transform.localRotation = headRotation * Quaternion.Euler(pitch, yaw, 0);
                }
            }
            bool useTool = focused && mouse != null && mouse.leftButton.isPressed && chair.enabled &&
                !chair.ComfortInputBlocked && Time.timeScale > 0 && GazeAction.PointerTarget(InteractionRay(viewer)) == null;
            if (tip != null) tip.enabled = useTool;
            if (stick != null) stick.enabled = useTool;
            if (hand != null)
            {
                reach = Mathf.MoveTowards(reach, useTool ? .7f : 0, Time.unscaledDeltaTime * 2);
                var ray = InteractionRay(viewer);
                var grip = viewer.transform.position + viewer.transform.right * .25f - viewer.transform.up * .3f;
                var direction = (ray.GetPoint(1.3f) - grip).normalized;
                var idleGrip = viewer.transform.position + viewer.transform.right * .28f - viewer.transform.up * .33f + viewer.transform.forward * .38f;
                var idleDirection = (viewer.transform.forward + viewer.transform.up * .55f - viewer.transform.right * .12f).normalized;
                float blend = Mathf.Clamp01(reach / .15f);
                hand.SetPositionAndRotation(Vector3.Lerp(idleGrip, grip + direction * reach, blend),
                    Quaternion.Slerp(Quaternion.LookRotation(idleDirection, viewer.transform.up), Quaternion.LookRotation(direction, viewer.transform.up), blend));
            }
            if (help == null) BuildHelp();
        }
        public static Ray InteractionRay(Camera camera)
        {
            var active = Current;
            return active != null && active.IsDesktopMode && !active.Looking && Mouse.current != null
                ? camera.ScreenPointToRay(Mouse.current.position.ReadValue()) : new Ray(camera.transform.position, camera.transform.forward);
        }
        void SetDesktopMode(bool desktop)
        {
            if (desktop == IsDesktopMode) return;
            IsDesktopMode = desktop;
            if (desktop)
            {
                previousLock = Cursor.lockState; previousVisible = Cursor.visible;
                headPosition = viewer.transform.localPosition; headRotation = viewer.transform.localRotation;
                yaw = pitch = reach = 0;
                if (headTracking != null) { headWasEnabled = headTracking.enabled; headTracking.enabled = false; }
                if (hand != null) { handPosition = hand.localPosition; handRotation = hand.localRotation; }
                if (handTracking != null) { handWasEnabled = handTracking.enabled; handTracking.enabled = false; }
                if (tip != null) { tipWasEnabled = tip.enabled; tip.enabled = false; }
                if (stick != null) { stickWasEnabled = stick.enabled; stick.enabled = false; }
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            }
            else
            {
                MoveInput = Vector2.zero; Looking = false;
                viewer.transform.SetLocalPositionAndRotation(headPosition, headRotation);
                if (hand != null) hand.SetLocalPositionAndRotation(handPosition, handRotation);
                if (headTracking != null) headTracking.enabled = headWasEnabled;
                if (handTracking != null) handTracking.enabled = handWasEnabled;
                if (tip != null) tip.enabled = tipWasEnabled;
                if (stick != null) stick.enabled = stickWasEnabled;
                Cursor.lockState = previousLock; Cursor.visible = previousVisible;
            }
            if (help != null) help.gameObject.SetActive(desktop);
            Debug.Log(desktop ? "Input mode: desktop keyboard/mouse." : "Input mode: XR tracking/controllers.");
        }
        void BuildHelp()
        {
            var comfort = GetComponent<ComfortController>();
            var font = comfort != null ? comfort.InterfaceFont : null;
            if (font == null) return;
            var root = new GameObject("Desktop controls", typeof(Canvas)); help = root.GetComponent<Canvas>();
            root.AddComponent<FruitFlyParenting.UI.DesktopJournalHelp>().Build(font);
        }
        void OnDisable()
        {
            if (IsDesktopMode && viewer != null) SetDesktopMode(false);
            if (Current == this) Current = null;
        }
        void OnEnable() { if (viewer != null) Current = this; }
        void OnDestroy() { if (help != null) Destroy(help.gameObject); }
    }
}
