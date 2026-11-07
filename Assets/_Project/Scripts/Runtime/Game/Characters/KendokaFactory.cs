using Kendo.AI;
using UnityEngine;

namespace Kendo.Game
{
    public enum KendokaRole { Master, MasterMotodachi, Opponent, OpponentWhite, FormerStudent }

    /// <summary>
    /// Builds every character from the one supplied model (KendoMaster_And_Shinai.fbx): the master as he is; opponents
    /// and the former student adapted with the supplied men helmet, a generated do, and different gi colours.
    /// No other character model exists in the project, so this is the documented adaptation.
    /// </summary>
    public static class KendokaFactory
    {
        public static Fighter Create(KendokaRole role, Vector3 position, Quaternion rotation, KendoGameConfig cfg, string name)
        {
            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(position, Quaternion.identity); // build un-rotated, turn at the end
            root.layer = Layers.Characters;
            if (cfg == null || cfg.characterModel == null)
            {
                Debug.LogError("[Kendo] Character model missing – run Kendo ▸ Build Game.");
                return null;
            }

            // model: feet at the origin, facing +Z (facing detected from the face mask, which sits in front of the face)
            var modelGo = Object.Instantiate(cfg.characterModel, root.transform, false);
            modelGo.name = "Model";
            var model = modelGo.transform;
            model.localPosition = Vector3.zero;
            model.localRotation = Quaternion.identity;
            Transform embeddedShinai = FindDeep(model, "Shinai");
            if (embeddedShinai != null) { embeddedShinai.gameObject.AddComponent<ShinaiTag>(); embeddedShinai.gameObject.SetActive(false); }
            var bounds = BodyBounds(model);
            var mask = FindDeep(model, "Masc_low");
            bool backwards = mask != null && Bounds(mask).center.z < bounds.center.z;
            if (backwards) model.localRotation = Quaternion.Euler(0f, 180f, 0f);
            bounds = BodyBounds(model);
            model.position += new Vector3(position.x - bounds.center.x, position.y - bounds.min.y, position.z - bounds.center.z);
            float h = bounds.size.y;

            var body = root.AddComponent<KendokaBody>();
            body.Build(model, h);
            var poser = root.AddComponent<KendokaPoser>();
            poser.body = body;

            var fighter = root.AddComponent<Fighter>();
            fighter.displayName = name;
            fighter.aiControlled = true;
            fighter.body = body;
            fighter.poser = poser;
            fighter.headPoint = body[BoneId.Head];
            fighter.side = 1;
            var rig = root.AddComponent<ShinaiRig>();
            var shinai = cfg.shinaiPrefab != null ? Object.Instantiate(cfg.shinaiPrefab, root.transform, false).transform : null;
            if (shinai != null) { shinai.name = "Shinai"; SetLayer(shinai.gameObject, Layers.Shinai); }
            rig.Init(shinai, root.transform, 1.2f, Layers.EnvironmentMask);
            fighter.Setup(rig, Mathf.Min(h, 1.80f));
            fighter.zones.AddRange(HitGeometry.ForKendoka(body));

            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.85f, 0f);
            col.height = 1.7f;
            col.radius = 0.24f;
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            switch (role)
            {
                case KendokaRole.Master:
                    break;
                case KendokaRole.MasterMotodachi:
                    Armour(body, cfg, null, null, new Color(0.55f, 0.08f, 0.06f));
                    break;
                case KendokaRole.Opponent:
                    Armour(body, cfg, new Color(0.10f, 0.13f, 0.30f), new Color(0.08f, 0.10f, 0.22f), new Color(0.12f, 0.12f, 0.14f));
                    break;
                case KendokaRole.OpponentWhite:
                    Armour(body, cfg, new Color(0.88f, 0.88f, 0.84f), new Color(0.82f, 0.82f, 0.80f), new Color(0.35f, 0.08f, 0.08f));
                    break;
                case KendokaRole.FormerStudent:
                    Armour(body, cfg, new Color(0.06f, 0.06f, 0.07f), new Color(0.04f, 0.04f, 0.05f), new Color(0.50f, 0.04f, 0.05f));
                    fighter.technique = Technique.Boss();
                    break;
            }
            root.transform.rotation = Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
            return fighter;
        }

        /// <summary>Opponent kit: hide the master's hat, mask, eyes, cape and flask; recolour gi and hakama; add men and do.</summary>
        private static void Armour(KendokaBody body, KendoGameConfig cfg, Color? gi, Color? hakama, Color doColor)
        {
            body.SetPartVisible("Sombrero", false);
            body.SetPartVisible("Masc", false);
            body.SetPartVisible("Ojos", false);
            body.SetPartVisible("Capa", false);
            body.SetPartVisible("Frasco", false);
            if (gi.HasValue) Tint(body, "Parte_Arriba", gi.Value);
            if (hakama.HasValue) Tint(body, "Lompa", hakama.Value);
            if (cfg.menHelmet != null)
            {
                var men = Object.Instantiate(cfg.menHelmet, body[BoneId.Head], false);
                men.name = "Men";
                var mb = BodyBoundsAll(men.transform);
                float s = cfg.menScale;
                men.transform.localScale *= s;
                mb = BodyBoundsAll(men.transform);
                // sit the helmet so its crown is just above the head top
                var top = body.headTop.position + Vector3.up * 0.04f * body.scale;
                men.transform.position += top - new Vector3(mb.center.x, mb.max.y, mb.center.z) + Vector3.down * cfg.menFitDrop * 0.2f;
                foreach (var c in men.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            }
            var doGo = ProceduralMeshes.Spawn("Do", ProceduralMeshes.Do(), ProceduralMeshes.Lit("Do_Lacquer", doColor, 0.85f, 0.1f), body[BoneId.Chest], true);
            doGo.transform.localPosition = new Vector3(0f, -0.30f * body.scale, 0.0f);
            doGo.transform.localScale = Vector3.one * body.scale;
        }

        private static void Tint(KendokaBody body, string part, Color c)
        {
            foreach (var r in body.PartRenderers(part))
            {
                var mats = r.materials; // instance copies – never edits the FBX's materials
                foreach (var m in mats)
                {
                    if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
                    if (m.HasProperty("_Color")) m.SetColor("_Color", c);
                    if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", null);
                }
                r.materials = mats;
            }
        }

        public static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var r = FindDeep(c, name);
                if (r != null) return r;
            }
            return null;
        }

        private static Bounds Bounds(Transform t)
        {
            var r = t.GetComponent<Renderer>();
            return r != null ? r.bounds : new Bounds(t.position, Vector3.zero);
        }

        public static Bounds BodyBounds(Transform model)
        {
            var person = FindDeep(model, "Personaje_low") ?? model;
            return BodyBoundsAll(person);
        }

        public static Bounds BodyBoundsAll(Transform t)
        {
            bool any = false;
            var b = new Bounds(t.position, Vector3.zero);
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (r.GetComponentInParent<ShinaiTag>() != null) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        public static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayer(t.gameObject, layer);
        }
    }
}
