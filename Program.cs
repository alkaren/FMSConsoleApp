using System.IO.Ports;

class Program
{
    static SerialPort serialPort;

    static void Main(string[] args)
    {
        // Dynamically determine the serial port name
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

        serialPort.DataReceived += SerialPort_DataReceived;

        try
        {
            serialPort.Open();
            Console.WriteLine($"Serial port {portName} opened successfully. Waiting for GPS data...");

            // Keep the console application running
            while (true)
            {
                // Your application logic here
                Thread.Sleep(1000); // to wait for data
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error opening serial port: " + ex.Message);
        }
        finally
        {
            serialPort.Close();
        }
    }

    private static void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        SerialPort sp = (SerialPort)sender;
        string indata = sp.ReadLine();  // Read the data from the serial port

        // Process the received data (assuming NMEA format)
        if (indata.StartsWith("$GPGGA"))
        {
            // Split the NMEA sentence into fields
            string[] parts = indata.Split(',');

            // Check if the sentence has enough fields
            if (parts.Length >= 10)
            {
                // Extract latitude and longitude from the NMEA sentence
                string latitudeNmea = parts[2];  // Latitude (format: ddmm.mmmm)
                string longitudeNmea = parts[4]; // Longitude (format: dddmm.mmmm)

                // Convert NMEA format to decimal degrees
                double latitude = NmeaToDecimal(latitudeNmea);
                double longitude = NmeaToDecimal(longitudeNmea);

                // Output latitude and longitude in Google Maps compatible format
                Console.WriteLine($"Latitude: -{latitude.ToString("0.000000").Replace(",",".")}");
                Console.WriteLine($"Longitude: {longitude.ToString("0.000000").Replace(",", ".")}");
            }
            else
            {
                Console.WriteLine("Invalid NMEA sentence format.");
            }
        }
    }

    // Method to convert NMEA format (ddmm.mmmm or dddmm.mmmm) to decimal degrees
    private static double NmeaToDecimal(string nmeaCoordinate)
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
        return decimalDegrees;
    }

    // Method to dynamically find the serial port where GPS receiver is connected
    private static string FindSerialPort()
    {
        try
        {
            // Search for USB serial ports by looking into /dev/ directory
            string[] serialPorts = Directory.GetFiles("/dev/", "ttyUSB*")
                                            .Concat(Directory.GetFiles("/dev/", "ttyACM*"))
                                            .ToArray();

            // Alternatively, you can use dmesg command to check recent kernel messages
            // string[] dmesgOutput = RunShellCommand("dmesg | grep ttyUSB").Split('\n');

            // Filter the list of serial ports to find the correct GPS device
            foreach (string port in serialPorts)
            {
                // Example: Check if the serial port is connected to the GPS device
                // You might need to adjust this based on your GPS receiver's specific identification
                // For demonstration, we check for a specific pattern in the device name
                if (port.Contains("USB0"))
                {
                    return port; // Return the port name if found
                }
            }

            return null; // Return null if GPS receiver is not found
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return null;
        }
    }

    // Method to run a shell command and capture its output
    private static string RunShellCommand(string command)
    {
        var escapedArgs = command.Replace("\"", "\\\"");

        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{escapedArgs}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        process.Start();
        string result = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return result;
    }
}
