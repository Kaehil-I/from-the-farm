using FromTheFarm.Api.Models;
using FromTheFarm.Api.Services;
using Xunit;

namespace FromTheFarm.Api.Tests;

// These limits are mirrored from the Android client's FormValidation.kt. If one
// side changes, these tests are the place the divergence should show up.
public class RequestValidationTests
{
    [Theory]
    [InlineData(-90.0, -180.0)]
    [InlineData(90.0, 180.0)]
    [InlineData(-29.85, 31.02)]
    [InlineData(0.0, 0.0)]
    public void Location_AcceptsCoordinatesOnAndInsideTheBounds(double latitude, double longitude)
    {
        Assert.Null(RequestValidation.Location(new GeoLocation { Latitude = latitude, Longitude = longitude }));
    }

    [Theory]
    [InlineData(90.1, 0.0)]
    [InlineData(-90.1, 0.0)]
    [InlineData(0.0, 180.1)]
    [InlineData(0.0, -180.1)]
    [InlineData(double.NaN, 0.0)]
    [InlineData(0.0, double.PositiveInfinity)]
    public void Location_RejectsCoordinatesOutsideTheBounds(double latitude, double longitude)
    {
        Assert.NotNull(RequestValidation.Location(new GeoLocation { Latitude = latitude, Longitude = longitude }));
    }

    [Fact]
    public void Location_RejectsAMissingLocation()
    {
        Assert.NotNull(RequestValidation.Location(null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CropType_RejectsBlankValues(string? cropType)
    {
        Assert.NotNull(RequestValidation.CropType(cropType));
    }

    [Fact]
    public void CropType_RejectsAValueLongerThanTheLimit()
    {
        Assert.NotNull(RequestValidation.CropType(new string('a', RequestValidation.MaxCropTypeLength + 1)));
        Assert.Null(RequestValidation.CropType(new string('a', RequestValidation.MaxCropTypeLength)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Quantity_RejectsNonPositiveValues(int quantity)
    {
        Assert.NotNull(RequestValidation.Quantity(quantity, "quantity"));
    }

    [Fact]
    public void Quantity_RejectsValuesAboveTheCeiling()
    {
        Assert.Null(RequestValidation.Quantity(RequestValidation.MaxQuantity, "quantity"));
        Assert.NotNull(RequestValidation.Quantity(RequestValidation.MaxQuantity + 1, "quantity"));
    }

    [Fact]
    public void Unit_RejectsBlankAndOverlongValues()
    {
        Assert.NotNull(RequestValidation.Unit(" "));
        Assert.NotNull(RequestValidation.Unit(new string('u', RequestValidation.MaxUnitLength + 1)));
        Assert.Null(RequestValidation.Unit("kg"));
    }

    [Fact]
    public void Deadline_RejectsYesterdayButAcceptsToday()
    {
        var today = new DateOnly(2026, 9, 20);

        Assert.NotNull(RequestValidation.Deadline(today.AddDays(-1), today));
        Assert.Null(RequestValidation.Deadline(today, today));
        Assert.Null(RequestValidation.Deadline(today.AddDays(1), today));
    }

    [Theory]
    [InlineData("Farmer", true)]
    [InlineData("Buyer", true)]
    [InlineData("farmer", false)]
    [InlineData("Admin", false)]
    [InlineData(null, false)]
    public void Role_AcceptsOnlyTheTwoDocumentedValues(string? role, bool expectedValid)
    {
        Assert.Equal(expectedValid, RequestValidation.Role(role) is null);
    }

    [Theory]
    [InlineData("en", true)]
    [InlineData("zu", true)]
    [InlineData("af", true)]
    [InlineData("EN", false)]
    [InlineData("xh", false)]
    [InlineData(null, false)]
    public void Language_AcceptsOnlyTheThreeSupportedCodes(string? language, bool expectedValid)
    {
        Assert.Equal(expectedValid, RequestValidation.Language(language) is null);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("+27 82 555 0143", true)]
    [InlineData("(031) 555-0143", true)]
    [InlineData("0315550", true)]
    [InlineData("031555", false)]
    [InlineData("+27 82 555 0143 ext 1234567890", false)]
    [InlineData("0824lifted", false)]
    public void Phone_AcceptsOptionalNumbersInTheClientsFormat(string? phone, bool expectedValid)
    {
        Assert.Equal(expectedValid, RequestValidation.Phone(phone) is null);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void SearchRadiusKm_RejectsValuesOutsideTheSliderRange(int radius)
    {
        Assert.NotNull(RequestValidation.SearchRadiusKm(radius));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(25)]
    public void SearchRadiusKm_AcceptsValuesInsideTheSliderRange(int radius)
    {
        Assert.Null(RequestValidation.SearchRadiusKm(radius));
    }

    [Fact]
    public void Photo_TreatsAnAbsentPayloadAsAcceptableAndProducesNothingToStore()
    {
        Assert.Null(RequestValidation.Photo(null, out var dataUri));
        Assert.Null(dataUri);

        Assert.Null(RequestValidation.Photo("   ", out var blankDataUri));
        Assert.Null(blankDataUri);
    }

    [Fact]
    public void Photo_BuildsADataUriForAJpeg()
    {
        var base64 = Convert.ToBase64String(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 });

        Assert.Null(RequestValidation.Photo(base64, out var dataUri));
        Assert.Equal($"data:image/jpeg;base64,{base64}", dataUri);
    }

    [Fact]
    public void Photo_BuildsADataUriForAPng()
    {
        var base64 = Convert.ToBase64String(
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        Assert.Null(RequestValidation.Photo(base64, out var dataUri));
        Assert.Equal($"data:image/png;base64,{base64}", dataUri);
    }

    [Fact]
    public void Photo_AcceptsAFullDataUriAsWellAsABarePayload()
    {
        var base64 = Convert.ToBase64String(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 });

        Assert.Null(RequestValidation.Photo($"data:image/jpeg;base64,{base64}", out var dataUri));
        Assert.Equal($"data:image/jpeg;base64,{base64}", dataUri);
    }

    [Fact]
    public void Photo_RejectsSomethingThatIsNotAnImage()
    {
        var pdfHeader = Convert.ToBase64String(new byte[] { 0x25, 0x50, 0x44, 0x46 });

        Assert.NotNull(RequestValidation.Photo(pdfHeader, out var dataUri));
        Assert.Null(dataUri);
    }

    [Fact]
    public void Photo_RejectsAPayloadThatIsNotValidBase64()
    {
        Assert.NotNull(RequestValidation.Photo("this is not base64!!", out var dataUri));
        Assert.Null(dataUri);
    }

    [Fact]
    public void Photo_RejectsAnOversizedPayloadWithoutDecodingIt()
    {
        var oversized = new string('A', (RequestValidation.MaxPhotoBytes / 3 * 4) + 1024);

        Assert.NotNull(RequestValidation.Photo(oversized, out var dataUri));
        Assert.Null(dataUri);
    }

    [Fact]
    public void ForListing_ReportsTheFirstProblemItFinds()
    {
        var valid = new GeoLocation { Latitude = -29.85, Longitude = 31.02 };

        Assert.Null(RequestValidation.ForListing("Tomatoes", 50, "kg", valid));
        Assert.NotNull(RequestValidation.ForListing("", 50, "kg", valid));
        Assert.NotNull(RequestValidation.ForListing("Tomatoes", 0, "kg", valid));
        Assert.NotNull(RequestValidation.ForListing("Tomatoes", 50, "", valid));
        Assert.NotNull(RequestValidation.ForListing("Tomatoes", 50, "kg", null));
    }

    [Fact]
    public void ForDemand_AlsoChecksTheDeadline()
    {
        var valid = new GeoLocation { Latitude = -29.85, Longitude = 31.02 };
        var today = new DateOnly(2026, 9, 20);

        Assert.Null(RequestValidation.ForDemand("Tomatoes", 20, "kg", valid, today, today));
        Assert.NotNull(RequestValidation.ForDemand("Tomatoes", 20, "kg", valid, today.AddDays(-1), today));
    }
}
