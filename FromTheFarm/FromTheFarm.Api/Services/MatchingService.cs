using FromTheFarm.Api.Models;

namespace FromTheFarm.Api.Services;

// Implements the weighted scoring formula specified in Section 3 of the
// Planning and Design document: distance (35%), crop type match (30%),
// quantity fit (20%), freshness window (15%). Matches scoring below the
// 0.4 threshold are excluded from the feed entirely.
public class MatchingService
{
    private const double DistanceWeight = 0.35;
    private const double CropMatchWeight = 0.30;
    private const double QuantityFitWeight = 0.20;
    private const double FreshnessWeight = 0.15;
    private const double MinimumScoreThreshold = 0.4;

    // How many days past the buyer's deadline a listing's harvest date can be
    // before it decays to zero freshness. Not specified numerically in the
    // design doc — 14 days is a reasonable default for perishable produce and
    // is isolated here as a single constant so it's easy to tune later.
    private const int FreshnessDecayDays = 14;

    /// <summary>
    /// Scores a listing against a demand request. Returns null if the pair
    /// should be excluded from the feed entirely (crop mismatch, or outside
    /// the buyer's search radius) rather than merely scored low.
    /// </summary>
    public double? TryScoreMatch(Listing listing, DemandRequest demand, int searchRadiusKm)
    {
        // Crop type match is binary and exclusionary — no partial credit,
        // per Section 3: a buyer looking for tomatoes has no use for potatoes.
        if (!string.Equals(listing.CropType, demand.CropType, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var distanceKm = CalculateDistanceKm(
            listing.Location.Latitude, listing.Location.Longitude,
            demand.Location.Latitude, demand.Location.Longitude);

        if (distanceKm > searchRadiusKm)
        {
            return null;
        }

        var distanceScore = 1.0 - (distanceKm / searchRadiusKm);
        var quantityScore = ScoreQuantityFit(listing.Quantity, demand.QuantityNeeded);
        var freshnessScore = ScoreFreshness(listing.HarvestDate, demand.Deadline);

        var totalScore =
            (distanceScore * DistanceWeight) +
            (1.0 * CropMatchWeight) + // guaranteed 1.0 — non-matches already returned null above
            (quantityScore * QuantityFitWeight) +
            (freshnessScore * FreshnessWeight);

        return totalScore >= MinimumScoreThreshold ? totalScore : null;
    }

    private static double ScoreQuantityFit(decimal supply, decimal demand)
    {
        var larger = Math.Max(supply, demand);
        if (larger == 0) return 1.0;

        var difference = Math.Abs(supply - demand);
        var score = 1.0 - (double)(difference / larger);
        return Math.Clamp(score, 0.0, 1.0);
    }

    private static double ScoreFreshness(DateOnly harvestDate, DateOnly deadline)
    {
        // Harvest ready on or before the buyer's deadline: full credit.
        if (harvestDate <= deadline)
        {
            return 1.0;
        }

        // Harvest ready after the deadline: decay linearly to zero over
        // FreshnessDecayDays, since the produce is progressively less useful
        // to a buyer whose need has already passed.
        var daysLate = harvestDate.DayNumber - deadline.DayNumber;
        var score = 1.0 - ((double)daysLate / FreshnessDecayDays);
        return Math.Clamp(score, 0.0, 1.0);
    }

    /// <summary>
    /// Haversine great-circle distance between two coordinates, in kilometres.
    /// </summary>
    public static double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusKm = 6371.0;

        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);

        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return earthRadiusKm * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
}
