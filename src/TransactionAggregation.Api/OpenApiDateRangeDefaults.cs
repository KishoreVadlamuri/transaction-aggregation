using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace TransactionAggregation.Api;

/// <summary>
/// Prefills Scalar / OpenAPI <c>from</c> and <c>to</c> date fields with relative defaults
/// (yesterday → one week from today, UTC) so Try-it requests are ready to send.
/// </summary>
public static class OpenApiDateRangeDefaults
{
    public static Func<OpenApiDocument, OpenApiDocumentTransformerContext, CancellationToken, Task> CreateTransformer()
    {
        return (document, _, _) =>
        {
            var (fromValue, toValue) = ComputeDefaults(DateTimeOffset.UtcNow);
            var fromNode = JsonValue.Create(fromValue)!;
            var toNode = JsonValue.Create(toValue)!;

            if (document.Paths is not null)
            {
                foreach (var pathItem in document.Paths.Values)
                {
                    if (pathItem.Operations is null)
                    {
                        continue;
                    }

                    foreach (var operation in pathItem.Operations.Values)
                    {
                        ApplyToParameters(operation.Parameters, fromNode, toNode);
                        ApplyToRequestBody(operation.RequestBody, fromNode, toNode);
                    }
                }
            }

            return Task.CompletedTask;
        };
    }

    public static (string From, string To) ComputeDefaults(DateTimeOffset utcNow)
    {
        var today = DateTime.SpecifyKind(utcNow.UtcDateTime.Date, DateTimeKind.Utc);
        var from = new DateTimeOffset(today.AddDays(-1), TimeSpan.Zero);
        var to = new DateTimeOffset(today.AddDays(7), TimeSpan.Zero);
        return (Format(from), Format(to));
    }

    private static string Format(DateTimeOffset value) =>
        value.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

    private static void ApplyToParameters(
        IList<IOpenApiParameter>? parameters,
        JsonNode fromNode,
        JsonNode toNode)
    {
        if (parameters is null)
        {
            return;
        }

        foreach (var parameter in parameters)
        {
            if (parameter is not OpenApiParameter concrete)
            {
                continue;
            }

            if (IsFromName(concrete.Name))
            {
                SetParameterDefault(concrete, fromNode);
            }
            else if (IsToName(concrete.Name))
            {
                SetParameterDefault(concrete, toNode);
            }
        }
    }

    private static void ApplyToRequestBody(
        IOpenApiRequestBody? requestBody,
        JsonNode fromNode,
        JsonNode toNode)
    {
        if (requestBody?.Content is null)
        {
            return;
        }

        foreach (var mediaType in requestBody.Content.Values)
        {
            ApplyToSchema(mediaType.Schema, fromNode, toNode, visited: []);
        }
    }

    private static void ApplyToSchema(
        IOpenApiSchema? schema,
        JsonNode fromNode,
        JsonNode toNode,
        HashSet<IOpenApiSchema> visited)
    {
        if (schema is null || !visited.Add(schema))
        {
            return;
        }

        var effective = schema is OpenApiSchemaReference reference
            ? reference.Target ?? schema
            : schema;

        if (effective.OneOf is not null)
        {
            foreach (var item in effective.OneOf)
            {
                ApplyToSchema(item, fromNode, toNode, visited);
            }
        }

        if (effective.AnyOf is not null)
        {
            foreach (var item in effective.AnyOf)
            {
                ApplyToSchema(item, fromNode, toNode, visited);
            }
        }

        if (effective.AllOf is not null)
        {
            foreach (var item in effective.AllOf)
            {
                ApplyToSchema(item, fromNode, toNode, visited);
            }
        }

        if (effective.Properties is null)
        {
            return;
        }

        foreach (var (name, property) in effective.Properties)
        {
            if (IsFromName(name))
            {
                SetSchemaDefault(property, fromNode);
            }
            else if (IsToName(name))
            {
                SetSchemaDefault(property, toNode);
            }

            ApplyToSchema(property, fromNode, toNode, visited);
        }
    }

    private static void SetParameterDefault(OpenApiParameter parameter, JsonNode value)
    {
        // Use example only for query params — OpenAPI "default" means "server assumes this if
        // omitted", so clients like Scalar may show the value in the UI but not send it, leaving
        // DateTimeOffset? from/to null in the minimal-API handler.
        parameter.Required = true;
        parameter.Example = value.DeepClone();
        SetSchemaExample(parameter.Schema, value);
    }

    private static void SetSchemaDefault(IOpenApiSchema? schema, JsonNode value)
    {
        switch (schema)
        {
            case OpenApiSchema concrete:
                concrete.Default = value.DeepClone();
                concrete.Example = value.DeepClone();
                break;
            case OpenApiSchemaReference reference:
                // Reference holders support default/example overrides without mutating the shared target.
                reference.Default = value.DeepClone();
                break;
        }
    }

    private static void SetSchemaExample(IOpenApiSchema? schema, JsonNode value)
    {
        switch (schema)
        {
            case OpenApiSchema concrete:
                concrete.Example = value.DeepClone();
                // Clear any prior default so Scalar does not treat this as a server-side default.
                concrete.Default = null;
                break;
            case OpenApiSchemaReference reference:
                reference.Default = null;
                break;
        }
    }

    private static bool IsFromName(string? name) =>
        string.Equals(name, "from", StringComparison.OrdinalIgnoreCase);

    private static bool IsToName(string? name) =>
        string.Equals(name, "to", StringComparison.OrdinalIgnoreCase);
}