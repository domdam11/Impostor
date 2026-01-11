using Impostor.Api.Games;

namespace Impostor.Tools.SemanticServerReplay.Mocks
{
    public class MockGameCodeFactory : IGameCodeFactory
    {
        public GameCode Result { get; set; }

        public GameCode Create()
        {
            return Result;
        }
    }
}
