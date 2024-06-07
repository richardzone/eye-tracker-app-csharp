using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.Aruco;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;
using log4net;
using log4net.Config;

namespace eye_tracker_app_csharp;

[SupportedOSPlatform("windows")]
internal class Program
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

    private static void Main(string[] args)
    {
        XmlConfigurator.Configure();

        if (Screen.PrimaryScreen == null)
        {
            Log.Error("Primary Screen is null");
            return;
        }

        var screenWidth = Screen.PrimaryScreen.Bounds.Width;
        var screenHeight = Screen.PrimaryScreen.Bounds.Height;
        Log.Info($"Screen size is {screenWidth}x{screenHeight}");

        var duration = 0; // Default duration is 0 (instant move)

        if (args.Length > 0 && int.TryParse(args[0], out var cameraIndex))
        {
            Log.Info($"Camera ID provided via command line: {cameraIndex}");

            // Check for the --duration parameter
            for (var i = 1; i < args.Length; i++)
                if (args[i] == "--duration" && i + 1 < args.Length && int.TryParse(args[i + 1], out var parsedDuration))
                {
                    duration = parsedDuration;
                    Log.Info($"Duration provided via command line: {duration} ms");
                }
        }
        else
        {
            var cameraIds = ListCameras();
            if (cameraIds.Count == 1)
            {
                cameraIndex = cameraIds[0];
                Log.Info($"Only one camera ID found: {cameraIndex}. Using this ID.");
            }
            else
            {
                cameraIndex = GetCameraSelection(cameraIds);
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

        while (true)
        {
            using var frame = capture.QueryFrame();
            if (frame == null)
            {
                Log.Error("Failed to capture frame from camera.");
                break;
            }

            // Crop the frame to the bottom half
            var bottomHalf = new Mat(frame, new Rectangle(0, frame.Rows / 2, frame.Cols, frame.Rows / 2));

            using var grayscale = new Mat();
            CvInvoke.CvtColor(bottomHalf, grayscale, ColorConversion.Bgr2Gray);
            var ids = new VectorOfInt();
            var corners = new VectorOfVectorOfPointF();

            ArucoInvoke.DetectMarkers(grayscale, dictionary, corners, ids, parameters);

            if (ids.Size == 4)
            {
                // Sort the markers by x-coordinate
                var markerData = corners.ToArrayOfArray().Select((corner, index) => new
                {
                    corner[0].X,
                    Corner = corner,
                    Id = ids[index]
                }).OrderBy(marker => marker.X).ToArray();

                var sortedIds = new VectorOfInt();
                foreach (var marker in markerData) sortedIds.Push(new[] { marker.Id });
                Log.Info($"Detected Aruco markers with IDs: {string.Join(", ", sortedIds.ToArray())}");

                var markerIds = sortedIds.ToArray();
                var x = 100 * markerIds[0] + markerIds[1];
                var y = 100 * markerIds[2] + markerIds[3];

                if (x >= 0 && x <= screenWidth && y >= 0 && y <= screenHeight)
                {
                    MoveCursor(x, y, duration);
                    Log.Info($"Mouse moved to ({x}, {y})");
                }
                else
                {
                    Log.Error($"Calculated coordinates ({x}, {y}) are out of bounds.");
                }
            }
            else
            {
                Log.Error($"Did not detect exactly 4 Aruco markers. {ids.Size} detected.");
            }
        }

        capture.Dispose();
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
        foreach (var id in cameraIds)
        {
            Console.WriteLine($"- {id}");
        }

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