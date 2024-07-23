using System;
using System.IO.Ports;
using System.Text.Json;
using System.Threading.Tasks;
using Confluent.Kafka;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Globalization;

class Program
{
    static SerialPort serialPort;
    static async Task Main(string[] args)
    {
        string portName = "/dev/ttyACM0"; //FindSerialPort();

        if (portName == null)
        {
            Console.WriteLine("GPS receiver not detected. Exiting...");
            return;
        }

        serialPort = new SerialPort();
        serialPort.PortName = portName;
        serialPort.BaudRate = 9600;    // GPS usually communicates at 9600 baud
        serialPort.Parity = Parity.None;
        serialPort.DataBits = 8;
        serialPort.StopBits = StopBits.One;
        serialPort.Handshake = Handshake.None;

        var kafkaConfig = new ProducerConfig { BootstrapServers = "20.198.250.153:9092" };
        using var producer = new ProducerBuilder<Null, string>(kafkaConfig).Build();
        serialPort.DataReceived += (sender, e) => DataReceivedHandler(sender, e, producer);

        serialPort.Open();
        Console.WriteLine($"Serial port {portName} opened successfully. Waiting for GPS data...");
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }

    private static async void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e, IProducer<Null, string> producer)
    {
        SerialPort sp = (SerialPort)sender;
        string indata = sp.ReadLine();  // Read the data from the serial port
        var gpsData = ParseGpsData(indata);

        if (gpsData != null)
        {
            var messageData = new Dictionary<string, object>
            {
                { "key", Guid.NewGuid().ToString() },
                { "unitid", "TH0001" },
                { "drivername", "Alkaren" },
                { "driver", "https://drive.google.com/file/d/13DzMENEjMaGIo7FJDMHrnX-QOyH5RYcI/view" },
                { "timestamp", DateTime.UtcNow.ToString("o") }, // ISO 8601 format
                { "latitude", gpsData.Latitude },
                { "longitude", gpsData.Longitude }
            };

            string message = JsonSerializer.Serialize(messageData);
            var result = await producer.ProduceAsync("test", new Message<Null, string> { Value = message });
            Console.WriteLine($"Produced message to: {result.TopicPartitionOffset}");
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
                Console.WriteLine(latitude + ", " + longitude + " Date Time: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));

                // Output latitude and longitude in Google Maps compatible format
                //Console.WriteLine($"Latitude: -{latitude.ToString("0.000000").Replace(",", ".")}");
                //Console.WriteLine($"Longitude: {longitude.ToString("0.000000").Replace(",", ".")}");
                string googleMapsUrl = $"https://www.google.com/maps?q={latitude.ToString("0.000000").Replace(",", ".")},{longitude.ToString("0.000000").Replace(",", ".")}";
                Console.WriteLine($"Google Maps URL: {googleMapsUrl}");

                //double doubleValueLat = double.Parse(latitude.ToString("0.000000").Replace(",", "."));
                //double doubleValueLong = double.Parse($"{longitude.ToString("0.000000").Replace(",", ".")}");
                //Console.WriteLine($"{doubleValueLat}, {doubleValueLong}");
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
}

public class GpsData
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
