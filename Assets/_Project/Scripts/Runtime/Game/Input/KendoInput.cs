using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Kendo.Game
{
    public enum StrikeCommand { None, Men, Kote, Do, Tsuki }

    /// <summary>
    /// The one input representation every consumer reads. Desktop, OpenXR and scripted tests fill the same frame;
    /// gameplay never asks which device produced it. Buttons are "pressed this frame" edges; holds are levels.
    /// When <see cref="tracked"/> is true the head/hand poses are real (XR) and drive the camera and shinai directly.
    /// </summary>
    public struct KendoInputFrame
    {
        public Vector2 move;          // x = right, y = forward (suriashi intent)
        public Vector2 look;          // pixels (desktop) or snap-turn intent (XR)
        public bool aimHeld;          // fine shinai aim instead of camera
        public StrikeCommand strike;  // edge
        public bool blockHeld;
        public bool kiai;             // edge
        public bool toggleKamae;      // edge
        public bool bowHeld;
        public bool interact;         // edge (F / Enter / trigger)
        public bool confirm;          // edge (Enter / F / click / A)
        public bool back;             // edge (Backspace / B)
        public bool skip;             // edge (Tab)
        public bool pause;            // edge (Esc / menu)
        public bool freeCursor;       // hold (Left Alt)
        public bool replayDemo;       // edge (G)
        public bool toggleControls;   // edge (F1)

        public bool tracked;
        public Pose head, leftHand, rightHand;
        public float leftGrip, rightGrip, leftTrigger, rightTrigger;
        public bool headTracked, leftTracked, rightTracked;
    }

    public interface IKendoInputSource
    {
        KendoInputFrame Read();
        string Name { get; }
    }

    /// <summary>Keyboard + mouse (Input System; falls back to the legacy Input Manager if the project uses it).</summary>
    public sealed class DesktopKendoInput : IKendoInputSource
    {
        public string Name => "Keyboard & mouse";

        public KendoInputFrame Read()
        {
            var f = new KendoInputFrame();
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            var m = Mouse.current;
            if (k != null)
            {
                f.move = new Vector2((k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0), (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0));
                if (k.qKey.wasPressedThisFrame) f.strike = StrikeCommand.Men;
                if (k.eKey.wasPressedThisFrame) f.strike = StrikeCommand.Kote;
                if (k.rKey.wasPressedThisFrame) f.strike = StrikeCommand.Do;
                if (k.tKey.wasPressedThisFrame) f.strike = StrikeCommand.Tsuki;
                f.blockHeld = k.leftShiftKey.isPressed || k.rightShiftKey.isPressed;
                f.kiai = k.spaceKey.wasPressedThisFrame;
                f.toggleKamae = k.xKey.wasPressedThisFrame;
                f.bowHeld = k.bKey.isPressed;
                f.interact = k.fKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame;
                f.confirm = f.interact;
                f.back = k.backspaceKey.wasPressedThisFrame;
                f.skip = k.tabKey.wasPressedThisFrame;
                f.pause = k.escapeKey.wasPressedThisFrame;
                f.freeCursor = k.leftAltKey.isPressed;
                f.replayDemo = k.gKey.wasPressedThisFrame;
                f.toggleControls = k.f1Key.wasPressedThisFrame;
            }
            if (m != null)
            {
                f.look = m.delta.ReadValue();
                f.aimHeld = m.rightButton.isPressed;
                if (m.leftButton.wasPressedThisFrame && f.strike == StrikeCommand.None) f.strike = StrikeCommand.Men;
            }
#else
            f.move = new Vector2((Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0), (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
            if (Input.GetKeyDown(KeyCode.Q) || Input.GetMouseButtonDown(0)) f.strike = StrikeCommand.Men;
            if (Input.GetKeyDown(KeyCode.E)) f.strike = StrikeCommand.Kote;
            if (Input.GetKeyDown(KeyCode.R)) f.strike = StrikeCommand.Do;
            if (Input.GetKeyDown(KeyCode.T)) f.strike = StrikeCommand.Tsuki;
            f.blockHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            f.kiai = Input.GetKeyDown(KeyCode.Space);
            f.toggleKamae = Input.GetKeyDown(KeyCode.X);
            f.bowHeld = Input.GetKey(KeyCode.B);
            f.interact = Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            f.confirm = f.interact;
            f.back = Input.GetKeyDown(KeyCode.Backspace);
            f.skip = Input.GetKeyDown(KeyCode.Tab);
            f.pause = Input.GetKeyDown(KeyCode.Escape);
            f.freeCursor = Input.GetKey(KeyCode.LeftAlt);
            f.replayDemo = Input.GetKeyDown(KeyCode.G);
            f.toggleControls = Input.GetKeyDown(KeyCode.F1);
            f.look = new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 20f;
            f.aimHeld = Input.GetMouseButton(1);
#endif
            return f;
        }
    }

    /// <summary>Feeds a script of frames (PlayMode tests, demos, replays). Edges are delivered on one frame only.</summary>
    public sealed class ScriptedKendoInput : IKendoInputSource
    {
        public KendoInputFrame next;
        public string Name => "Scripted";

        public KendoInputFrame Read()
        {
            var f = next;
            // clear edges so they fire exactly once; keep levels
            next.strike = StrikeCommand.None;
            next.kiai = next.toggleKamae = next.interact = next.confirm = next.back = next.skip = next.pause = next.replayDemo = next.toggleControls = false;
            next.look = Vector2.zero;
            return f;
        }
    }

    /// <summary>Chooses the source: XR when a headset is active, otherwise desktop. Tests can override it.</summary>
    public sealed class KendoInputRouter
    {
        private readonly DesktopKendoInput desktop = new DesktopKendoInput();
        private XRKendoInput xr;
        public IKendoInputSource Override;
        public KendoInputFrame Current { get; private set; }
        private int frame = -1;

        public bool XRActive => xr != null && xr.DeviceActive;
        public string SourceName => Override != null ? Override.Name : (XRActive ? xr.Name : desktop.Name);

        public void EnableXR(XRKendoInput source) { xr = source; }

        /// <summary>Reads once per rendered frame; every consumer sees the same frame.</summary>
        public KendoInputFrame Read()
        {
            if (frame == Time.frameCount) return Current;
            frame = Time.frameCount;
            if (Override != null) Current = Override.Read();
            else if (XRActive) Current = xr.Read();
            else Current = desktop.Read();
            return Current;
        }
    }
}
