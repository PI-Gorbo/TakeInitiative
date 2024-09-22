
using Alba;
using FluentAssertions;
namespace TakeInitiative.Api.Tests.Integration;

public interface IWebAppClient
{
    public IAlbaHost AlbaHost { get; }
}
