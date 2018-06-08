using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;

namespace SimplePrograms
{
    public class Aero
    {
        public string ICAO24;
        public string Callsign;
        public string OriginCountry;
        public double Longitude, Latitude;
        public double GeoAltitude;
        private bool _HasRLD = false;
        private double _RelativeGeoDistance;
        public double RelativeGeoDistance
        {
            get
            {
                if (!this._HasRLD)
                {
                    throw new MissingFieldException("Must set a relative geodesic distance before accessing it.");
                }
                return _RelativeGeoDistance;
            }

            set
            {
                _RelativeGeoDistance = value;
                this._HasRLD = true;
            }
        }

        public Aero(string icao24,
                    string callsign,
                    string origin_country,
                    double latitude,
                    double longitude,
                    double geo_altitude)
        {
            ICAO24 = icao24;
            Callsign = callsign;
            OriginCountry = origin_country;
            Latitude = latitude;
            Longitude = longitude;
            GeoAltitude = geo_altitude;
        }
    }

    public class RootResponse
    {
        public int Time { get; set; }
        public List<List<object>> States { get; set; }
    }
    
    public static class AeroTracker
    {
        const double EarthRadius = 6371.009d;
        static readonly string URL = @"http://opensky-network.org/api/states/all";
        static readonly string Header = "application/json";
        static readonly string North = "N", East = "E", South = "S", West = "W";
        private static readonly Dictionary<string, int> DirectionSign
            = new Dictionary<string, int>
            {
                {North, 1},
                {East, 1},
                {South, -1},
                {West, -1}
            };

        public static void NearestAeroplane(string latitude, string longitude)
        {
            double lat = AeroTracker.GetCoordinate(latitude);
            double lon = AeroTracker.GetCoordinate(longitude);
            var response = AeroTracker.GetAPIData();
            var time = response.Time; var states = response.States;
            var aeroList = AeroTracker.StatesToAero(states);
            var nearest = AeroTracker.GetNearest(aeroList, lat, lon);
            AeroTracker.DisplayNearest(nearest, lat, lon);         
        }

        private static void DisplayNearest(Aero nearest, double lat, double lon)
        {
            Console.WriteLine("From:");
            Console.WriteLine("Latitude: {0} degrees", lat);
            Console.WriteLine("Longitude: {0} degrees", lon);
            Console.WriteLine("The closest aeroplane is:");
            Console.WriteLine("Latitude: {0} degrees", nearest.Latitude);
            Console.WriteLine("Longitude: {0} degrees", nearest.Longitude);
            Console.WriteLine("Relative distance: {0} km", nearest.RelativeGeoDistance);
            Console.WriteLine("Geometric Altitude: {0} m", nearest.GeoAltitude);
            Console.WriteLine("Callsign: {0}", nearest.Callsign);
            Console.WriteLine("ICAO24 ID: {0}", nearest.ICAO24);
            Console.WriteLine("Country of origin: {0}", nearest.OriginCountry);
        }

        private static RootResponse GetAPIData()
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue(AeroTracker.Header));

            var response = client.GetAsync(AeroTracker.URL).Result;
            if (response.IsSuccessStatusCode)
            {
                var json = response.Content.ReadAsStringAsync().Result;
                return JsonConvert.DeserializeObject<RootResponse>(json);
            }
            throw new WebException("Could not get data from API");
        }

        private static List<Aero> StatesToAero(List<List<object>> states)
        {
            var aeroList = new List<Aero>();
            foreach (var state in states)
            {
                try { aeroList.Add(AeroTracker.CreateAero(state)); }
                catch (InvalidCastException) { continue; }
                catch (NullReferenceException) { continue; }
            }
            return aeroList;
        }

        // Throws a NullReferenceException if the API returns null instead of a float/double.
        private static Aero CreateAero(List<object> state)   
        {
            string icao24 = (string)state[0], callsign = (string)state[1], origin = (string)state[2];
            double longi = (double)state[5], lati = (double)state[6], geo_alti = (double)state[7];
            return new Aero(icao24, callsign, origin, lati, longi, geo_alti);
        }

        private static double GetCoordinate(string coord)
        {
            var values = coord.Split(null);
            return AeroTracker.GetLocation(values) * AeroTracker.GetSign(values);
        }

        private static double GetLocation(string[] coordList)
        {
            foreach (var value in coordList)
            {
                if (double.TryParse(value, out double location))
                {
                    return location;
                }
            }
            throw new ArgumentException("Could not determine location from coordinate");
        }

        private static int GetSign(string[] coordList)
        {
            foreach (var value in coordList)
            {
                if (AeroTracker.DirectionSign.TryGetValue(value, out int sign))
                {
                    return sign;
                }
            }
            // Default value of North/East
            return 1;
        }

        private static bool AeroWithinTol(Aero aero, double lat, double lon, double latTol, double lonTol)
        {
            double maxLat = lat + latTol, minLat = lat - latTol;
            double maxLon = lon + lonTol, minLon = lon - lonTol;
            double aLat = aero.Latitude, aLon = aero.Longitude;
            if (aLat > maxLat) return false;
            else if (aLat < minLat) return false;
            else if (aLon > maxLon) return false;
            else if (aLon < minLon) return false;
            else return true;
        }

        private static Aero GetNearest(List<Aero> aeroList, double lat, double lon)
        {
            Aero aero;
            Aero nearest = null;
            for (var i = 0; i < aeroList.Count; i++)
            {
                aero = aeroList[i];
                if (i == 0)
                {
                    aero.RelativeGeoDistance = AeroTracker.GeoDistance(lat, lon, aero.Latitude, aero.Longitude);
                    nearest = aero;
                    continue;
                }
                if (nearest == null) throw new NullReferenceException("Could not find nearest during loop");

                double latTol = Math.Max(nearest.Latitude, lat) - Math.Min(nearest.Latitude, lat);
                double lonTol = Math.Max(nearest.Longitude, lon) - Math.Min(nearest.Longitude, lon);
                if (AeroTracker.AeroWithinTol(aero, lat, lon, latTol, lonTol))
                {
                    aero.RelativeGeoDistance = AeroTracker.GeoDistance(lat, lon, aero.Latitude, aero.Longitude);
                    if (aero.RelativeGeoDistance < nearest.RelativeGeoDistance)
                    {
                        nearest = aero;
                    }
                }
            }
            if (nearest == null) throw new NullReferenceException("Could not find nearest during return");
            return nearest;
        }

        private static double GeoDistance(double lat1, double lon1, double lat2, double lon2)
        {
            // Factor used to convert degrees to radians.
            double rad = Math.PI / 180; 
            lat1 *= rad; lon1 *= rad; lat2 *= rad; lon2 *= rad;
            double deltaLat = Math.Max(lat1, lat2) - Math.Min(lat1, lat2);
            double deltaLon = Math.Max(lon1, lon2) - Math.Min(lon1, lon2);
            double haverLat = (1 - Math.Cos(deltaLat)) / 2;
            double haverLon = (1 - Math.Cos(deltaLon)) / 2;
            double haver = haverLat + (Math.Cos(lat1) * Math.Cos(lat2) * haverLon);
            double centralAngle = Math.Atan2(Math.Sqrt(haver), Math.Sqrt(1 - haver));
            double distance = centralAngle * AeroTracker.EarthRadius * 2;
            return distance;
        }
    }
}
