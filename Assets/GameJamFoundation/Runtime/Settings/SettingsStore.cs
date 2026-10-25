using System;
using UnityEngine;

namespace FreedomJam.Foundation
{
    public sealed class SettingsStore
    {
        readonly string prefix;
        public SettingsStore(string prefix = "FreedomJam.") { this.prefix = prefix; }

        public GameSettings LoadSettings(GameSettings defaults)
        {
            var result = defaults.Copy();
            var json = PlayerPrefs.GetString(prefix + "settings", "").Trim();
            if (json.StartsWith("{") && json.EndsWith("}"))
            {
                try { JsonUtility.FromJsonOverwrite(json, result); }
                catch (ArgumentException) { result = defaults.Copy(); }
            }
            result.Sanitize(defaults);
            return result;
        }
        public void SaveSettings(GameSettings settings)
        {
            var safe = settings.Copy(); safe.Sanitize(new GameSettings());
            PlayerPrefs.SetString(prefix + "settings", JsonUtility.ToJson(safe));
            PlayerPrefs.Save();
        }
        public int LoadBestScore(string key, int fallback, ScoreDirection direction) => PlayerPrefs.GetInt(prefix + "best." + key, fallback);
        public bool TryRecordBestScore(string key, int candidate, int fallback, ScoreDirection direction)
        {
            var prefKey = prefix + "best." + key;
            var current = PlayerPrefs.GetInt(prefKey, fallback);
            if (PlayerPrefs.HasKey(prefKey) && (direction == ScoreDirection.HigherIsBetter ? candidate <= current : candidate >= current)) return false;
            PlayerPrefs.SetInt(prefKey, candidate); PlayerPrefs.Save(); return true;
        }
    }
}
