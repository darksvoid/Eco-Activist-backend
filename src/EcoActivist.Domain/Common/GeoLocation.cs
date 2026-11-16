using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace EcoActivist.Domain.Common;

public static class GeoLocation
{
    public const int Wgs84Srid = 4326;

    private static readonly GeometryFactory Factory =
        NtsGeometryServices.Instance.CreateGeometryFactory(Wgs84Srid);

    public static Point CreatePoint(double latitude, double longitude)
    {
        if (double.IsNaN(latitude) || latitude is < -90 or > 90)
            throw new ArgumentOutOfRangeException(nameof(latitude), latitude, "Latitude must be within [-90, 90].");
        if (double.IsNaN(longitude) || longitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(longitude), longitude, "Longitude must be within [-180, 180].");

        return Factory.CreatePoint(new Coordinate(longitude, latitude));
    }
}
