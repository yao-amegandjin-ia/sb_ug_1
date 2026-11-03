namespace GameLogic.Upgrades
{
    /// <summary>
    /// What an upgrade changes in the game. Each UpgradeDefinition picks one.
    /// UpgradeController.Total(effect) adds up every owned level of every upgrade with that effect,
    /// so two upgrades can share an effect (e.g. a Clock and a later "Wall Calendar" both giving day time).
    ///
    /// To add a brand new kind of effect:
    ///   1. add a value here (add to the END, Unity saves enums as numbers in the upgrade assets)
    ///   2. read UpgradeController.Total(UpgradeEffect.YourEffect) in the script that should change
    /// </summary>

    public enum UpgradeEffect
    {
        DayLengthUpgrade = 0,   // Clock: seconds added to the day timer (HUD), needs to be changed when timer is added
        DailyClientsUpgrade = 1,       // Advertisements: extra new clients each day (ClientGenerator)
        WaitingRoomSeatsUpgrade = 2,   // Couch: extra clients allowed in the waiting area (FirmController)
        PatienceUpgrade = 3     // Magazines: seconds added to every new client's patience (ClientGenerator)
    }
}