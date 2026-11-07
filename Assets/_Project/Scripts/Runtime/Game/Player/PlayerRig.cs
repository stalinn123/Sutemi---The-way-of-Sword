using System;
using System.Collections.Generic;
using Kendo.AI;
using Kendo.Rules;
using UnityEngine;

namespace Kendo.Game
{
    /// <summary>Something the player can use with F / Enter (master, lesson board, level board, badge shelf…).</summary>
    public interface IInteractable
    {
        string Prompt { get; }
        bool CanInteract { get; }
        void Interact();
        Vector3 Position { get; }
    }

    public sealed class Interactable : MonoBehaviour, IInteractable
    {
        public string prompt = "Interact";
        public float range = 2.6f;
        public Action action;
        public Func<bool> available;
        public string Prompt => prompt;
        public bool CanInteract => available == null || available();
        public void Interact() => action?.Invoke();
        public Vector3 Position => transform.position;
        public static readonly List<Interactable> All = new List<Interactable>();
        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);
    }

    /// <summary>
    /// The first-person player: CharacterController body, head camera, the supplied shinai in two procedural hands,
    /// suriashi footwork, mouse look, fine aim, rei, interaction – all driven by <see cref="KendoInputFrame"/>.
    /// In VR the same rig follows the tracked head and builds the shinai pose from both controllers.
    /// </summary>
    [DefaultExecutionOrder(30)]
    public sealed class PlayerRig : MonoBehaviour
    {
        public const float Height = 1.75f, EyeHeight = 1.62f;
        public static PlayerRig Active { get; private set; }

        public Fighter fighter;
        public Camera cam;
        public Transform head;
        public CharacterController controller;
        public PlayerArms arms;
        public float yaw, pitch;
        public float bowAngle;           // current rei depth (deg)
        public float bowHoldAtDepth;     // seconds held near the deepest point
        public float maxBowThisBow;
        public bool Bowing => bowAngle > 3f;
        public event Action<float, float> BowFinished; // depth, hold
        public Interactable Focus { get; private set; }
        public bool InputEnabled = true;
        public bool MovementEnabled = true;
        public bool StrikesEnabled = true;
        public float stepLengthScale = 1f;
        public Func<DuelAction, Vector3?> aimPointWorld; // lessons/combat: where the cut is aimed (null = gaze)
        public event Action<DuelAction> CommandIssued;   // for the imitation learner (Agent 2)

        private KendoGame game;
        private float vy;
        private float lastStepCommand;
        private Vector3 lastPos;
        private float pace = 1f;
        private readonly XRShinaiTracker xrTracker = new XRShinaiTracker();

        public static PlayerRig Create(Vector3 position, Quaternion rotation, KendoGameConfig cfg, LayerMask environment)
        {
            var go = new GameObject("Player");
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, rotation.eulerAngles.y, 0f));
            go.layer = Layers.Characters;
            var rig = go.AddComponent<PlayerRig>();
            rig.Build(cfg, environment);
            return rig;
        }

        private void Build(KendoGameConfig cfg, LayerMask environment)
        {
            game = KendoGame.Instance;
            controller = gameObject.AddComponent<CharacterController>();
            controller.height = Height;
            controller.radius = 0.25f;
            controller.center = new Vector3(0f, Height / 2f, 0f);
            controller.stepOffset = 0.05f;
            controller.slopeLimit = 35f;
            controller.skinWidth = 0.03f;

            head = new GameObject("Head").transform;
            head.SetParent(transform, false);
            head.localPosition = new Vector3(0f, EyeHeight, 0f);
            cam = head.gameObject.AddComponent<Camera>();
            cam.nearClipPlane = 0.04f;
            cam.farClipPlane = 600f;
            cam.fieldOfView = game != null ? game.Settings.fov : 70f;
            head.gameObject.AddComponent<AudioListener>();
            head.tag = "MainCamera";

            fighter = gameObject.AddComponent<Fighter>();
            fighter.side = 0;
            fighter.displayName = "You";
            fighter.headPoint = head;
            var shinaiRig = gameObject.AddComponent<ShinaiRig>();
            Transform shinai = cfg != null && cfg.shinaiPrefab != null ? Instantiate(cfg.shinaiPrefab).transform : null;
            if (shinai == null) Debug.LogError("[Kendo] Shinai prefab missing – run Kendo ▸ Build Game.");
            else
            {
                shinai.name = "Shinai (player)";
                shinai.SetParent(transform, true);
                SetLayer(shinai.gameObject, Layers.Shinai);
            }
            shinaiRig.Init(shinai, transform, 1.2f, environment);
            fighter.Setup(shinaiRig, Height);
            fighter.aimOverride = AimFor;
            BuildZones();
            arms = gameObject.AddComponent<PlayerArms>();
            arms.Init(this);
            lastPos = transform.position;
            yaw = transform.eulerAngles.y;
            Active = this;
        }

        private static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayer(t.gameObject, layer);
        }

        private Transform Anchor(string name, Transform parent, Vector3 local)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = local;
            return t;
        }

        /// <summary>The player's own datotsu-bui (targets for the opponent).</summary>
        private void BuildZones()
        {
            var men = Anchor("Zone_Men", head, new Vector3(0f, 0.13f, 0.0f));
            var throat = Anchor("Zone_Tsuki", transform, new Vector3(0f, 1.43f, 0.12f));
            var doR = Anchor("Zone_DoR", transform, new Vector3(0.14f, 1.08f, 0.10f));
            var doL = Anchor("Zone_DoL", transform, new Vector3(-0.14f, 1.08f, 0.10f));
            fighter.zones.Add(new HitZone { target = Target.Men, anchor = men, radius = 0.12f });
            fighter.zones.Add(new HitZone { target = Target.Tsuki, anchor = throat, a = Vector3.down * 0.03f, b = Vector3.up * 0.03f, radius = 0.05f });
            fighter.zones.Add(new HitZone { target = Target.DoRight, anchor = doR, a = Vector3.down * 0.08f, b = Vector3.up * 0.08f, radius = 0.1f });
            fighter.zones.Add(new HitZone { target = Target.DoLeft, anchor = doL, a = Vector3.down * 0.08f, b = Vector3.up * 0.08f, radius = 0.1f });
        }

        public void AddKoteZone(Transform rightForearm, Vector3 a, Vector3 b)
        {
            fighter.zones.Add(new HitZone { target = Target.KoteRight, anchor = rightForearm, a = a, b = b, radius = 0.065f });
        }

        private void OnDestroy() { if (Active == this) Active = null; }

        private Vector3? AimFor(DuelAction a)
        {
            var p = aimPointWorld != null ? aimPointWorld(a) : null;
            var gaze = head.position + head.forward * 1.9f;
            if (p.HasValue)
            {
                // the cut follows the eyes: halfway between the named target and where the player looks at that depth
                float depth = Vector3.Dot(p.Value - head.position, head.forward);
                var g = head.position + head.forward * Mathf.Max(0.5f, depth);
                return transform.InverseTransformPoint(Vector3.Lerp(p.Value, g, 0.5f));
            }
            return transform.InverseTransformPoint(gaze);
        }

        // ------------------------------------------------------------------ update

        private void Update()
        {
            if (game == null) game = KendoGame.Instance;
            if (game == null) return;
            var f = game.Input.Read();
            var s = game.Settings;
            bool blocked = !InputEnabled || game.Ui.BlocksGameplay || game.Paused || fighter.Frozen;
            float dt = Time.deltaTime;
            cam.fieldOfView = s.fov;
            fighter.footwork.pace = s.footworkPace;

            if (f.tracked && game.Input.XRActive) UpdateXR(f, blocked, dt);
            else UpdateDesktop(f, s, blocked, dt);

            // footwork: the player steps through the CharacterController (collides with walls, dais, opponents)
            var local = fighter.footwork.Update(dt, Time.time);
            if (s.smoothFootwork && !blocked && MovementEnabled && !Bowing && f.move.sqrMagnitude > 0.01f && !fighter.footwork.Stepping)
            {
                var v = new Vector3(f.move.x, 0f, f.move.y).normalized * (float)(KendoTimings.StepForward / (KendoTimings.StepSeconds + KendoTimings.SettleSeconds)) * s.footworkPace;
                local += v * dt;
            }
            vy = controller.isGrounded ? -0.5f : vy - 9.81f * dt;
            var world = transform.TransformDirection(local) + Vector3.up * vy * dt;
            if (controller.enabled) controller.Move(world);

            UpdateBow(f, blocked, dt);
            UpdateInteraction(f, blocked);
            fighter.playerPitch = pitch;
        }

        private void UpdateDesktop(KendoInputFrame f, Kendo.Campaign.PlayerSettings s, bool blocked, float dt)
        {
            if (!blocked)
            {
                var look = f.look * 0.12f * s.mouseSensitivity;
                if (f.aimHeld && !Bowing)
                {
                    var m = fighter.Motion;
                    m.aim = new Vector2(Mathf.Clamp(m.aim.x + look.x * 0.5f, -15f, 15f), Mathf.Clamp(m.aim.y + look.y * (s.invertY ? -0.5f : 0.5f), -12f, 12f));
                }
                else
                {
                    yaw += look.x;
                    pitch = Mathf.Clamp(pitch - look.y * (s.invertY ? -1f : 1f), -60f, 60f);
                    var m = fighter.Motion;
                    m.aim = Vector2.MoveTowards(m.aim, Vector2.zero, 40f * dt);
                }
            }
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            float bowPitch = bowAngle * 0.9f;
            head.localRotation = Quaternion.Euler(pitch + bowPitch, 0f, 0f);
            // bowing moves the eyes forward and down (pivot at the hips)
            float r = Mathf.Deg2Rad * bowAngle;
            head.localPosition = new Vector3(0f, 0.95f + (EyeHeight - 0.95f) * Mathf.Cos(r), (EyeHeight - 0.95f) * Mathf.Sin(r));
            fighter.Motion.lookPitch = pitch;
            fighter.Motion.ceilingLocal = CeilingAbove();

            if (blocked) { fighter.BlockCommand(false, true); return; }
            if (MovementEnabled && !Bowing && f.move.sqrMagnitude > 0.01f && !s.smoothFootwork)
            {
                StepDir d = f.move.y > 0.5f ? StepDir.Forward : f.move.y < -0.5f ? StepDir.Back : f.move.x > 0.5f ? StepDir.Right : f.move.x < -0.5f ? StepDir.Left : StepDir.None;
                if (d != StepDir.None && fighter.StepCommand(d, stepLengthScale)) Issue(ToAction(d));
            }
            if (f.toggleKamae && !Bowing) fighter.ToggleKamae();
            if (f.kiai) fighter.KiaiCommand();
            bool wantBlock = f.blockHeld && !Bowing;
            // the block covers men/kote when the opponent strikes high, do when he cuts low
            bool high = !(fighter.opponent is Fighter op && op.Motion.Mode == ShinaiMode.Strike && op.Motion.Strike == DuelAction.Do);
            bool wasBlocking = fighter.Motion.Mode == ShinaiMode.Block;
            fighter.BlockCommand(wantBlock, high);
            if (!wasBlocking && fighter.Motion.Mode == ShinaiMode.Block) Issue(high ? DuelAction.BlockHigh : DuelAction.BlockDo);
            if (StrikesEnabled && f.strike != StrikeCommand.None && !Bowing)
            {
                var a = f.strike == StrikeCommand.Kote ? DuelAction.Kote : f.strike == StrikeCommand.Do ? DuelAction.Do : f.strike == StrikeCommand.Tsuki ? DuelAction.Tsuki : DuelAction.Men;
                if (fighter.StrikeCommand(a)) Issue(a);
            }
        }

        private static DuelAction ToAction(StepDir d) => d == StepDir.Forward ? DuelAction.StepForward : d == StepDir.Back ? DuelAction.StepBack : d == StepDir.Left ? DuelAction.StepLeft : DuelAction.StepRight;

        private void Issue(DuelAction a) => CommandIssued?.Invoke(a);

        private float CeilingAbove()
        {
            var origin = transform.position + Vector3.up * 1.0f;
            if (Physics.SphereCast(origin, 0.15f, Vector3.up, out var hit, 3.5f, fighter.rig.environment, QueryTriggerInteraction.Ignore))
                return hit.point.y - transform.position.y;
            return 99f;
        }

        public enum BowState { Upright, Lowering, Holding, Rising }
        public BowState Bow { get; private set; }
        private bool bowKeyWas;

        /// <summary>
        /// Rei on desktop: hold B to bend (depth meter), release B to stop and pause at that depth, press B again to rise.
        /// Mouse look stays active, so the player keeps (or loses) eye contact with the partner.
        /// </summary>
        private void UpdateBow(KendoInputFrame f, bool blocked, float dt)
        {
            bool key = f.bowHeld && !blocked && fighter.Motion.Mode != ShinaiMode.Strike;
            bool pressed = key && !bowKeyWas;
            bool released = !key && bowKeyWas;
            bowKeyWas = key;
            switch (Bow)
            {
                case BowState.Upright:
                    if (pressed) { Bow = BowState.Lowering; maxBowThisBow = 0f; bowHoldAtDepth = 0f; }
                    break;
                case BowState.Lowering:
                    bowAngle = Mathf.Min(45f, bowAngle + 24f * dt);
                    maxBowThisBow = bowAngle;
                    if (released || !f.bowHeld) Bow = BowState.Holding;
                    break;
                case BowState.Holding:
                    bowHoldAtDepth += dt;
                    if (pressed || bowHoldAtDepth > 6f)
                    {
                        Bow = BowState.Rising;
                        BowFinished?.Invoke(maxBowThisBow, bowHoldAtDepth);
                    }
                    break;
                case BowState.Rising:
                    bowAngle = Mathf.Max(0f, bowAngle - 40f * dt);
                    if (bowAngle <= 0f) Bow = BowState.Upright;
                    break;
            }
            fighter.bowDeg = bowAngle;
        }

        private void UpdateInteraction(KendoInputFrame f, bool blocked)
        {
            Focus = null;
            if (blocked) { game.Ui.SetPrompt(""); return; }
            float best = 0.5f;
            foreach (var it in Interactable.All)
            {
                if (it == null || !it.CanInteract) continue;
                var to = it.Position - head.position;
                var flat = to; flat.y = 0f;
                if (flat.magnitude > it.range) continue;
                float dot = Vector3.Dot(new Vector3(head.forward.x, 0f, head.forward.z).normalized, flat.normalized);
                if (dot > best) { best = dot; Focus = it; }
            }
            game.Ui.SetPrompt(Focus != null ? $"[F] {Focus.Prompt}" : "");
            if (Focus != null && f.interact) Focus.Interact();
        }

        private void UpdateXR(KendoInputFrame f, bool blocked, float dt)
        {
            // head: tracked pose relative to the rig, with the saved height calibration
            var s = game.Settings;
            if (f.headTracked)
            {
                head.localPosition = f.head.position + Vector3.up * s.vrHeightOffset;
                head.localRotation = f.head.rotation;
            }
            if (!blocked && Mathf.Abs(f.look.x) > 0.5f && Time.time - lastStepCommand > 0.35f) { yaw += 30f * Mathf.Sign(f.look.x); lastStepCommand = Time.time; }
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (!blocked && MovementEnabled && f.move.sqrMagnitude > 0.01f)
            {
                StepDir d = f.move.y > 0.5f ? StepDir.Forward : f.move.y < -0.5f ? StepDir.Back : f.move.x > 0.5f ? StepDir.Right : f.move.x < -0.5f ? StepDir.Left : StepDir.None;
                if (d != StepDir.None && fighter.StepCommand(d)) Issue(ToAction(d));
            }
            if (!blocked && f.kiai) fighter.KiaiCommand();
            if (!blocked && f.toggleKamae) fighter.ToggleKamae();
            // shinai from both controllers (two-handed grip); tracking loss freezes it
            var rig = fighter.rig;
            rig.externalPose = true;
            if (xrTracker.Solve(f, transform, s.vrArmScale, out var grip, out var rot)) rig.SetWorldPose(grip, rot);
        }
    }

    /// <summary>Builds a two-handed shinai pose from tracked controllers: left hand at the kashira, right hand behind the
    /// tsuba, blade along left→right hand. One-handed or lost tracking holds the last good pose for 0.3 s, then pauses.</summary>
    public sealed class XRShinaiTracker
    {
        public float lostSince = -1f;
        public bool TrackingLost => lostSince >= 0f && Time.unscaledTime - lostSince > 0.3f;
        private Vector3 lastGrip;
        private Quaternion lastRot = Quaternion.identity;

        public bool Solve(KendoInputFrame f, Transform rig, float armScale, out Vector3 grip, out Quaternion rot)
        {
            grip = lastGrip;
            rot = lastRot;
            if (!(f.leftTracked && f.rightTracked))
            {
                if (lostSince < 0f) lostSince = Time.unscaledTime;
                return !TrackingLost;
            }
            lostSince = -1f;
            var l = rig.TransformPoint(f.leftHand.position);
            var r = rig.TransformPoint(f.rightHand.position);
            var blade = r - l;
            if (blade.magnitude < 0.05f) blade = rig.rotation * (f.rightHand.rotation * Vector3.forward);
            blade.Normalize();
            var tsuruHint = rig.rotation * (f.rightHand.rotation * Vector3.up);
            var p = ShinaiMotion.Make(l - blade * (ShinaiRig.LeftGripAlong * armScale), blade, tsuruHint);
            lastGrip = grip = p.position;
            lastRot = rot = p.rotation;
            return true;
        }
    }
}
