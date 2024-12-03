using System.Threading.Tasks;

namespace Impostor.Api.Net.Inner.Objects.ShipStatus
{
    public interface IInnerShipStatus : IInnerNetObject
    {
        ValueTask Sabotage(Innersloth.SystemTypes systemType);
    }
}
