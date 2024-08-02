using System;
using Impostor.Api.Utils;

namespace Impostor.Tools.SemanticServerReplay
{
    public class FakeDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; set; }
    }
}
