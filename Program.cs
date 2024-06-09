using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.Aruco;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;
using log4net;
using log4net.Config;
using log4net.Core;
using log4net.Repository.Hierarchy;

namespace eye_tracker_app_csharp;

[SupportedOSPlatform("windows")]
internal class Program
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

    private static void Main(string[] args)
    {
        XmlConfigurator.Configure();

        var logLevel = Level.Info; // Set default log level to Info
        var duration = 0; // Default duration is 0 (instant move)
        var cameraIndex = -1;

        // Parse command line arguments
        for (var i = 0; i < args.Length; i++)
            if (args[i] == "--loglevel" && i + 1 < args.Length)
            {
                logLevel = GetLogLevel(args[i + 1]);
                i++; // Skip the next argument as it is the log level value
            }
            else if (int.TryParse(args[i], out var parsedCameraIndex))
            {
                cameraIndex = parsedCameraIndex;
                Log.Info($"Camera ID provided via command line: {cameraIndex}");
            }
            else if (args[i] == "--duration" && i + 1 < args.Length &&
                     int.TryParse(args[i + 1], out var parsedDuration))
            {
                duration = parsedDuration;
                Log.Info($"Duration provided via command line: {duration} ms");
                i++; // Skip the next argument as it is the duration value
            }

        // Configure log4net and set the log level
        ((Hierarchy)LogManager.GetRepository()).Root.Level = logLevel;
        ((Hierarchy)LogManager.GetRepository()).RaiseConfigurationChanged(EventArgs.Empty);

        Log.Info($"Log level set to: {logLevel}");

        if (Screen.PrimaryScreen == null)
        {
            Log.Error("Primary Screen is null");
            return;
        }

        var screenWidth = Screen.PrimaryScreen.Bounds.Width;
        var screenHeight = Screen.PrimaryScreen.Bounds.Height;
        Log.Info($"Screen size is {screenWidth}x{screenHeight}");

        if (cameraIndex < 0)
        {
            var cameraIds = ListCameras();
            switch (cameraIds.Count)
            {
                case 0:
                    Log.Error("No cameras available.");
                    Console.WriteLine("No cameras available. Press any key to exit.");
                    Console.ReadKey();
                    return;
                case 1:
                    cameraIndex = cameraIds[0];
                    Log.Info($"Only one camera ID found: {cameraIndex}. Using this ID.");
                    break;
                default:
                    cameraIndex = GetCameraSelection(cameraIds);
                    break;
            }
        }

        if (cameraIndex < 0)
        {
            Log.Error("No valid camera ID selected.");
            return;
        }

        VideoCapture capture;
        try
        {
            capture = new VideoCapture(cameraIndex);
            Log.Info($"VideoCapture opened with camera ID: {cameraIndex}");
        }
        catch (Exception ex)
        {
            Log.Error("Failed to open camera.", ex);
            return;
        }

        var dictionary = new Dictionary(Dictionary.PredefinedDictionaryName.Dict6X6_100);
        var parameters = DetectorParameters.GetDefault();

        try
        {
            while (true)
            {
                using var frame = new Mat();
                capture.Read(frame);
                if (frame.IsEmpty)
                {
                    Log.Error("Failed to capture frame from camera.");
                    break;
                }

                ParseFrameAndMove(frame, dictionary, parameters, screenWidth, screenHeight, duration);
            }
        }
        catch (Exception ex)
        {
            Log.Error("An error occurred during frame processing.", ex);
        }
        finally
        {
            capture.Dispose();
        }
    }

    private static void ParseFrameAndMove(Mat frame, Dictionary dictionary, DetectorParameters parameters,
        int screenWidth,
        int screenHeight, int duration)
    {
        var (x, y) = ParseScreenCoordinates(frame, dictionary, parameters);
        if (x.HasValue && y.HasValue) ValidateAndMoveCursor(x.Value, screenWidth, y.Value, screenHeight, duration);
    }

    private static (int? x, int? y) ParseScreenCoordinates(Mat frame, Dictionary dictionary,
        DetectorParameters parameters)
    {
        // Crop the frame to the bottom half
        using var bottomHalf = new Mat(frame, new Rectangle(0, frame.Rows / 2, frame.Cols, frame.Rows / 2));

        using var ids = new VectorOfInt();
        using var corners = new VectorOfVectorOfPointF();

        ArucoInvoke.DetectMarkers(bottomHalf, dictionary, corners, ids, parameters);

        if (ids.Size == 4)
        {
            // Sort the markers by x-coordinate
            var markerData = corners.ToArrayOfArray().Select((corner, index) => new
            {
                corner[0].X,
                Corner = corner,
                Id = ids[index]
            }).OrderBy(marker => marker.X).ToArray();

            using var sortedIds = new VectorOfInt();
            foreach (var marker in markerData) sortedIds.Push(new[] { marker.Id });
            Log.Info($"Detected Aruco markers with IDs: {string.Join(", ", sortedIds.ToArray())}");

            var markerIds = sortedIds.ToArray();
            var x = 100 * markerIds[0] + markerIds[1];
            var y = 100 * markerIds[2] + markerIds[3];

            return (x, y);
        }

        Log.Warn($"Did not detect exactly 4 Aruco markers. {ids.Size} detected.");
        return (null, null);
    }

    private static void ValidateAndMoveCursor(int x, int screenWidth, int y, int screenHeight, int duration)
    {
        if (x >= 0 && x <= screenWidth && y >= 0 && y <= screenHeight)
        {
            MoveCursor(x, y, duration);
            Log.Info($"Mouse moved to ({x}, {y})");
            return;
        }

        Log.Error($"Calculated coordinates ({x}, {y}) are out of bounds.");
    }

    private static Level GetLogLevel(string logLevelStr)
    {
        return logLevelStr.ToLower() switch
        {
            "debug" => Level.Debug,
            "info" => Level.Info,
            "warn" => Level.Warn,
            "error" => Level.Error,
            "fatal" => Level.Fatal,
            _ => Level.Info
        };
    }

    private static List<int> ListCameras()
    {
        var cameraIds = new List<int>();

        // Set OpenCV log level to silent
        var logLevel = CvInvoke.LogLevel;
        Log.Debug($"OpenCV Current Log level: {logLevel}");
        CvInvoke.LogLevel = LogLevel.Silent;
        for (var i = 0; i < 10; i++)
            try
            {
                using var capture = new VideoCapture(i);
                if (!capture.IsOpened) continue;
                cameraIds.Add(i);
                Log.Info($"Camera ID {i} is available.");
            }
            catch
            {
                // ignored
            }

        CvInvoke.LogLevel = logLevel;
        return cameraIds;
    }

    private static int GetCameraSelection(List<int> cameraIds)
    {
        Console.WriteLine("Available camera IDs:");
        foreach (var id in cameraIds) Console.WriteLine($"- {id}");

        Console.Write("Select a camera ID: ");
        if (int.TryParse(Console.ReadLine(), out var selectedCamera) && cameraIds.Contains(selectedCamera))
            return selectedCamera;

        Log.Error("Invalid camera ID input.");
        return -1;
    }

    private static void MoveCursor(int targetX, int targetY, int duration)
    {
        if (duration <= 0)
        {
            Cursor.Position = new Point(targetX, targetY);
            return;
        }

        var currentPos = Cursor.Position;

        var startX = currentPos.X;
        var startY = currentPos.Y;

        const int steps = 100; // Number of steps for the smooth movement
        var stepDuration = duration / steps;

        for (var i = 0; i <= steps; i++)
        {
            var x = startX + (targetX - startX) * i / steps;
            var y = startY + (targetY - startY) * i / steps;
            Cursor.Position = new Point(x, y);
            Thread.Sleep(stepDuration);
        }
    }
}