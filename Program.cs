// See https://aka.ms/new-console-template for more information
using System.Runtime.InteropServices;
using Iot.Device.Media;

// dotnet publish "PiCamera.csproj" -c Release --runtime linux-arm64 --self-contained
// scp -rp bin/Release/net8.0/linux-arm64/publish/* rcaron@192.168.0.202:/home/rcaron/PiCamera

int frames = 0;

if (RuntimeInformation.ProcessArchitecture != Architecture.Arm64)
{
    Console.WriteLine("Not running on a PI...  Idiot!");
    return;
}

Console.WriteLine("Hole on !  Getting camera information...");

VideoConnectionSettings imageSettings = new VideoConnectionSettings(0, (640, 480), VideoPixelFormat.JPEG);
imageSettings.VerticalFlip = true;
imageSettings.HorizontalFlip = true;

using (VideoDevice videoDevice = VideoDevice.Create(imageSettings))
{
    IEnumerable<VideoPixelFormat> pixelFormats = videoDevice.GetSupportedPixelFormats();

    foreach(var pixelFormat in pixelFormats)
    {
        Console.WriteLine($"Pixel format : {pixelFormat}");
        IEnumerable<Resolution> resolutions = videoDevice.GetPixelFormatResolutions(pixelFormat);
        if (resolutions != null)
        {
            foreach( var resolution in resolutions)
            {
                Console.WriteLine($"\tMin : {resolution.MinWidth} x {resolution.MinHeight}");
                Console.WriteLine($"\tMax : {resolution.MaxWidth} x {resolution.MaxHeight}");
            }
        }
    }

    Console.WriteLine("");
    Console.WriteLine("Taking picture now...");

    videoDevice.Capture("./Image.jpg");

    Console.WriteLine("");
    Console.WriteLine("Capturing around 10 frames now...");

    videoDevice.NewImageBufferReady += NewImageEventHandler;

    videoDevice.StartCaptureContinuous();

    CancellationTokenSource cancellationToken = new CancellationTokenSource();

    new Thread(() => { videoDevice.CaptureContinuous(cancellationToken.Token); }).Start();

    while(frames <= 10)
    {
        Thread.SpinWait(1);
    }
    cancellationToken.Cancel();

    videoDevice.StopCaptureContinuous();
}

Console.WriteLine("Capturing video now!");
VideoConnectionSettings videoSettings = new VideoConnectionSettings(0, (640, 480), VideoPixelFormat.H264);
videoSettings.VerticalFlip = true;
videoSettings.HorizontalFlip = true;

FileStream fileStream = File.Create("./Video.h264");
using (VideoDevice videoDevice = VideoDevice.Create(videoSettings))
{
    videoDevice.NewImageBufferReady += NewVideoEventHandler;

    videoDevice.StartCaptureContinuous();

    CancellationTokenSource cancellationToken = new CancellationTokenSource();

    new Thread(() => { videoDevice.CaptureContinuous(cancellationToken.Token); }).Start();

    while (Console.KeyAvailable is false)
    {
        Thread.SpinWait(1);
    }
    cancellationToken.Cancel();

    videoDevice.StopCaptureContinuous();
    fileStream.Close();
}

async void NewVideoEventHandler(object sender, NewImageBufferReadyEventArgs eventArgs)
{
    try
    {
        await fileStream.WriteAsync(eventArgs.ImageBuffer, 0, eventArgs.Length);
        Console.Write(".");
    }
    catch (ObjectDisposedException)
    {
        // Do nothing, thread is not stopped yet....
    }
}

void NewImageEventHandler(object sender, NewImageBufferReadyEventArgs e)
{
    try
    {
        frames++;

        // No more than 10 frames, even if thread is not stopped yet....
        if (frames <= 10)
        {
            File.WriteAllBytes($"./Image{frames}.jpg", e.ImageBuffer);
            Console.WriteLine($"Got an image... ({frames})");
        }
    }
    catch(ObjectDisposedException)
    {
        // Do nothing, thread is stopped
    }
}