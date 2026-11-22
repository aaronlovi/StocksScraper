using System.Collections.Generic;
using Stocks.DataModels.Scoring;

namespace Stocks.EDGARScraper.Tests.Scoring;

public class ScoringCheckResultsTests {
    private static List<ScoringCheck> MakeScorecard(string pattern) {
        var checks = new List<ScoringCheck>();
        for (int i = 0; i < pattern.Length; i++) {
            ScoringCheckResult result = pattern[i] switch {
                'P' => ScoringCheckResult.Pass,
                'F' => ScoringCheckResult.Fail,
                _ => ScoringCheckResult.NotAvailable,
            };
            checks.Add(new ScoringCheck(i + 1, "c" + (i + 1), null, "", result));
        }
        return checks;
    }

    [Fact]
    public void Encode_WritesOneLetterPerCheckInCheckOrder() {
        Assert.Equal("PFNPPPPPPPPPPPF", ScoringCheckResults.Encode(MakeScorecard("PFNPPPPPPPPPPPF")));
    }

    [Fact]
    public void Decode_RoundTripsEncode() {
        IReadOnlyList<ScoringCheckResult>? decoded = ScoringCheckResults.Decode("PFNPPPPPPPPPPPF");
        Assert.NotNull(decoded);
        Assert.Equal(15, decoded!.Count);
        Assert.Equal(ScoringCheckResult.Pass, decoded[0]);
        Assert.Equal(ScoringCheckResult.Fail, decoded[1]);
        Assert.Equal(ScoringCheckResult.NotAvailable, decoded[2]);
        Assert.Equal(ScoringCheckResult.Fail, decoded[14]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("PPPP")]
    [InlineData("PPPPPPPPPPPPPPPP")]
    [InlineData("PPPPPPPPPPPPPPX")]
    public void Decode_MissingOrMalformedIsNull(string? stored) {
        Assert.Null(ScoringCheckResults.Decode(stored));
    }
}
