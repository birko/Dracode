namespace DraCode.KoboldLair.Server.Models.Agents
{
    /// <summary>
    /// Statistics about Dragon service
    /// </summary>
    public class DragonStatistics
    {
        public int ActiveSessions { get; set; }
        public int TotalSpecifications { get; set; }

        public override string ToString()
        {
            return $"Dragon Stats: {ActiveSessions} active sessions, {TotalSpecifications} specifications created";
        }
    }
}
