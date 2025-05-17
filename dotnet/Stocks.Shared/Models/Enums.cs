using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Stocks.Shared.Models;

public enum ErrorCodes : int {
    [JsonPropertyName("NONE")] None = 0,
    [JsonPropertyName("GENERIC_ERROR")] GenericError = 1,
    [JsonPropertyName("NOT_FOUND")] NotFound = 2,
    [JsonPropertyName("TOO_MANY_RETRIES")] TooManyRetries = 3,
    [JsonPropertyName("DUPLICATE")] Duplicate = 4,
    [JsonPropertyName("VALIDATION_ERROR")] ValidationError = 5,
    [JsonPropertyName("CONCURRENCY_CONFLICT")] ConcurrencyConflict = 6,
    [JsonPropertyName("SERIALIZATION_ERROR")] SerializationError = 7,
}
