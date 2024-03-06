namespace Impostor.Api.Config
{
    public class AntiCheatConfig
    {
        public const string Section = "AntiCheat";

        public bool Enabled { get; set; } = false;

        public bool BanIpFromGame { get; set; } = false;
    }
}
