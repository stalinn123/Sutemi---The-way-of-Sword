using System;

namespace Kendo.Core
{
    /// <summary>Every scene of the game, in build order.</summary>
    public enum KendoSceneId
    {
        Boot = 0,
        MainMenu = 1,
        TrainingDojo = 2,
        Level1 = 3,
        Level2 = 4,
        Level3 = 5,
        Level4 = 6,
        FinalBattle = 7,
        Ending = 8,
    }

    public static class KendoScenes
    {
        public const string Folder = "Assets/_Project/Scenes";

        public static string FileName(KendoSceneId id)
        {
            switch (id)
            {
                case KendoSceneId.Boot: return "00_Boot";
                case KendoSceneId.MainMenu: return "01_MainMenu";
                case KendoSceneId.TrainingDojo: return "02_TrainingDojo";
                case KendoSceneId.Level1: return "03_Level1";
                case KendoSceneId.Level2: return "04_Level2";
                case KendoSceneId.Level3: return "05_Level3";
                case KendoSceneId.Level4: return "06_Level4";
                case KendoSceneId.FinalBattle: return "07_FinalBattle";
                case KendoSceneId.Ending: return "08_Ending";
                default: throw new ArgumentOutOfRangeException(nameof(id), id, null);
            }
        }

        public static bool TryParse(string name, out KendoSceneId id)
        {
            foreach (KendoSceneId s in Enum.GetValues(typeof(KendoSceneId)))
            {
                if (string.Equals(FileName(s), name, StringComparison.OrdinalIgnoreCase)) { id = s; return true; }
            }
            id = KendoSceneId.Boot;
            return false;
        }

        public static string AssetPath(KendoSceneId id) => $"{Folder}/{FileName(id)}.unity";

        public static KendoSceneId[] BuildOrder => (KendoSceneId[])Enum.GetValues(typeof(KendoSceneId));
    }
}
