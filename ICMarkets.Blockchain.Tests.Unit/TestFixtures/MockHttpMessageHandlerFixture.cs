using System.Net;
using Moq;
using Moq.Protected;

namespace ICMarkets.Blockchain.Tests.Unit.TestFixtures;

/// <summary>
/// Reusable fixture for creating mock HTTP message handlers.
/// </summary>
public class MockHttpMessageHandlerFixture
{
    public Mock<HttpMessageHandler> CreateHandler(
        HttpStatusCode statusCode,
        string content = "{}")
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });
        return handlerMock;
    }

    public Mock<HttpMessageHandler> CreateHandlerWithFunc(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFunc)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(responseFunc);
        return handlerMock;
    }

    public Mock<HttpMessageHandler> CreateHandlerWithSequence(
        params (HttpStatusCode StatusCode, string Content)[] responses)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        var callCount = 0;

        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var response = responses[Math.Min(callCount, responses.Length - 1)];
                callCount++;
                return new HttpResponseMessage
                {
                    StatusCode = response.StatusCode,
                    Content = new StringContent(response.Content)
                };
            });

        return handlerMock;
    }
}
