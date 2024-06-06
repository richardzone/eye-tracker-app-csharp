﻿using System.Runtime.InteropServices;
using Emgu.CV;
using Emgu.CV.Aruco;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;
using log4net;
using log4net.Config;

namespace eye_tracker_app_csharp;

internal class Program
{
    private const int MaxScreenWidth = 1920;
    private const int MaxScreenHeight = 1080;
    private static readonly ILog Log = LogManager.GetLogger(typeof(Program));

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out Point lpPoint);

    private static void Main(string[] args)
    {
        XmlConfigurator.Configure();

        int duration = 0; // Default duration is 0 (instant move)

        if (args.Length > 0 && int.TryParse(args[0], out var cameraIndex))
        {
            Log.Info($"Camera ID provided via command line: {cameraIndex}");

            // Check for the --duration parameter
            for (var i = 1; i < args.Length; i++)
            {
                if (args[i] == "--duration" && i + 1 < args.Length && int.TryParse(args[i + 1], out var parsedDuration))
                {
                    duration = parsedDuration;
                    Log.Info($"Duration provided via command line: {duration} ms");
                }
            }
        }
        else
        {
            ListCameras();
            cameraIndex = GetCameraSelection();
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
            Log.Info($"Successfully opened camera with ID: {cameraIndex}");
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

            using var grayscale = new Mat();
            CvInvoke.CvtColor(frame, grayscale, ColorConversion.Bgr2Gray);
            var ids = new VectorOfInt();
            var corners = new VectorOfVectorOfPointF();

            ArucoInvoke.DetectMarkers(grayscale, dictionary, corners, ids, parameters);

            if (ids.Size == 4)
            {
                Log.Info($"Detected Aruco markers with IDs: {string.Join(", ", ids.ToArray())}");
                var markerIds = ids.ToArray();
                var x = 100 * markerIds[0] + markerIds[1];
                var y = 100 * markerIds[2] + markerIds[3];

                if (x is >= 0 and <= MaxScreenWidth && y is >= 0 and <= MaxScreenHeight)
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

    private static void ListCameras()
    {
        // Set OpenCV log level to silent
        var logLevel = CvInvoke.LogLevel;
        Log.Debug($"OpenCV Current Log level: {logLevel}");
        CvInvoke.LogLevel = LogLevel.Silent;
        for (var i = 0; i < 10; i++)
            try
            {
                using var capture = new VideoCapture(i);
                if (!capture.IsOpened) continue;
                Log.Info($"Camera ID {i} is available.");
            }
            catch
            {
                // ignored
            }

        CvInvoke.LogLevel = logLevel;
    }

    private static int GetCameraSelection()
    {
        Console.Write("Select a camera ID: ");
        if (int.TryParse(Console.ReadLine(), out var selectedCamera)) return selectedCamera;

        Log.Error("Invalid camera ID input.");
        return -1;
    }

    private static void MoveCursor(int targetX, int targetY, int duration)
    {
        if (duration <= 0)
        {
            SetCursorPos(targetX, targetY);
            return;
        }

        // Get the current cursor position
        if (!GetCursorPos(out var currentPos))
        {
            Log.Error("Failed to get current cursor position.");
            return;
        }

        var startX = currentPos.X;
        var startY = currentPos.Y;

        const int steps = 100; // Number of steps for the smooth movement
        var stepDuration = duration / steps;

        for (var i = 0; i <= steps; i++)
        {
            var x = startX + (targetX - startX) * i / steps;
            var y = startY + (targetY - startY) * i / steps;
            SetCursorPos(x, y);
            Thread.Sleep(stepDuration);
        }
    }

    private struct Point
    {
        public int X;
        public int Y;
    }
}