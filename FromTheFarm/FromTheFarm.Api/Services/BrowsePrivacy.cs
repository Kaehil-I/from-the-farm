using FromTheFarm.Api.Models;

namespace FromTheFarm.Api.Services;

// What other people may see of a record when browsing, as opposed to what its
// owner sees of their own. A listing is a farm and a demand is a buyer's site,
// so exact coordinates plus the owner's account id would let any signed-in user
// locate and track someone. Browsing needs "roughly how far away", not an
// address, so coordinates are rounded and ownership identifiers are dropped.
public static class BrowsePrivacy
{
    // Two decimal places is about 1.1 km of latitude: close enough for the
    // distance filters and recommendations, too coarse to identify a property.
    public const int CoordinateDecimals = 2;

    public static GeoLocation Coarsen(GeoLocation location) => new()
    {
        Latitude = Math.Round(location.Latitude, CoordinateDecimals),
        Longitude = Math.Round(location.Longitude, CoordinateDecimals)
    };

    // These mutate and return the instance. That is safe here because they are
    // applied to documents just read from the database and never written back.
    public static Listing Redact(Listing listing)
    {
        listing.FarmerId = string.Empty;
        listing.ClientGeneratedId = null;
        listing.Location = Coarsen(listing.Location);
        return listing;
    }

    public static DemandRequest Redact(DemandRequest demand)
    {
        demand.BuyerId = string.Empty;
        demand.ClientGeneratedId = null;
        demand.Location = Coarsen(demand.Location);
        return demand;
    }
}
