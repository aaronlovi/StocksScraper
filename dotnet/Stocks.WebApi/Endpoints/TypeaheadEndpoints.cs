using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Stocks.WebApi.Services;

namespace Stocks.WebApi.Endpoints;

public static class TypeaheadEndpoints {
    public static void MapTypeaheadEndpoints(this IEndpointRouteBuilder app) {
        _ = app.MapGet("/api/typeahead", (string? q, TypeaheadTrieService trie) => {
            if (string.IsNullOrWhiteSpace(q))
                return Results.Ok(new List<TypeaheadResult>());

            List<TypeaheadResult> results = trie.Search(q, 10);
            return Results.Ok(results);
        });
    }
}
