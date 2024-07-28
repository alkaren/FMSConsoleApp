using System;
using System.IO.Ports;
using System.Text.Json;
using Confluent.Kafka;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Net.NetworkInformation;
using System.Xml.Linq;

class Program
{
    static async Task Main(string[] args)
    {
        string portName = "/dev/ttyACM0";
        int baudRate = 9600;

        var gpsReceiver = new SerialPort(portName, baudRate);
        gpsReceiver.Open();

        var config = new ProducerConfig { BootstrapServers = "20.198.250.153:9092" };
        using var producer = new ProducerBuilder<Null, string>(config).Build();

        CancellationTokenSource cts = new CancellationTokenSource();
        Task.Run(() => ReadGpsDataAndSendToKafka(gpsReceiver, producer, cts.Token));

        Console.WriteLine("Press Enter to exit...");
        Console.ReadLine();

        cts.Cancel();
        gpsReceiver.Close();
    }

    static async Task ReadGpsDataAndSendToKafka(SerialPort gpsReceiver, IProducer<Null, string> producer, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                string data = gpsReceiver.ReadLine();
                var gpsData = ParseGpsData(data);
                if (gpsData != null)
                {
                    var messageData = new Dictionary<string, object>
                    {
                        { "key", Guid.NewGuid().ToString() },
                        { "unitid", "TH0001" },
                        { "drivername", "Alkaren" },
                        { "driver", "https://drive.google.com/file/d/13DzMENEjMaGIo7FJDMHrnX-QOyH5RYcI/view" },
                        { "timestamp", DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm:ss") }, // ISO 8601 format
                        { "latitude", gpsData.Latitude },
                        { "longitude", gpsData.Longitude }
                    };
                    string message = JsonSerializer.Serialize(messageData);
                    var result = await producer.ProduceAsync("test", new Message<Null, string> { Value = message });
                    Console.WriteLine($"Produced message to: {result.TopicPartitionOffset}");
                    Console.WriteLine("Sample Date: " + DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm:ss"));
                }
                else
                {
                    Console.WriteLine("Nothing Sample Date: " + DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm:ss"));
                }
                await Task.Delay(1000); // Sleep for 1 second
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }

    private static GpsData ParseGpsData(string data)
    {
        // Example parsing logic for NMEA sentences, e.g., $GPGGA or $GPRMC
        // This is a simplified example; you'll need to adjust it for your specific GPS data format
        // Process the received data (assuming NMEA format)
        if (data.StartsWith("$GPGGA"))
        {
            // Split the NMEA sentence into fields
            string[] parts = data.Split(',');

            // Check if the sentence has enough fields
            if (parts.Length >= 10)
            {
                if (String.IsNullOrEmpty(parts[2]) || String.IsNullOrEmpty(parts[4]))
                {
                    Console.WriteLine("LatLong Is NULL," + " Date Time: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                    return null;
                }
                // Extract latitude and longitude from the NMEA sentence
                string latitudeNmea = parts[2];  // Latitude (format: ddmm.mmmm)
                string longitudeNmea = parts[4]; // Longitude (format: dddmm.mmmm)
                string latDirection = parts[3];
                string lonDirection = parts[5];

                // Convert NMEA format to decimal degrees
                double latitude = NmeaToDecimal(latitudeNmea, latDirection);
                double longitude = NmeaToDecimal(longitudeNmea, lonDirection);
                //Console.WriteLine(latitude + ", " + longitude + " Date Time: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));

                string googleMapsUrl = $"https://www.google.com/maps?q={latitude.ToString("0.000000").Replace(",", ".")},{longitude.ToString("0.000000").Replace(",", ".")}";
                Console.WriteLine($"Google Maps URL: {googleMapsUrl}");

                return new GpsData
                {
                    Latitude = latitude,
                    Longitude = longitude
                };
            }
            else
            {
                Console.WriteLine("Invalid NMEA sentence format.");
            }
        }

        return null;
    }

    // Method to convert NMEA format (ddmm.mmmm or dddmm.mmmm) to decimal degrees
    private static double NmeaToDecimal(string nmeaCoordinate, string direction)
    {
        double coordinate = double.Parse(nmeaCoordinate, System.Globalization.CultureInfo.InvariantCulture);

        // Example: latitude format is ddmm.mmmm, so divide by 100 to get decimal degrees
        double degrees = Math.Floor(coordinate / 100);
        double minutes = coordinate - (degrees * 100);

        // Calculate decimal degrees
        double decimalDegrees = degrees + (minutes / 60);

        // Ensure proper sign for latitude and longitude based on NMEA format
        // Latitude is positive (N) or negative (S)
        // Longitude is positive (E) or negative (W)
        // Adjust sign based on direction
        if (direction == "S" || direction == "W")
        {
            decimalDegrees *= -1;
        }

        return decimalDegrees;
    }

    static bool CheckInternetConnection()
    {
        try
        {
            using (var ping = new Ping())
            {
                var reply = ping.Send("www.google.com");
                return reply.Status == IPStatus.Success;
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

}

public class GpsData
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
