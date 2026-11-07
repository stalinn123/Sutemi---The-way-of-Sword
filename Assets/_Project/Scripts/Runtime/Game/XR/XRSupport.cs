using UnityEngine;
using UnityEngine.UI;

namespace Kendo.Game
{
    /// <summary>
    /// Prepared VR layer (OpenXR through the Input System; NOT tested on a headset):
    /// – switches the UI canvases to world space in front of the player and selects buttons with a head-gaze ray and the
    ///   confirm button (B / trigger);
    /// – first-time calibration: stand straight in chudan and press both triggers to set the eye-height offset and
    ///   arm scale (saved in settings);
    /// – tracking loss: when a controller is lost for more than 0.3 s the shinai holds still and the game pauses with a
    ///   notice until tracking returns.
    /// Gameplay is unchanged: the tracked shinai pose feeds the same strike sensor and rules engine as desktop.
    /// </summary>
    public sealed class XRSupport : MonoBehaviour
    {
        private KendoGame game;
        private bool worldSpace;
        private Button gazed;
        private bool calibrating;
        private bool pausedForTracking;

        private void Awake() { game = GetComponent<KendoGame>(); }

        private void Update()
        {
            if (game == null) return;
            bool xr = game.Input.XRActive;
            if (xr != worldSpace) SetWorldSpace(xr);
            if (!xr) return;
            var rig = PlayerRig.Active;
            var f = game.Input.Read();
            if (rig != null)
            {
                PlaceCanvases(rig.head);
                GazeSelect(rig.cam, f);
                Calibrate(rig, f);
                TrackingLoss(f);
            }
        }

        private void SetWorldSpace(bool on)
        {
            worldSpace = on;
            foreach (var c in GetComponentsInChildren<Canvas>(true))
            {
                if (c.transform.parent != null && c.transform.parent.GetComponentInParent<Canvas>() != null) continue;
                c.renderMode = on ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;
                if (on)
                {
                    var rt = (RectTransform)c.transform;
                    rt.sizeDelta = new Vector2(1920, 1080);
                    rt.localScale = Vector3.one * 0.0012f;
                }
            }
        }

        private void PlaceCanvases(Transform head)
        {
            var fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            foreach (var c in GetComponentsInChildren<Canvas>(true))
            {
                if (c.renderMode != RenderMode.WorldSpace) continue;
                var target = head.position + fwd * 1.6f + Vector3.down * 0.15f;
                c.transform.position = Vector3.Lerp(c.transform.position, target, 4f * Time.unscaledDeltaTime);
                c.transform.rotation = Quaternion.LookRotation(fwd);
                c.worldCamera = head.GetComponent<Camera>();
            }
        }

        private void GazeSelect(Camera cam, KendoInputFrame f)
        {
            if (cam == null) return;
            Button best = null;
            var center = new Vector2(cam.pixelWidth / 2f, cam.pixelHeight / 2f);
            foreach (var b in GetComponentsInChildren<Button>(false))
            {
                if (!b.interactable || !b.gameObject.activeInHierarchy) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)b.transform, center, cam)) { best = b; break; }
            }
            if (gazed != best)
            {
                if (gazed != null) gazed.OnDeselect(null);
                gazed = best;
                if (gazed != null) gazed.Select();
            }
            if (gazed != null && (f.confirm || f.rightTrigger > 0.8f)) gazed.onClick.Invoke();
        }

        private void Calibrate(PlayerRig rig, KendoInputFrame f)
        {
            var s = game.Settings;
            if (!calibrating && Mathf.Approximately(s.vrHeightOffset, 0f) && f.headTracked && f.leftTrigger > 0.8f && f.rightTrigger > 0.8f) calibrating = true;
            if (!calibrating) return;
            s.vrHeightOffset = Mathf.Clamp(PlayerRig.EyeHeight - f.head.position.y, -0.5f, 0.5f);
            float handSpan = Vector3.Distance(f.leftHand.position, f.rightHand.position);
            s.vrArmScale = Mathf.Clamp(handSpan > 0.05f ? 0.20f / handSpan : 1f, 0.8f, 1.25f);
            s.Clamp();
            game.Save();
            game.Ui.Toast($"VR calibrated: eye height offset {s.vrHeightOffset:+0.00;-0.00} m.");
            calibrating = false;
        }

        private void TrackingLoss(KendoInputFrame f)
        {
            bool lost = f.tracked && !(f.leftTracked && f.rightTracked);
            if (lost && !pausedForTracking && !game.Paused)
            {
                pausedForTracking = true;
                game.Paused = true;
                game.Ui.Toast("Controller tracking lost – the game is paused until both controllers are tracked again.", 4f);
            }
            else if (!lost && pausedForTracking)
            {
                pausedForTracking = false;
                game.Paused = false;
            }
        }
    }
}
