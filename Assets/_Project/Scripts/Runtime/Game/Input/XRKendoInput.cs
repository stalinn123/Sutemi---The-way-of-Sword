using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Kendo.Game
{
    /// <summary>
    /// OpenXR input through Input System bindings (no XRI dependency at compile time). Produces the same
    /// <see cref="KendoInputFrame"/> as the desktop: tracked head + both controller poses, grip/trigger values and the
    /// intents (thumbstick suriashi, buttons). NOT tested on a headset in this project – see Docs/VR.md.
    /// Bindings: left stick = suriashi, right stick = snap turn, grips = hold the shinai, A = kiai, B = interact/confirm,
    /// X = toggle kamae, Y = bow, left menu = pause, right stick click = skip.
    /// </summary>
    public sealed class XRKendoInput : IKendoInputSource
    {
        public string Name => "OpenXR";
#if ENABLE_INPUT_SYSTEM
        private readonly InputAction headPos, headRot, headTracked;
        private readonly InputAction lPos, lRot, lTracked, lGrip, lTrigger, lStick, lPrimary, lSecondary, lMenu;
        private readonly InputAction rPos, rRot, rTracked, rGrip, rTrigger, rStick, rPrimary, rSecondary, rStickClick;
        private bool prevA, prevB, prevX, prevY, prevMenu, prevClick;

        private static InputAction Make(string name, InputActionType type, params string[] bindings)
        {
            var a = new InputAction(name, type);
            foreach (var b in bindings) a.AddBinding(b);
            a.Enable();
            return a;
        }

        public XRKendoInput()
        {
            headPos = Make("headPos", InputActionType.Value, "<XRHMD>/centerEyePosition", "<XRHMD>/devicePosition");
            headRot = Make("headRot", InputActionType.Value, "<XRHMD>/centerEyeRotation", "<XRHMD>/deviceRotation");
            headTracked = Make("headTracked", InputActionType.Button, "<XRHMD>/isTracked");
            lPos = Make("lPos", InputActionType.Value, "<XRController>{LeftHand}/devicePosition");
            lRot = Make("lRot", InputActionType.Value, "<XRController>{LeftHand}/deviceRotation");
            lTracked = Make("lTracked", InputActionType.Button, "<XRController>{LeftHand}/isTracked");
            lGrip = Make("lGrip", InputActionType.Value, "<XRController>{LeftHand}/grip");
            lTrigger = Make("lTrigger", InputActionType.Value, "<XRController>{LeftHand}/trigger");
            lStick = Make("lStick", InputActionType.Value, "<XRController>{LeftHand}/primary2DAxis", "<XRController>{LeftHand}/thumbstick");
            lPrimary = Make("lX", InputActionType.Button, "<XRController>{LeftHand}/primaryButton");
            lSecondary = Make("lY", InputActionType.Button, "<XRController>{LeftHand}/secondaryButton");
            lMenu = Make("lMenu", InputActionType.Button, "<XRController>{LeftHand}/menu", "<XRController>{LeftHand}/menuButton");
            rPos = Make("rPos", InputActionType.Value, "<XRController>{RightHand}/devicePosition");
            rRot = Make("rRot", InputActionType.Value, "<XRController>{RightHand}/deviceRotation");
            rTracked = Make("rTracked", InputActionType.Button, "<XRController>{RightHand}/isTracked");
            rGrip = Make("rGrip", InputActionType.Value, "<XRController>{RightHand}/grip");
            rTrigger = Make("rTrigger", InputActionType.Value, "<XRController>{RightHand}/trigger");
            rStick = Make("rStick", InputActionType.Value, "<XRController>{RightHand}/primary2DAxis", "<XRController>{RightHand}/thumbstick");
            rPrimary = Make("rA", InputActionType.Button, "<XRController>{RightHand}/primaryButton");
            rSecondary = Make("rB", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
            rStickClick = Make("rClick", InputActionType.Button, "<XRController>{RightHand}/primary2DAxisClick", "<XRController>{RightHand}/thumbstickClicked");
        }
#endif

        public bool DeviceActive => UnityEngine.XR.XRSettings.isDeviceActive;

        public KendoInputFrame Read()
        {
            var f = new KendoInputFrame { tracked = true };
#if ENABLE_INPUT_SYSTEM
            f.head = new Pose(headPos.ReadValue<Vector3>(), headRot.ReadValue<Quaternion>());
            f.leftHand = new Pose(lPos.ReadValue<Vector3>(), lRot.ReadValue<Quaternion>());
            f.rightHand = new Pose(rPos.ReadValue<Vector3>(), rRot.ReadValue<Quaternion>());
            f.headTracked = headTracked.ReadValue<float>() > 0.5f || f.head.rotation != new Quaternion(0, 0, 0, 0);
            f.leftTracked = lTracked.ReadValue<float>() > 0.5f;
            f.rightTracked = rTracked.ReadValue<float>() > 0.5f;
            f.leftGrip = lGrip.ReadValue<float>();
            f.rightGrip = rGrip.ReadValue<float>();
            f.leftTrigger = lTrigger.ReadValue<float>();
            f.rightTrigger = rTrigger.ReadValue<float>();
            var ls = lStick.ReadValue<Vector2>();
            f.move = new Vector2(Mathf.Abs(ls.x) > 0.6f ? Mathf.Sign(ls.x) : 0f, Mathf.Abs(ls.y) > 0.6f ? Mathf.Sign(ls.y) : 0f);
            var rs = rStick.ReadValue<Vector2>();
            f.look = new Vector2(Mathf.Abs(rs.x) > 0.7f ? Mathf.Sign(rs.x) : 0f, 0f);
            bool a = rPrimary.ReadValue<float>() > 0.5f, b = rSecondary.ReadValue<float>() > 0.5f;
            bool x = lPrimary.ReadValue<float>() > 0.5f, y = lSecondary.ReadValue<float>() > 0.5f;
            bool menu = lMenu.ReadValue<float>() > 0.5f, click = rStickClick.ReadValue<float>() > 0.5f;
            f.kiai = a && !prevA;
            f.interact = f.confirm = b && !prevB;
            f.toggleKamae = x && !prevX;
            f.bowHeld = y;
            f.pause = menu && !prevMenu;
            f.skip = click && !prevClick;
            f.blockHeld = false; // in VR the block is the real shinai position
            prevA = a; prevB = b; prevX = x; prevY = y; prevMenu = menu; prevClick = click;
#endif
            return f;
        }
    }
}
