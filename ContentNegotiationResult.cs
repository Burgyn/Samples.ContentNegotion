using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using System.Net.Mime;
using System.Reflection;
using System.Runtime.Serialization;

namespace Samples.ContentNegotion;

public class ContentNegotiationResult<TResult>(TResult result)
    : IResult, IEndpointMetadataProvider, IStatusCodeHttpResult, IValueHttpResult
{
    private readonly TResult _result = result;

    object? IValueHttpResult.Value => _result;

    public int StatusCode { get; set; } = StatusCodes.Status200OK;

    int? IStatusCodeHttpResult.StatusCode => StatusCode;

    public Task ExecuteAsync(HttpContext httpContext)
    {
        if (_result == null)
        {
            httpContext.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        }

        var negotiator = GetNegotiator(httpContext);
        if (negotiator == null)
        {
            httpContext.Response.StatusCode = StatusCodes.Status406NotAcceptable;
            return Task.CompletedTask;
        }

        httpContext.Response.StatusCode = StatusCode;
        return negotiator.Handle(httpContext, _result, httpContext.RequestAborted);
    }

    private static IResponseNegotiator? GetNegotiator(HttpContext httpContext)
    {
        var accept = httpContext.Request.GetTypedHeaders().Accept;
        return ContentNegotiationProvider.Negotiators.FirstOrDefault(n =>
        {
            return accept.Any(a => n.CanHandle(a));
        });
    }

    static void IEndpointMetadataProvider.PopulateMetadata(MethodInfo method, EndpointBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(builder);

        builder.Metadata.Add(new ProducesResponseTypeMetadata(StatusCodes.Status200OK, typeof(TResult),
            ContentNegotiationProvider.Negotiators.Select(n => n.ContentType).ToArray()));
    }
}

public static class ContentNegotiationProvider
{
    private static readonly List<IResponseNegotiator> _negotiators = [];

    internal static IReadOnlyList<IResponseNegotiator> Negotiators => _negotiators;

    public static void AddNegotiator<TNegotiator>()
        where TNegotiator : IResponseNegotiator, new()
    {
        _negotiators.Add(new TNegotiator());
    }
}

public static class Negotiation
{
    public static ContentNegotiationResult<T> Negotiate<T>(T result)
        => new(result);
}

public interface IResponseNegotiator
{
    string ContentType { get; }

    bool CanHandle(MediaTypeHeaderValue accept);

    Task Handle<TResult>(HttpContext httpContext, TResult result, CancellationToken cancellationToken);
}

public class XmlNegotiator : IResponseNegotiator
{
    public string ContentType => MediaTypeNames.Application.Xml;
    public bool CanHandle(MediaTypeHeaderValue accept)
        => accept.MediaType == ContentType;

    public async Task Handle<TResult>(HttpContext httpContext, TResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);

        httpContext.Response.ContentType = ContentType;

        using var stream = new FileBufferingWriteStream();
        using var streamWriter = new StreamWriter(stream);
        var serializer = new DataContractSerializer(result.GetType());

        serializer.WriteObject(stream, result);

        await stream.DrainBufferAsync(httpContext.Response.Body, cancellationToken);
    }
}

public class JsonNegotiator : IResponseNegotiator
{
    public string ContentType => MediaTypeNames.Application.Json;

    public bool CanHandle(MediaTypeHeaderValue accept)
        => accept.MediaType == ContentType;

    public async Task Handle<TResult>(HttpContext httpContext, TResult result, CancellationToken cancellationToken)
    {
        await TypedResults.Ok(result).ExecuteAsync(httpContext);
    }
}