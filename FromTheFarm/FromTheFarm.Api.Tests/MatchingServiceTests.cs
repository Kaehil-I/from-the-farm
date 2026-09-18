using FromTheFarm.Api.Models;
using FromTheFarm.Api.Services;
using Xunit;

namespace FromTheFarm.Api.Tests;

public class MatchingServiceTests
{
    private readonly MatchingService _sut = new(); // "sut" = System Under Test

    private static Listing MakeListing(
        string cropType = "Tomato",
        decimal quantity = 100,
        double lat = 0,
        double lon = 0,
        DateOnly? harvestDate = null) => new()
        {
            CropType = cropType,
            Quantity = quantity,
            Unit = "kg",
            Location = new GeoLocation { Latitude = lat, Longitude = lon },
            HarvestDate = harvestDate ?? new DateOnly(2026, 1, 1)
        };

    private static DemandRequest MakeDemand(
        string cropType = "Tomato",
        decimal quantityNeeded = 100,
        double lat = 0,
        double lon = 0,
        DateOnly? deadline = null) => new()
        {
            CropType = cropType,
            QuantityNeeded = quantityNeeded,
            Unit = "kg",
            Location = new GeoLocation { Latitude = lat, Longitude = lon },
            Deadline = deadline ?? new DateOnly(2026, 1, 1)
        };

    [Fact]
    public void DifferentCropTypes_AreExcludedEntirely()
    {
        var listing = MakeListing(cropType: "Tomato");
        var demand = MakeDemand(cropType: "Potato");

        var score = _sut.TryScoreMatch(listing, demand, searchRadiusKm: 50);

        Assert.Null(score);
    }

    [Fact]
    public void CropTypeComparison_IsCaseInsensitive()
    {
        var listing = MakeListing(cropType: "tomato");
        var demand = MakeDemand(cropType: "TOMATO");

        var score = _sut.TryScoreMatch(listing, demand, searchRadiusKm: 50);

        Assert.NotNull(score);
    }

    [Fact]
    public void DistanceBeyondSearchRadius_IsExcludedEntirely()
    {
        var listing = MakeListing(lat: 0.0, lon: 0.0);
        var demand = MakeDemand(lat: 1.4, lon: 0.0); // ~157km apart

        var score = _sut.TryScoreMatch(listing, demand, searchRadiusKm: 50);

        Assert.Null(score);
    }

    [Fact]
    public void SameLocation_SameQuantity_HarvestBeforeDeadline_ScoresPerfectMatch()
    {
        var listing = MakeListing(quantity: 100, harvestDate: new DateOnly(2026, 1, 1));
        var demand = MakeDemand(quantityNeeded: 100, deadline: new DateOnly(2026, 1, 10));

        var score = _sut.TryScoreMatch(listing, demand, searchRadiusKm: 50);

        Assert.NotNull(score);
        Assert.Equal(1.0, score!.Value, precision: 5);
    }

    [Fact]
    public void HarvestDateLateByHalfTheDecayWindow_LosesHalfTheFreshnessCredit()
    {
        // Expected: (1.0*0.35) + (1.0*0.30) + (1.0*0.20) + (0.5*0.15) = 0.925
        var listing = MakeListing(quantity: 100, harvestDate: new DateOnly(2026, 1, 8));
        var demand = MakeDemand(quantityNeeded: 100, deadline: new DateOnly(2026, 1, 1));

        var score = _sut.TryScoreMatch(listing, demand, searchRadiusKm: 50);

        Assert.NotNull(score);
        Assert.Equal(0.925, score!.Value, precision: 3);
    }

    [Fact]
    public void HarvestDateLateBeyondTheDecayWindow_FreshnessClampsToZero_ButNeverGoesNegative()
    {
        var listing = MakeListing(quantity: 100, harvestDate: new DateOnly(2026, 1, 31)); // 30 days late
        var demand = MakeDemand(quantityNeeded: 100, deadline: new DateOnly(2026, 1, 1));

        var score = _sut.TryScoreMatch(listing, demand, searchRadiusKm: 50);

        Assert.NotNull(score);
        Assert.Equal(0.85, score!.Value, precision: 3);
    }

    [Fact]
    public void VeryPoorMatchAcrossEveryCategory_FallsBelowThresholdAndIsExcluded()
    {
        var listing = MakeListing(quantity: 100, lat: 0.0, lon: 0.0, harvestDate: new DateOnly(2026, 2, 1));
        var demand = MakeDemand(quantityNeeded: 0, lat: 0.449, lon: 0.0, deadline: new DateOnly(2026, 1, 1));

        var score = _sut.TryScoreMatch(listing, demand, searchRadiusKm: 50);

        Assert.Null(score);
    }

    [Fact]
    public void CalculateDistanceKm_SamePoint_IsZero()
    {
        var distance = MatchingService.CalculateDistanceKm(-29.8587, 31.0218, -29.8587, 31.0218);

        Assert.Equal(0.0, distance, precision: 5);
    }

    [Fact]
    public void CalculateDistanceKm_OneDegreeOfLongitudeAtTheEquator_IsAboutOneHundredElevenKm()
    {
        var distance = MatchingService.CalculateDistanceKm(0, 0, 0, 1);

        Assert.True(distance is > 111.0 and < 111.3, $"Expected roughly 111.2km, got {distance}km");
    }
}