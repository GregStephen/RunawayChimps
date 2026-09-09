namespace RunawayChimps.Travel
{
    public static class SectorDestinations
    {
        public const string Hub = "Hub_Base";
        public const string LevelOne = "Level1_Containment";
        public const string LevelTwo = "Level2_BehavioralConditioning_Blockout";
        public const string LevelTwoPath = "Assets/RunawayChimps/Level2Blockout/Scenes/" + LevelTwo + ".unity";

        public static bool IsSupported(string sceneName) =>
            sceneName == Hub || sceneName == LevelOne || sceneName == LevelTwo;
    }
}
