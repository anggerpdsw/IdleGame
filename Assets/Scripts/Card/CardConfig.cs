namespace IdleDefenseSurvival.Card
{
    /// <summary>
    /// Card balance constants loaded from dataCardConfig.json.
    /// </summary>
    [System.Serializable]
    public class CardConfig
    {
        public float DeathChainWindow = 2f;
        public int DeathChainMaxStack = 15;
        public int BulletStormAdditional = 5;
        public float BulletStormMult = 0.9f;
        public float ExecutionProtocolNormal = 0.15f;
        public float ExecutionProtocolBoss = 0.08f;
        public int OverkillCap = 2;
        public float VampiricFrenzyDuration = 5f;
        public int VampiricFrenzyMaxStacks = 10;
        public float GuardianHPDrop = 0.3f;
        public float GuardianCooldownDuration = 30f;
        public float WarMachineContinuityThreshold = 5f;
        public float WarMachineIdleThreshold = 3f;
        public float InfiniteArsenalMult = 0.9f;
        public int GamblerMaxCount = 20;
        public float GamblerPositiveValue = 25f;
        public float GamblerNegativeValue = -15f;
    }

}
