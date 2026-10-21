namespace Docmon.Server
{
    /// <summary>Describes a REST endpoint and its corresponding MCP tool.</summary>
    public sealed class ApiOperation
    {
        /// <summary>Stable MCP tool and OpenAPI operation identifier.</summary>
        public string Name { get; }
        /// <summary>HTTP method.</summary>
        public string Method { get; }
        /// <summary>Versioned URL template.</summary>
        public string Path { get; }
        /// <summary>Operator-facing summary.</summary>
        public string Summary { get; }
        /// <summary>Required request property names.</summary>
        public string[] Required { get; }
        /// <summary>Creates an endpoint descriptor.</summary>
        public ApiOperation(string name, string method, string path, string summary, params string[] required)
        { Name = name; Method = method; Path = "/api/v1" + path; Summary = summary; Required = required; }
    }
}
