using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ANL.Core.Contracts;
using ANL.Core.State;
using ANL.Runtime.Diagnostics;
using UnityEngine;

namespace ANL.Runtime.Persistence
{
    [Serializable]
    public sealed class SettingsData
    {
        public float textScale = 1.0f;
        public bool instantText = false;
        public bool reduceMotion = false;
        public bool reduceFlash = false;
        public bool audioEnabled = true;
        public bool autoAdvance = false;
        public bool testToolsEnabled = false;
    }

    public enum LoadOutcome
    {
        Ok,
        Missing,
        Corrupt,
        Incompatible,
        UnknownIds
    }

    public sealed class SaveService
    {
        private const int CurrentSchemaVersion = 1;

        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        private readonly string _contentVersion;
        private readonly string _artManifestSha256;

        public string SaveRoot { get; }

        public SaveService(string contentVersion, string artManifestSha256)
        {
            _contentVersion = contentVersion ?? "";
            _artManifestSha256 = artManifestSha256 ?? "";
            SaveRoot = Path.Combine(Application.persistentDataPath, "saves");
        }

        public string ResolvedPathFor(string slotId)
        {
            return Path.Combine(SaveRoot, (slotId ?? "") + ".json");
        }

        public bool Save(GameState state, string slotId)
        {
            if (state == null)
            {
                Diag.Error("SaveService.Save: state is null");
                return false;
            }
            if (string.IsNullOrEmpty(slotId))
            {
                Diag.Error("SaveService.Save: slot id is empty");
                return false;
            }

            try
            {
                var snapshot = state.ToSnapshot(_contentVersion, _artManifestSha256, slotId);
                string payloadJson = JsonUtility.ToJson(snapshot, false);
                var envelope = new SaveEnvelopeDto
                {
                    schemaVersion = CurrentSchemaVersion,
                    contentVersion = _contentVersion,
                    payloadJson = payloadJson,
                    payloadSha256 = Sha256Hex(payloadJson),
                    savedAtUtc = DateTime.UtcNow.ToString("o"),
                    slotId = slotId
                };
                string envelopeJson = JsonUtility.ToJson(envelope, false);

                Directory.CreateDirectory(SaveRoot);
                string finalPath = ResolvedPathFor(slotId);
                string tmpPath = finalPath + ".tmp";
                string bakPath = finalPath + ".bak";

                using (var stream = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, Utf8NoBom))
                {
                    writer.Write(envelopeJson);
                    writer.Flush();
                    stream.Flush();
                }

                try
                {
                    if (File.Exists(finalPath))
                    {
                        try
                        {
                            File.Copy(finalPath, bakPath, true);
                        }
                        catch (Exception backupEx)
                        {
                            Diag.Warn("SaveService: backup failed for " + slotId + ": " + backupEx.Message);
                        }
                        File.Delete(finalPath);
                    }
                    File.Move(tmpPath, finalPath);
                }
                catch (Exception replaceEx)
                {
                    Diag.Error("SaveService: replace failed for " + slotId + ": " + replaceEx.Message);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Diag.Error("SaveService.Save failed for " + slotId + ": " + ex.Message);
                return false;
            }
        }

        public LoadOutcome Load(string slotId, out ProgressSnapshot snapshot)
        {
            snapshot = null;
            if (string.IsNullOrEmpty(slotId)) return LoadOutcome.Missing;

            string finalPath = ResolvedPathFor(slotId);
            if (!File.Exists(finalPath)) return LoadOutcome.Missing;

            var outcome = TryLoadFile(finalPath, out snapshot);
            if (outcome == LoadOutcome.Ok || outcome == LoadOutcome.Incompatible) return outcome;

            string bakPath = finalPath + ".bak";
            if (File.Exists(bakPath))
            {
                var bakOutcome = TryLoadFile(bakPath, out var bakSnapshot);
                if (bakOutcome == LoadOutcome.Ok)
                {
                    snapshot = bakSnapshot;
                    Diag.Warn("SaveService: recovered slot " + slotId + " from backup");
                    return LoadOutcome.Ok;
                }
            }

            snapshot = null;
            return LoadOutcome.Corrupt;
        }

        private LoadOutcome TryLoadFile(string path, out ProgressSnapshot snapshot)
        {
            snapshot = null;
            try
            {
                string envelopeJson = File.ReadAllText(path, Utf8NoBom);
                var envelope = JsonUtility.FromJson<SaveEnvelopeDto>(envelopeJson);
                if (envelope == null || string.IsNullOrEmpty(envelope.payloadJson)) return LoadOutcome.Corrupt;
                if (envelope.schemaVersion > CurrentSchemaVersion) return LoadOutcome.Incompatible;
                if (!string.Equals(envelope.contentVersion ?? "", _contentVersion, StringComparison.Ordinal))
                    return LoadOutcome.Incompatible;
                if (!string.Equals(Sha256Hex(envelope.payloadJson), envelope.payloadSha256 ?? "", StringComparison.OrdinalIgnoreCase))
                    return LoadOutcome.Corrupt;

                var parsed = JsonUtility.FromJson<ProgressSnapshot>(envelope.payloadJson);
                if (parsed == null) return LoadOutcome.Corrupt;
                snapshot = parsed;
                return LoadOutcome.Ok;
            }
            catch (Exception ex)
            {
                Diag.Warn("SaveService: read failed for " + path + ": " + ex.Message);
                return LoadOutcome.Corrupt;
            }
        }

        public bool Exists(string slotId)
        {
            if (string.IsNullOrEmpty(slotId)) return false;
            try
            {
                return File.Exists(ResolvedPathFor(slotId));
            }
            catch (Exception ex)
            {
                Diag.Warn("SaveService.Exists failed for " + slotId + ": " + ex.Message);
                return false;
            }
        }

        public string[] ListSlots()
        {
            return new[] { "slot_01", "slot_02", "slot_03", "autosave" };
        }

        public string ReadSlotTimestamp(string slotId)
        {
            if (string.IsNullOrEmpty(slotId)) return "";
            try
            {
                string path = ResolvedPathFor(slotId);
                if (!File.Exists(path)) return "";
                var envelope = JsonUtility.FromJson<SaveEnvelopeDto>(File.ReadAllText(path, Utf8NoBom));
                return envelope != null && !string.IsNullOrEmpty(envelope.savedAtUtc) ? envelope.savedAtUtc : "";
            }
            catch (Exception ex)
            {
                Diag.Warn("SaveService.ReadSlotTimestamp failed for " + slotId + ": " + ex.Message);
                return "";
            }
        }

        public SettingsData LoadSettings()
        {
            string path = Path.Combine(Application.persistentDataPath, "settings.json");
            try
            {
                if (!File.Exists(path)) return new SettingsData();
                var settings = JsonUtility.FromJson<SettingsData>(File.ReadAllText(path, Utf8NoBom));
                return settings ?? new SettingsData();
            }
            catch (Exception ex)
            {
                Diag.Warn("SaveService: settings load failed: " + ex.Message);
                return new SettingsData();
            }
        }

        public void SaveSettings(SettingsData settings)
        {
            if (settings == null) return;
            string path = Path.Combine(Application.persistentDataPath, "settings.json");
            try
            {
                File.WriteAllText(path, JsonUtility.ToJson(settings, false), Utf8NoBom);
            }
            catch (Exception ex)
            {
                Diag.Error("SaveService: settings save failed: " + ex.Message);
            }
        }

        public static string Sha256Hex(string text)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? ""));
                var builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }
    }
}
