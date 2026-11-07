using System;
using System.IO;
using Kendo.AI;
using Kendo.Campaign;
using UnityEngine;

namespace Kendo.Game
{
    public enum SaveLoadStatus { NewGame, Loaded, RecoveredFromBackup, CorruptReset, FutureVersionReset, StorageUnavailable }

    /// <summary>Where save bytes live. The file store is used in the game; the memory store in tests.</summary>
    public interface ISaveStore
    {
        bool Exists(string name);
        string Read(string name);
        void Write(string name, string text);       // must be atomic (temp file + replace)
        void Copy(string from, string to);
        void Quarantine(string name, string suffix);
        byte[] ReadBytes(string name);
        void WriteBytes(string name, byte[] data);
        bool Available { get; }
    }

    public sealed class FileSaveStore : ISaveStore
    {
        private readonly string folder;
        public bool Available { get; private set; }

        public FileSaveStore(string folder)
        {
            this.folder = folder;
            try { Directory.CreateDirectory(folder); Available = true; }
            catch (Exception e) { Debug.LogWarning($"[Kendo] Save folder unavailable ({e.Message}); progress will only be kept in memory."); Available = false; }
        }

        private string P(string n) => Path.Combine(folder, n);
        public bool Exists(string name) => Available && File.Exists(P(name));
        public string Read(string name) => File.ReadAllText(P(name));
        public byte[] ReadBytes(string name) => File.ReadAllBytes(P(name));

        public void Write(string name, string text)
        {
            string tmp = P(name + ".tmp");
            File.WriteAllText(tmp, text);
            if (File.Exists(P(name))) File.Delete(P(name));
            File.Move(tmp, P(name));
        }

        public void WriteBytes(string name, byte[] data)
        {
            string tmp = P(name + ".tmp");
            File.WriteAllBytes(tmp, data);
            if (File.Exists(P(name))) File.Delete(P(name));
            File.Move(tmp, P(name));
        }

        public void Copy(string from, string to) { if (File.Exists(P(from))) File.Copy(P(from), P(to), true); }

        public void Quarantine(string name, string suffix)
        {
            if (!File.Exists(P(name))) return;
            string dest = P(name + ".corrupt-" + suffix);
            if (File.Exists(dest)) File.Delete(dest);
            File.Move(P(name), dest);
        }
    }

    public sealed class MemorySaveStore : ISaveStore
    {
        public readonly System.Collections.Generic.Dictionary<string, string> files = new System.Collections.Generic.Dictionary<string, string>();
        public readonly System.Collections.Generic.Dictionary<string, byte[]> blobs = new System.Collections.Generic.Dictionary<string, byte[]>();
        public bool failWrites;
        public bool Available => true;
        public bool Exists(string name) => files.ContainsKey(name) || blobs.ContainsKey(name);
        public string Read(string name) => files[name];
        public void Write(string name, string text) { if (failWrites) throw new IOException("disk full (simulated)"); files[name] = text; }
        public void Copy(string from, string to) { if (files.TryGetValue(from, out var t)) files[to] = t; }
        public void Quarantine(string name, string suffix) { if (files.TryGetValue(name, out var t)) { files.Remove(name); files[name + ".corrupt-" + suffix] = t; } }
        public byte[] ReadBytes(string name) => blobs[name];
        public void WriteBytes(string name, byte[] data) { if (failWrites) throw new IOException("disk full (simulated)"); blobs[name] = data; }
    }

    /// <summary>
    /// Persistent saving with a backup and recovery: main → backup → quarantine + new game. Loading never throws.
    /// Also stores the player's behaviour log (imitation-learning data) and the personalised RL policy.
    /// </summary>
    public sealed class SaveService
    {
        public const string MainFile = "kendo_save.json";
        public const string BackupFile = "kendo_save.bak.json";
        public const string BehaviourFile = "kendo_player_behaviour.bin";
        public const string PolicyFile = "kendo_personal_policy.json";

        private readonly ISaveStore store;
        public KendoSaveData Data { get; private set; }
        public SaveLoadStatus Status { get; private set; }
        public string LastError { get; private set; }
        public bool HasSave => store.Exists(MainFile) || store.Exists(BackupFile);

        public SaveService(ISaveStore store) { this.store = store; }

        public static SaveService CreateDefault() => new SaveService(new FileSaveStore(Path.Combine(Application.persistentDataPath, "Saves")));

        private static string Now() => DateTime.UtcNow.ToString("o");

        public static bool TryParse(string json, out KendoSaveData data, out string error)
        {
            data = null;
            error = null;
            try
            {
                if (string.IsNullOrWhiteSpace(json)) { error = "empty file"; return false; }
                data = JsonUtility.FromJson<KendoSaveData>(json);
                if (data == null) { error = "not a save file"; return false; }
                if (data.version <= 0) { error = "missing version"; return false; }
                data.Normalize();
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                data = null;
                return false;
            }
        }

        public SaveLoadStatus Load()
        {
            if (!store.Available) { Data = KendoSaveData.NewGame(Now()); return Status = SaveLoadStatus.StorageUnavailable; }
            try
            {
                if (!store.Exists(MainFile) && !store.Exists(BackupFile)) { Data = KendoSaveData.NewGame(Now()); return Status = SaveLoadStatus.NewGame; }
                if (store.Exists(MainFile))
                {
                    if (TryParse(store.Read(MainFile), out var d, out var err))
                    {
                        if (d.version > KendoSaveData.CurrentVersion)
                        {
                            store.Quarantine(MainFile, "future-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
                            Data = KendoSaveData.NewGame(Now());
                            return Status = SaveLoadStatus.FutureVersionReset;
                        }
                        Data = d;
                        return Status = SaveLoadStatus.Loaded;
                    }
                    LastError = err;
                }
                if (store.Exists(BackupFile) && TryParse(store.Read(BackupFile), out var b, out _) && b.version <= KendoSaveData.CurrentVersion)
                {
                    if (store.Exists(MainFile)) store.Quarantine(MainFile, DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
                    Data = b;
                    Save();
                    return Status = SaveLoadStatus.RecoveredFromBackup;
                }
                if (store.Exists(MainFile)) store.Quarantine(MainFile, DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
                Data = KendoSaveData.NewGame(Now());
                return Status = SaveLoadStatus.CorruptReset;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Data = KendoSaveData.NewGame(Now());
                return Status = SaveLoadStatus.CorruptReset;
            }
        }

        public void NewGame()
        {
            var keepSettings = Data != null ? Data.settings : new PlayerSettings();
            Data = KendoSaveData.NewGame(Now());
            Data.settings = keepSettings;
            try { if (store.Exists(BehaviourFile)) store.WriteBytes(BehaviourFile, new byte[0]); } catch (Exception) { }
            Save();
        }

        /// <summary>Writes the save (previous good file becomes the backup). Returns false on I/O failure (play continues).</summary>
        public bool Save()
        {
            if (Data == null) return false;
            if (!store.Available) return false;
            try
            {
                Data.saveCount++;
                Data.savedUtc = Now();
                string json = JsonUtility.ToJson(Data, true);
                if (store.Exists(MainFile) && TryParse(store.Read(MainFile), out _, out _)) store.Copy(MainFile, BackupFile);
                store.Write(MainFile, json);
                LastError = null;
                return true;
            }
            catch (Exception e)
            {
                LastError = e.Message;
                Debug.LogWarning($"[Kendo] Save failed: {e.Message}");
                return false;
            }
        }

        public PlayerBehaviourLog LoadBehaviour()
        {
            try
            {
                if (!store.Exists(BehaviourFile)) return new PlayerBehaviourLog();
                var bytes = store.ReadBytes(BehaviourFile);
                if (bytes == null || bytes.Length < 8) return new PlayerBehaviourLog();
                using (var r = new BinaryReader(new MemoryStream(bytes))) return PlayerBehaviourLog.Read(r);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Kendo] Behaviour log unreadable ({e.Message}); starting a new one.");
                return new PlayerBehaviourLog();
            }
        }

        public void SaveBehaviour(PlayerBehaviourLog log)
        {
            try
            {
                using (var ms = new MemoryStream())
                {
                    using (var w = new BinaryWriter(ms)) log.Write(w);
                    store.WriteBytes(BehaviourFile, ms.ToArray());
                }
            }
            catch (Exception e) { Debug.LogWarning($"[Kendo] Could not save behaviour log: {e.Message}"); }
        }

        public string LoadPersonalPolicy()
        {
            try { return store.Exists(PolicyFile) ? store.Read(PolicyFile) : null; }
            catch (Exception) { return null; }
        }

        public void SavePersonalPolicy(string json)
        {
            try { store.Write(PolicyFile, json); } catch (Exception e) { Debug.LogWarning($"[Kendo] Could not save policy: {e.Message}"); }
        }
    }
}
